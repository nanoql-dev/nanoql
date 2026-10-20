// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/interpolateddefaultdensitycurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The default-density curve with linear interpolation: QuantLib-Python's <c>DefaultDensityCurve</c>, QuantLib's
/// <c>InterpolatedDefaultDensityCurve&lt;Linear&gt;</c>.
/// </summary>
public sealed class DefaultDensityCurve : InterpolatedDefaultDensityCurve<Linear>
{
    /// <summary>Initializes a new instance of the <see cref="DefaultDensityCurve"/> class.</summary>
    /// <param name="dates">The nodes' dates, sorted, the first one the reference date.</param>
    /// <param name="densities">The default densities at the nodes, one per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="densities"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">The inputs are refused as
    /// <see cref="InterpolatedDefaultDensityCurve{TInterpolator}"/> refuses them.</exception>
    public DefaultDensityCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> densities,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(dates, densities, dayCounter, calendar, jumps, jumpDates)
    {
    }
}
