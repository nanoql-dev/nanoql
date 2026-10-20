// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/interpolatedyoyinflationcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A year-on-year inflation curve interpolating its rates linearly: QuantLib's typedef
/// <c>InterpolatedYoYInflationCurve&lt;Linear&gt;</c>, named as QuantLib-Python names it.
/// </summary>
public sealed class YoYInflationCurve : InterpolatedYoYInflationCurve<Linear>
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationCurve"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The nodes' dates, sorted, the first one the base date.</param>
    /// <param name="rates">The year-on-year inflation rates at the nodes, one per date, the first one the base
    /// rate.</param>
    /// <param name="frequency">The frequency of the index the curve forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="rates"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">The base class's checks refuse the nodes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the curve.</exception>
    public YoYInflationCurve(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> rates,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality = null,
        Linear interpolator = default)
        : base(referenceDate, dates, rates, frequency, dayCounter, seasonality, interpolator)
    {
    }
}
