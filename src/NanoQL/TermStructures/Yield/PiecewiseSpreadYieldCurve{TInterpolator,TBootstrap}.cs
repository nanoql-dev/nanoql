// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewisespreadyieldcurve.hpp ql/termstructures/yield/spreadbootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// A spread curve bootstrapped on rate helpers over a base curve: its nodes are discount factors whose product with
/// the base's discount reprices each helper.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator of the spread discount factors.</typeparam>
/// <typeparam name="TBootstrap">The bootstrap (<see cref="IterativeBootstrap"/>, <see cref="GlobalBootstrap"/>).</typeparam>
/// <remarks>
/// <para>
/// QuantLib's curve is <c>PiecewiseYieldCurve&lt;SpreadTraits&lt;Discount&gt;, I, B&gt;</c>, deriving from the spread
/// discount curve its traits name; <c>SpreadTraits&lt;Discount&gt;</c> is <see cref="Discount"/> with that curve type,
/// its guesses, bounds and iteration counts <see cref="Discount"/>'s. So this curve is the piecewise discount curve
/// on a base: its discount is the base's times the interpolated spread discount, and its maximum date the earlier of
/// its own and the base's (see <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/>); its reference
/// date, calendar, settlement days, day counter and extrapolation flag are the base's, read when it is built. It adds
/// QuantLib's constructor and <see cref="BaseCurve"/> only.
/// </para>
/// <para>
/// QuantLib's spread discount curve reads the base without extrapolation (<c>spreaddiscountcurve.hpp:157</c>), so a
/// bootstrap or a read past a base that doesn't extrapolate throws where the zero- and forward-spreaded curves
/// extrapolate; here the base is read with extrapolation on, as theirs is. A documented divergence.
/// </para>
/// </remarks>
public class PiecewiseSpreadYieldCurve<TInterpolator, TBootstrap> : PiecewiseYieldCurve<Discount, TInterpolator, TBootstrap>
    where TInterpolator : struct, IInterpolator
    where TBootstrap : struct, IBootstrap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseSpreadYieldCurve{TInterpolator, TBootstrap}"/> class, and
    /// bootstraps it.
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
    public PiecewiseSpreadYieldCurve(
        YieldTermStructure baseCurve,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        EvaluationContext context,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
        : base(baseCurve, instruments, context, i, bootstrap)
    {
    }

    /// <summary>Gets the base curve the spread is bootstrapped over.</summary>
    public YieldTermStructure BaseCurve => SpreadBase!;
}
