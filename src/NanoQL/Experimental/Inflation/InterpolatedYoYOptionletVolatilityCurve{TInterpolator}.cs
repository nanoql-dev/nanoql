// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoyinflationoptionletvolatilitystructure2.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A year-on-year optionlet volatility interpolated in time and flat in strike: QuantLib's
/// <c>InterpolatedYoYOptionletVolatilityCurve</c>.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator in time.</typeparam>
/// <remarks>
/// <para>
/// The dates are the volatilities' own, with no lag, measured from the reference date, so the first is usually the
/// base date, before it. The base level is the interpolation's value at the base date, extrapolated. A volatility past
/// the last date or before the first refuses, as QuantLib's interpolation, which doesn't enable extrapolation. The
/// last date is the reference date plus the last time rounded up to whole years, adjusted, QuantLib's own
/// approximation (its <c>FIXME</c>).
/// </para>
/// <para>
/// QuantLib's protected constructor without nodes, through which its piecewise curve derives, isn't ported:
/// <see cref="PiecewiseYoYOptionletVolatilityCurve{TInterpolator, TBootstrap, TTraits}"/> implements
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/> itself, as S-infl's piecewise year-on-year curve does. Its
/// nodes come from its dates and data, QuantLib's master's (#2941), where 1.44's are a list only this constructor
/// fills.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class InterpolatedYoYOptionletVolatilityCurve<TInterpolator>
    : YoYOptionletVolatilitySurface, IInterpolatedCurve<YoYInflationVolatilityTraits, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;
    private readonly double minStrike;
    private readonly double maxStrike;

    private InterpolatedYoYOptionletVolatilityCurve(
        Date referenceDate,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period lag,
        Frequency frequency,
        bool indexIsInterpolated,
        ImmutableArray<Date> d,
        ImmutableArray<double> v,
        double minStrike,
        double maxStrike,
        TInterpolator i)
        : base(referenceDate, settlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated)
    {
        if (d.Length != v.Length)
        {
            throw new NanoQLException($"must have same number of dates and vols: {d.Length} vs {v.Length}");
        }

        if (d.Length <= 1)
        {
            throw new NanoQLException($"must have at least two dates: {d.Length}");
        }

        dates = d;
        this.minStrike = minStrike;
        this.maxStrike = maxStrike;
        double[] times = [.. d.Select(TimeFromReference)];
        curve = InterpolatedCurve<TInterpolator>.FromTimes(times, v.AsSpan(), i);

        // set the base vol level to that predicted by the interpolation
        // this is allowed by the extrapolation
        double baseTime = TimeFromReference(BaseDate());
        SetBaseLevel(curve.Interpolation.Evaluate(baseTime, true));
    }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Interpolator => curve.Interpolator;

    /// <inheritdoc/>
    /// <remarks>The reference date plus the last time rounded up to whole years, as an option tenor.</remarks>
    public override Date MaxDate =>
        OptionDateFromTenor(new Period((int)Math.Ceiling(curve.Interpolation.XMax), TimeUnit.Years));

    /// <summary>
    /// Builds the curve on the reference date the settlement days give after the evaluation date.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="lag">The observation lag.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="indexIsInterpolated">Whether the index is observed with linear interpolation.</param>
    /// <param name="d">The volatilities' dates, increasing.</param>
    /// <param name="v">The volatilities, one per date.</param>
    /// <param name="minStrike">The lowest strike the curve gives volatilities for.</param>
    /// <param name="maxStrike">The highest strike the curve gives volatilities for.</param>
    /// <param name="context">The evaluation context, whose evaluation date the reference date follows.</param>
    /// <param name="i">The interpolator.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The dates and volatilities differ in number or are fewer than two, with
    /// QuantLib's texts, or the interpolation refuses them.</exception>
    public static InterpolatedYoYOptionletVolatilityCurve<TInterpolator> Create(
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period lag,
        Frequency frequency,
        bool indexIsInterpolated,
        IEnumerable<Date> d,
        IEnumerable<double> v,
        double minStrike,
        double maxStrike,
        EvaluationContext context,
        TInterpolator i = default)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(v);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new InterpolatedYoYOptionletVolatilityCurve<TInterpolator>(
            referenceDate, settlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated, [.. d], [.. v], minStrike,
            maxStrike, i);
    }

    /// <inheritdoc/>
    public override double MinStrike() => minStrike;

    /// <inheritdoc/>
    public override double MaxStrike() => maxStrike;

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

    /// <inheritdoc/>
    /// <remarks>From the dates and data, QuantLib's master's (#2941).</remarks>
    public ImmutableArray<KeyValuePair<Date, double>> Nodes()
    {
        ImmutableArray<double> data = curve.Data();
        var nodes = ImmutableArray.CreateBuilder<KeyValuePair<Date, double>>(dates.Length);
        for (int k = 0; k < dates.Length; ++k)
        {
            nodes.Add(new KeyValuePair<Date, double>(dates[k], data[k]));
        }

        return nodes.MoveToImmutable();
    }

    /// <inheritdoc/>
    /// <remarks>The interpolation's value at the time, whatever the strike: the smile is flat.</remarks>
    protected override double VolatilityImpl(double length, double strike) => curve.Interpolation.Evaluate(length);
}
