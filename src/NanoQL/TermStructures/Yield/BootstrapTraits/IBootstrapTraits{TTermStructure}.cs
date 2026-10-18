// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A bootstrap's traits: what the bootstraps ask of a curve kind (QuantLib's <c>Discount</c>, <c>ZeroYield</c> and the
/// other traits structs), as static members.
/// </summary>
/// <typeparam name="TTermStructure">The kind of term structure bootstrapped.</typeparam>
/// <remarks>
/// <para>
/// Traits are stateless, so a traits type is a <see langword="readonly"/> struct with static members only, used as a
/// type argument. Each member reads the curve under construction through its working state, as QuantLib's read the curve
/// through their pointer to it.
/// </para>
/// <para>
/// QuantLib's global bootstrap detects two optional groups of members (<c>globalGuess</c>, and <c>transformDirect</c>
/// with <c>transformInverse</c>); here they are static virtual members whose defaults are QuantLib's behaviour when the
/// traits don't define them, and <see cref="HasGlobalGuess"/> and <see cref="HasTransform"/> say whether a traits type
/// does. An extension point: each family of term structures has its own traits.
/// </para>
/// </remarks>
public interface IBootstrapTraits<TTermStructure>
    where TTermStructure : TermStructure
{
    /// <summary>Gets the upper bound of the bootstrap's convergence loop.</summary>
    static abstract int MaxIterations { get; }

    /// <summary>Gets a value indicating whether the traits give a guess for the whole curve (<see cref="GlobalGuess"/>).</summary>
    static virtual bool HasGlobalGuess => false;

    /// <summary>
    /// Gets a value indicating whether the traits transform a node's value to remove its constraints from an
    /// unconstrained optimization (<see cref="TransformDirect"/> and <see cref="TransformInverse"/>).
    /// </summary>
    static virtual bool HasTransform => false;

    /// <summary>The curve's first date: the start of its data.</summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The date.</returns>
    static abstract Date InitialDate(BootstrapState<TTermStructure> c);

    /// <summary>The curve's value at its first date.</summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The value.</returns>
    static abstract double InitialValue(BootstrapState<TTermStructure> c);

    /// <summary>The guess for a node's value.</summary>
    /// <param name="i">The node.</param>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <param name="validData">Whether the curve's data hold a previous pass's values.</param>
    /// <param name="firstAliveHelper">The index of the first helper alive at the curve's first date.</param>
    /// <returns>The guess.</returns>
    static abstract double Guess(int i, BootstrapState<TTermStructure> c, bool validData, int firstAliveHelper);

    /// <summary>The lower bound of a node's value, from the previous values.</summary>
    /// <param name="i">The node.</param>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <param name="validData">Whether the curve's data hold a previous pass's values.</param>
    /// <param name="firstAliveHelper">The index of the first helper alive at the curve's first date.</param>
    /// <returns>The bound.</returns>
    static abstract double MinValueAfter(int i, BootstrapState<TTermStructure> c, bool validData, int firstAliveHelper);

    /// <summary>The upper bound of a node's value, from the previous values.</summary>
    /// <param name="i">The node.</param>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <param name="validData">Whether the curve's data hold a previous pass's values.</param>
    /// <param name="firstAliveHelper">The index of the first helper alive at the curve's first date.</param>
    /// <returns>The bound.</returns>
    static abstract double MaxValueAfter(int i, BootstrapState<TTermStructure> c, bool validData, int firstAliveHelper);

    /// <summary>Writes a root finder's trial value into the curve's data: QuantLib's <c>updateGuess</c>.</summary>
    /// <param name="data">The curve's data.</param>
    /// <param name="value">The value.</param>
    /// <param name="i">The node.</param>
    static abstract void UpdateGuess(double[] data, double value, int i);

    /// <summary>
    /// The guess for the whole curve, every node but the first, and whether it needs to be transformed. Read only when
    /// <see cref="HasGlobalGuess"/> is set; the default throws.
    /// </summary>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <param name="validData">Whether the curve's data hold a previous state's values.</param>
    /// <returns>The guess, and whether <see cref="TransformInverse"/> must be applied to it.</returns>
    /// <exception cref="NotSupportedException">The traits give no global guess.</exception>
    static virtual (Vector Guess, bool NeedsTransform) GlobalGuess(BootstrapState<TTermStructure> c, bool validData) =>
        throw new NotSupportedException("the traits give no global guess");

    /// <summary>A node's value from its unconstrained counterpart; the default is the identity.</summary>
    /// <param name="x">The unconstrained value.</param>
    /// <param name="i">The node.</param>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The node's value.</returns>
    static virtual double TransformDirect(double x, int i, BootstrapState<TTermStructure> c) => x;

    /// <summary>A node's unconstrained counterpart from its value; the default is the identity.</summary>
    /// <param name="x">The node's value.</param>
    /// <param name="i">The node.</param>
    /// <param name="c">The working state of the curve under construction.</param>
    /// <returns>The unconstrained value.</returns>
    static virtual double TransformInverse(double x, int i, BootstrapState<TTermStructure> c) => x;
}
