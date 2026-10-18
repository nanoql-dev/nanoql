// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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
/// An extension point: each family of term structures has its own traits.
/// </para>
/// </remarks>
public interface IBootstrapTraits<TTermStructure>
    where TTermStructure : TermStructure
{
    /// <summary>Gets the upper bound of the bootstrap's convergence loop.</summary>
    static abstract int MaxIterations { get; }

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
}
