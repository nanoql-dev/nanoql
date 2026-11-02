// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/zerocurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield term structure based on log-cubic interpolation of zero rates.</summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="InterpolatedZeroCurve{TInterpolator}"/> on
/// <see cref="LogCubic"/>. Its interpolator is QuantLib-Python's, <see cref="LogCubic"/> on Kruger's derivatives with
/// the monotonicity filter on, the name's only public form, so it takes none: QuantLib 1.42 deprecates the class
/// QuantLib-Python names, <c>DefaultLogCubic</c>, for <see cref="KrugerLog"/>, which leaves the filter off and gives
/// other numbers. QuantLib-Python deprecates the name, as zero curves should not use log interpolations;
/// <see cref="CubicZeroCurve"/> replaces it.
/// </remarks>
[Obsolete("QuantLib-Python deprecates LogCubicZeroCurve: zero curves should not use log interpolations. Use CubicZeroCurve.")]
public sealed class LogCubicZeroCurve : InterpolatedZeroCurve<LogCubic>
{
    // QuantLib-Python's interpolator: Kruger's derivatives, monotonic.
    private static readonly LogCubic MonotonicKrugerLogCubic = new(CubicInterpolationDerivativeApprox.Kruger);

    /// <summary>
    /// Initializes a new instance of the <see cref="LogCubicZeroCurve"/> class from its nodes; the first date is the
    /// reference date.
    /// </summary>
    /// <param name="dates">The nodes' dates, sorted.</param>
    /// <param name="yields">The zero rates at the nodes, with the given compounding and frequency.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="compounding">The rates' compounding.</param>
    /// <param name="frequency">
    /// The rates' compounding frequency, read only when the compounding isn't continuous, as QuantLib reads it.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="yields"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="compounding"/> isn't a member of its enum, or the compounding isn't continuous and
    /// <paramref name="frequency"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The nodes or the jumps are refused, as <see cref="InterpolatedZeroCurve{TInterpolator}"/> refuses them.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the nodes.</exception>
    public LogCubicZeroCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> yields,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        Compounding compounding = Compounding.Continuous,
        Frequency frequency = Frequency.Annual)
        : base(dates, yields, dayCounter, calendar, jumps, jumpDates, MonotonicKrugerLogCubic, compounding, frequency)
    {
    }
}
