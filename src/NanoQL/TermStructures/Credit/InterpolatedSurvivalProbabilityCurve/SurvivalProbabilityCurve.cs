// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/interpolatedsurvivalprobabilitycurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The survival-probability curve with linear interpolation: QuantLib-Python's <c>SurvivalProbabilityCurve</c>,
/// QuantLib's <c>InterpolatedSurvivalProbabilityCurve&lt;Linear&gt;</c>.
/// </summary>
public sealed class SurvivalProbabilityCurve : InterpolatedSurvivalProbabilityCurve<Linear>
{
    /// <summary>Initializes a new instance of the <see cref="SurvivalProbabilityCurve"/> class.</summary>
    /// <param name="dates">The nodes' dates, sorted, the first one the reference date.</param>
    /// <param name="probabilities">The survival probabilities at the nodes, one per date, the first 1.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="probabilities"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">The inputs are refused as
    /// <see cref="InterpolatedSurvivalProbabilityCurve{TInterpolator}"/> refuses them.</exception>
    public SurvivalProbabilityCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> probabilities,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(dates, probabilities, dayCounter, calendar, jumps, jumpDates)
    {
    }
}
