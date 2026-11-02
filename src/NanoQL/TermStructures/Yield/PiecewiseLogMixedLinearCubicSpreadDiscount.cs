// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewisespreadyieldcurve.hpp ql/termstructures/yield/spreadbootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// A spread discount curve over a base curve, bootstrapped iteratively on log-mixed-linear-cubic interpolation of its
/// spread discount factors: QuantLib-Python's <c>PiecewiseLogMixedLinearCubicSpreadDiscount</c>.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="PiecewiseSpreadYieldCurve{TInterpolator, TBootstrap}"/> on
/// <see cref="LogMixedLinearCubic"/> and <see cref="IterativeBootstrap"/>: it adds constructors only.
/// QuantLib's <see cref="Numerics.LogMixedLinearCubic"/> has no default construction: a curve built without an
/// interpolator refuses its zero value with <see cref="InvalidOperationException"/>, as the generic's does.
/// </remarks>
public sealed class PiecewiseLogMixedLinearCubicSpreadDiscount : PiecewiseSpreadYieldCurve<LogMixedLinearCubic, IterativeBootstrap>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseLogMixedLinearCubicSpreadDiscount"/> class, and bootstraps
    /// it.
    /// </summary>
    /// <param name="baseCurve">The base curve the spread is bootstrapped over.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="baseCurve"/>, <paramref name="instruments"/> or <paramref name="context"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">A helper is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    /// <exception cref="InvalidOperationException">The interpolator is its zero value.</exception>
    public PiecewiseLogMixedLinearCubicSpreadDiscount(
        YieldTermStructure baseCurve,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        EvaluationContext context,
        LogMixedLinearCubic i = default,
        IterativeBootstrap bootstrap = default)
        : base(baseCurve, instruments, context, i, bootstrap)
    {
    }
}
