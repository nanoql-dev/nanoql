// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/interpolatedsurvivalprobabilitycurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>A default-probability term structure interpolating survival probabilities.</summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// <para>
/// The first date is the reference date, where the probability is 1. Past the last node the probability decays at the
/// hazard rate there; the default density is minus the probability's derivative, and the hazard rate the base's. The
/// curve implements <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/> over <see cref="SurvivalProbability"/>,
/// as the piecewise survival-probability curve does, so the ISDA engine's curve test accepts it as QuantLib's cast
/// does.
/// </para>
/// <para>
/// QuantLib's protected constructors without nodes, through which its piecewise curve derives, aren't ported: the
/// piecewise curve implements the interface itself. QuantLib reads the first date before it checks how many there are,
/// so with no date it throws <c>std::out_of_range</c>; here an empty list is refused at that point with QuantLib's own
/// "not enough input dates given", the playbook's convention for such an exception. An extension point, for its named
/// class.
/// </para>
/// </remarks>
public class InterpolatedSurvivalProbabilityCurve<TInterpolator>
    : SurvivalProbabilityStructure, IInterpolatedCurve<SurvivalProbability, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedSurvivalProbabilityCurve{TInterpolator}"/>
    /// class.</summary>
    /// <param name="dates">The nodes' dates, sorted, the first one the reference date.</param>
    /// <param name="probabilities">The survival probabilities at the nodes, one per date, the first 1.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="probabilities"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer dates than the interpolator requires, a date is
    /// <c>default(Date)</c>, the probabilities aren't one per date, the first isn't 1, one after it isn't positive or
    /// rises above the one before it, the dates aren't sorted or two give the same time, the interpolation refuses the
    /// points, or the jumps' dates don't match the jumps.</exception>
    public InterpolatedSurvivalProbabilityCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> probabilities,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator interpolator = default)
        : this(
            InterpolatedCurve<TInterpolator>.Inputs(dates, probabilities, dayCounter, nameof(probabilities)),
            calendar,
            jumps,
            jumpDates,
            interpolator)
    {
    }

    private InterpolatedSurvivalProbabilityCurve(
        (ImmutableArray<Date> Dates, ImmutableArray<double> Data, DayCounter DayCounter) inputs,
        Calendar? calendar,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator interpolator)
        : base(inputs.Dates[0], calendar, inputs.DayCounter, null, jumps, jumpDates)
    {
        // initialize
        dates = inputs.Dates;
        ImmutableArray<double> data = inputs.Data;
        if (!(dates.Length >= TInterpolator.RequiredPoints))
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        if (!(data.Length == dates.Length))
        {
            throw new ArgumentException("dates/data count mismatch", "probabilities");
        }

        if (!(data[0] == 1.0))
        {
            throw new ArgumentException(
                "the first probability must be == 1.0 to flag the corresponding date as reference date",
                "probabilities");
        }

        double[] times = InterpolatedCurve<TInterpolator>.SetupTimes(dates.AsSpan(), dates[0], inputs.DayCounter);

        for (int i = 1; i < dates.Length; ++i)
        {
            if (!(data[i] > 0.0))
            {
                throw new ArgumentException("negative probability", "probabilities");
            }

            if (!(data[i] <= data[i - 1]))
            {
                throw new ArgumentException(
                    $"negative hazard rate implied by the survival probability {DataFormattersFunctions.General(data[i])} at "
                    + $"{dates[i]} (t={DataFormattersFunctions.General(times[i])}) after the survival probability "
                    + $"{DataFormattersFunctions.General(data[i - 1])} at {dates[i - 1]} "
                    + $"(t={DataFormattersFunctions.General(times[i - 1])})",
                    "probabilities");
            }
        }

        curve = InterpolatedCurve<TInterpolator>.FromTimes(times, data.AsSpan(), interpolator);
    }

    /// <inheritdoc/>
    public sealed override Date MaxDate => TermStructures.SurvivalProbability.MaxDate(dates.AsSpan());

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>The survival probabilities at the nodes.</summary>
    /// <returns>The probabilities.</returns>
    public ImmutableArray<double> SurvivalProbabilities() => curve.Data();

    /// <inheritdoc/>
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
    internal override double? HazardRateSurvival(double t) =>
        GetType() == typeof(InterpolatedSurvivalProbabilityCurve<TInterpolator>) || GetType() == typeof(SurvivalProbabilityCurve)
            ? DensityHazardRateSurvival(t)
            : null;

    /// <inheritdoc/>
    protected sealed override double SurvivalProbabilityImpl(double t) =>
        TermStructures.SurvivalProbability.SurvivalProbabilityImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);

    /// <inheritdoc/>
    protected sealed override double DefaultDensityImpl(double t) =>
        TermStructures.SurvivalProbability.DefaultDensityImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);
}
