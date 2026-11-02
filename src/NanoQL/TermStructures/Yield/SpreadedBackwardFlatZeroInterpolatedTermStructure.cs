// SPDX-FileCopyrightText: Copyright (C) 2006 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008, 2014 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Robert Philipp
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewisezerospreadedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield curve with spreads added to another curve's zero rates, backward-flat between their
/// dates.</summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for
/// <see cref="InterpolatedPiecewiseZeroSpreadedTermStructure{TInterpolator}"/> on <see cref="BackwardFlat"/>: it adds
/// constructors only.
/// </remarks>
public sealed class SpreadedBackwardFlatZeroInterpolatedTermStructure : InterpolatedPiecewiseZeroSpreadedTermStructure<BackwardFlat>
{
    /// <summary>Initializes a new instance of the <see cref="SpreadedBackwardFlatZeroInterpolatedTermStructure"/>
    /// class.</summary>
    /// <param name="originalCurve">The curve the spreads are added to.</param>
    /// <param name="spreads">The spreads, one per date.</param>
    /// <param name="dates">The spreads' dates, sorted.</param>
    /// <param name="comp">The compounding the spreads are added in.</param>
    /// <param name="freq">The frequency the spreads are added in, which a compounded rule needs.</param>
    /// <param name="factory">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalCurve"/>, <paramref name="spreads"/> or <paramref name="dates"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException">
    /// The spreads or their dates are refused, as
    /// <see cref="InterpolatedPiecewiseZeroSpreadedTermStructure{TInterpolator}"/> refuses them.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the spreads.</exception>
    public SpreadedBackwardFlatZeroInterpolatedTermStructure(
        YieldTermStructure originalCurve,
        IEnumerable<double> spreads,
        IEnumerable<Date> dates,
        Compounding comp = Compounding.Continuous,
        Frequency freq = Frequency.NoFrequency,
        BackwardFlat factory = default)
        : base(originalCurve, spreads, dates, comp, freq, factory)
    {
    }
}
