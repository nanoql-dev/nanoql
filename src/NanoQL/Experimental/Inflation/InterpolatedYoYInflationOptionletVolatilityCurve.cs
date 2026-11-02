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
/// The year-on-year optionlet volatility curve on linear interpolation: QuantLib-Python's
/// <c>InterpolatedYoYInflationOptionletVolatilityCurve</c>.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="InterpolatedYoYOptionletVolatilityCurve{TInterpolator}"/> on
/// <see cref="Linear"/>: its own <see cref="Create"/> returns it.
/// </remarks>
[Experimental("NQL0001")]
public sealed class InterpolatedYoYInflationOptionletVolatilityCurve : InterpolatedYoYOptionletVolatilityCurve<Linear>
{
    private InterpolatedYoYInflationOptionletVolatilityCurve(
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
        Linear i)
        : base(referenceDate, settlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated, d, v, minStrike, maxStrike, i)
    {
    }

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
    /// <returns>The curve, of this class.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The dates and volatilities differ in number or are fewer than two, with
    /// QuantLib's texts, or the interpolation refuses them.</exception>
    public static new InterpolatedYoYInflationOptionletVolatilityCurve Create(
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
        Linear i = default)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(v);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new InterpolatedYoYInflationOptionletVolatilityCurve(
            referenceDate, settlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated, [.. d], [.. v], minStrike,
            maxStrike, i);
    }
}
