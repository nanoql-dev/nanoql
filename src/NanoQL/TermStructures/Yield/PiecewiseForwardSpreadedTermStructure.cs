// SPDX-FileCopyrightText: Copyright (C) 2006 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008, 2014 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Robert Philipp
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewiseforwardspreadedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield curve with spreads added to another curve's instantaneous forward rates, backward-flat between
/// their dates.</summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for
/// <see cref="InterpolatedPiecewiseForwardSpreadedTermStructure{TInterpolator}"/> on <see cref="BackwardFlat"/>: it
/// adds constructors only.
/// </remarks>
public sealed class PiecewiseForwardSpreadedTermStructure : InterpolatedPiecewiseForwardSpreadedTermStructure<BackwardFlat>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseForwardSpreadedTermStructure"/> class.
    /// </summary>
    /// <param name="originalCurve">The curve the spreads are added to.</param>
    /// <param name="spreads">The spreads on the instantaneous forward rate, one per date.</param>
    /// <param name="dates">The spreads' dates, sorted.</param>
    /// <param name="factory">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalCurve"/>, <paramref name="spreads"/> or <paramref name="dates"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// No spread is given, the spreads and the dates differ in number, a date is <c>default(Date)</c>, or the
    /// interpolation refuses the spreads.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the spreads.</exception>
    public PiecewiseForwardSpreadedTermStructure(
        YieldTermStructure originalCurve,
        IEnumerable<double> spreads,
        IEnumerable<Date> dates,
        BackwardFlat factory = default)
        : base(originalCurve, spreads, dates, factory)
    {
    }
}
