// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/spreaddiscountcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A spread yield curve based on log-linear interpolation of spread discount factors, which gives piecewise-constant
/// spreads.
/// </summary>
/// <remarks>
/// QuantLib's typedef and QuantLib-Python's <c>%template</c> name for
/// <see cref="InterpolatedSpreadDiscountCurve{TInterpolator}"/> on <see cref="LogLinear"/>: it adds constructors only.
/// </remarks>
public sealed class SpreadDiscountCurve : InterpolatedSpreadDiscountCurve<LogLinear>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadDiscountCurve"/> class from its nodes; the first date is the
    /// base's reference date.
    /// </summary>
    /// <param name="baseCurve">The base curve.</param>
    /// <param name="dates">The nodes' dates, sorted, the first the base's reference date.</param>
    /// <param name="dfs">The spread discount factors at the nodes, the first one 1.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="baseCurve"/>, <paramref name="dates"/> or <paramref name="dfs"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The nodes are refused, as <see cref="InterpolatedSpreadDiscountCurve{TInterpolator}"/> refuses them.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the nodes.</exception>
    public SpreadDiscountCurve(
        YieldTermStructure baseCurve, IEnumerable<Date> dates, IEnumerable<double> dfs, LogLinear interpolator = default)
        : base(baseCurve, dates, dfs, interpolator)
    {
    }
}
