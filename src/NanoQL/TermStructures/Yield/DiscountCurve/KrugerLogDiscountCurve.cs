// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/discountcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield term structure based on log-cubic interpolation of discount factors with Kruger's derivatives.</summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="InterpolatedDiscountCurve{TInterpolator}"/> on
/// <see cref="KrugerLog"/>: it adds constructors only.
/// </remarks>
public sealed class KrugerLogDiscountCurve : InterpolatedDiscountCurve<KrugerLog>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrugerLogDiscountCurve"/> class from its nodes; the first date is
    /// the reference date.
    /// </summary>
    /// <param name="dates">The nodes' dates, sorted.</param>
    /// <param name="dfs">The discount factors at the nodes, the first one 1.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="dfs"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The nodes or the jumps are refused, as <see cref="InterpolatedDiscountCurve{TInterpolator}"/> refuses them.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the nodes.</exception>
    public KrugerLogDiscountCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> dfs,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        KrugerLog interpolator = default)
        : base(dates, dfs, dayCounter, calendar, jumps, jumpDates, interpolator)
    {
    }
}
