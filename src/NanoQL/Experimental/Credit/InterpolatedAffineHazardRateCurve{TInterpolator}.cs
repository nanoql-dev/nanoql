// SPDX-FileCopyrightText: Copyright (C) 2015 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/interpolatedaffinehazardratecurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the protected constructors without nodes, (dc, model, jumps), (referenceDate, dc, model, jumps) and (settlementDays, cal, dc, model, jumps), which only QuantLib's piecewise curve calls, deriving from this curve, where nanoql's piecewise curve takes the model itself

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A one-factor affine survival structure whose deterministic hazard rate is interpolated on nodes: the survival is
/// that rate's, flat past the last node, times the model's bond price at its initial intensity.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// The formulas are <see cref="AffineHazardRate"/>'s, the model's initial intensity its dynamics' short rate at x0,
/// where QuantLib squares x0 (the S-xcr plan's XR8 (xiii)). <see cref="FromNodes{TBootstrap}"/> builds the curve on a
/// piecewise affine curve's nodes, for its conditional survival: QuantLib's piecewise affine curve is this curve, and
/// nanoql's piecewise curve can't change its base by its traits (XR3 (c)).
/// </remarks>
[Experimental("NQL0001")]
public class InterpolatedAffineHazardRateCurve<TInterpolator> : OneFactorAffineSurvivalStructure
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedAffineHazardRateCurve{TInterpolator}"/>
    /// class.</summary>
    /// <param name="dates">The nodes' dates, the first the reference date.</param>
    /// <param name="hazardRates">The deterministic hazard rates at the nodes.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="model">The model of the default intensity.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException">An argument but the optional ones is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are fewer dates than the interpolator requires ("not enough input dates
    /// given"), or fewer or more rates ("dates/data count mismatch").</exception>
    public InterpolatedAffineHazardRateCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> hazardRates,
        DayCounter dayCounter,
        OneFactorAffineModel model,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator interpolator = default)
        : this(InterpolatedCurve<TInterpolator>.Inputs(dates, hazardRates, dayCounter, nameof(hazardRates)), model, calendar, jumps, jumpDates, interpolator)
    {
    }

    private InterpolatedAffineHazardRateCurve(
        (ImmutableArray<Date> Dates, ImmutableArray<double> Data, DayCounter DayCounter) inputs,
        OneFactorAffineModel model,
        Calendar? calendar,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator interpolator)
        : base(model, inputs.Dates[0], inputs.DayCounter, calendar, jumps, jumpDates)
    {
        // initialize
        dates = inputs.Dates;
        if (!(dates.Length >= TInterpolator.RequiredPoints))
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        if (!(inputs.Data.Length == dates.Length))
        {
            throw new ArgumentException("dates/data count mismatch", "hazardRates");
        }

        curve = InterpolatedCurve<TInterpolator>.Create(dates.AsSpan(), inputs.Data.AsSpan(), dates[0], inputs.DayCounter, interpolator);
    }

    /// <inheritdoc/>
    public override Date MaxDate => dates[^1];

    /// <summary>Builds the curve on a piecewise affine curve's nodes and model.</summary>
    /// <typeparam name="TBootstrap">The piecewise curve's bootstrap.</typeparam>
    /// <param name="piecewise">The piecewise curve, built on a model.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="piecewise"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The piecewise curve has no model.</exception>
    public static InterpolatedAffineHazardRateCurve<TInterpolator> FromNodes<TBootstrap>(
        PiecewiseDefaultCurve<AffineHazardRate, TInterpolator, TBootstrap> piecewise)
        where TBootstrap : struct, IBootstrap
    {
        ArgumentNullException.ThrowIfNull(piecewise);
        OneFactorAffineModel model = piecewise.Model ?? throw new NanoQLException("the piecewise curve has no model");
        return new InterpolatedAffineHazardRateCurve<TInterpolator>(
            piecewise.Dates(), piecewise.Data(), piecewise.DayCounter, model, piecewise.Calendar, interpolator: piecewise.Interpolator);
    }

    /// <summary>Gets the nodes' times.</summary>
    /// <returns>The times.</returns>
    public ImmutableArray<double> Times() => curve.Times();

    /// <summary>Gets the nodes' dates.</summary>
    /// <returns>The dates.</returns>
    public ImmutableArray<Date> Dates() => dates;

    /// <summary>Gets the nodes' data, the deterministic hazard rates.</summary>
    /// <returns>The data.</returns>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>Gets the deterministic hazard rates at the nodes.</summary>
    /// <returns>The rates.</returns>
    public ImmutableArray<double> HazardRates() => curve.Data();

    /// <summary>Gets the nodes, (date, hazard rate).</summary>
    /// <returns>The nodes.</returns>
    public ImmutableArray<KeyValuePair<Date, double>> Nodes()
    {
        ImmutableArray<double> data = curve.Data();
        var nodes = ImmutableArray.CreateBuilder<KeyValuePair<Date, double>>(dates.Length);
        for (int i = 0; i < dates.Length; ++i)
        {
            nodes.Add(new KeyValuePair<Date, double>(dates[i], data[i]));
        }

        return nodes.MoveToImmutable();
    }

    /// <inheritdoc/>
    protected override double HazardRateImpl(double t) => AffineHazardRate.Rate(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);

    /// <inheritdoc/>
    internal override double? HazardRateSurvival(double t) =>
        GetType() == typeof(InterpolatedAffineHazardRateCurve<TInterpolator>)
            ? Math.Exp(-AffineHazardRate.Integral(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t))
            : null;

    /// <inheritdoc/>
    protected override double SurvivalProbabilityImpl(double t) =>
        AffineHazardRate.SurvivalProbabilityImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t, Model);

    /// <summary>The conditional survival: the deterministic rate's survival from the prior time times the model's bond
    /// price there at the realized intensity.</summary>
    /// <param name="tFwd">The prior time.</param>
    /// <param name="tTarget">The target time.</param>
    /// <param name="yVal">The intensity realized at the prior time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The prior time follows the target ("Probability time in the past.").</exception>
    protected override double ConditionalSurvivalProbabilityImpl(double tFwd, double tTarget, double yVal)
    {
        if (!(tFwd <= tTarget))
        {
            throw new NanoQLException("Probability time in the past.");
        }

        if (tFwd == 0.0)
        {
            return SurvivalProbabilityImpl(tTarget);
        }

        if (tFwd - tTarget == 0.0)
        {
            return 1.0;
        }

        ReadOnlySpan<double> times = curve.Times().AsSpan();
        ReadOnlySpan<double> data = curve.Data().AsSpan();
        double integralTFwd = AffineHazardRate.Integral(times, data, curve.Interpolation, tFwd);
        double integralTP = AffineHazardRate.Integral(times, data, curve.Interpolation, tTarget);
        return Math.Exp(-(integralTP - integralTFwd)) * Model.DiscountBond(tFwd, tTarget, yVal);
    }
}
