// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/forwardcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield term structure based on forward-flat interpolation of instantaneous forward rates.</summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="InterpolatedForwardCurve{TInterpolator}"/> on
/// <see cref="ForwardFlat"/>: it adds constructors only.
/// </remarks>
public sealed class ForwardFlatForwardCurve : InterpolatedForwardCurve<ForwardFlat>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardFlatForwardCurve"/> class from its nodes; the first date is
    /// the reference date.
    /// </summary>
    /// <param name="dates">The nodes' dates, sorted.</param>
    /// <param name="forwards">The instantaneous forward rates at the nodes.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="forwards"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The nodes or the jumps are refused, as <see cref="InterpolatedForwardCurve{TInterpolator}"/> refuses them.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the nodes.</exception>
    public ForwardFlatForwardCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> forwards,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        ForwardFlat interpolator = default)
        : base(dates, forwards, dayCounter, calendar, jumps, jumpDates, interpolator)
    {
    }
}
