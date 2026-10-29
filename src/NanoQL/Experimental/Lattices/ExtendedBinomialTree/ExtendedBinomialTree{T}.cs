// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 John Maiden
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/lattices/extendedbinomialtree.hpp ql/experimental/lattices/extendedbinomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The time-dependent binomial tree base: two branches from each node, node <c>index</c> of column <c>i</c> going to
/// nodes <c>index</c> and <c>index + 1</c>, its moves read from the process at each step's time: QuantLib's
/// <c>ExtendedBinomialTree</c>.
/// </summary>
/// <typeparam name="T">The derived tree, QuantLib's curiously recurring parameter.</typeparam>
/// <remarks>
/// <para>
/// Immutable once built, on its process, which it keeps. A node at step i is built from the process's drift and
/// volatility at tᵢ, read as if they held for the whole history to that step, so the tree's spread at step i is that of
/// a constant volatility σ(tᵢ), not the integrated one. On constant parameters the tree is exact, and every QuantLib
/// engine flattens its process to constant parameters before it builds a tree (<see cref="PricingEngines.BinomialVanillaEngine"/>),
/// so through an engine the extended trees equal the standard ones; on a tree built directly on a time-dependent
/// process the per-step reads are an approximation that doesn't converge to the time-dependent price. QuantLib's
/// design, kept (the S-xrt plan's XRR6, ruled 2026-10-28; <c>Conventions/ExtendedTreeTimeDependenceTests.cs</c>).
/// </para>
/// <para>
/// An extension point, as in QuantLib: the equal-probabilities and equal-jumps bases and the Tian, Leisen-Reimer and
/// Joshi trees derive from it.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class ExtendedBinomialTree<T> : Tree
    where T : ExtendedBinomialTree<T>
{
    /// <summary>Initializes a new instance of the <see cref="ExtendedBinomialTree{T}"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    protected ExtendedBinomialTree(StochasticProcess1D process, double end, int steps)
        : base(ColumnsFor(steps))
    {
        ArgumentNullException.ThrowIfNull(process);
        X0 = process.X0;
        Dt = end / steps;
        TreeProcess = process;
    }

    /// <inheritdoc/>
    public override int Branches => 2;

    /// <summary>Gets the underlying's value at time 0.</summary>
    protected double X0 { get; }

    /// <summary>Gets the step's length.</summary>
    protected double Dt { get; }

    /// <summary>Gets the process the tree reads at each step's time.</summary>
    protected StochasticProcess1D TreeProcess { get; }

    /// <inheritdoc/>
    public override int Size(int i) => i + 1;

    /// <inheritdoc/>
    public override int Descendant(int i, int index, int branch) => index + branch;

    /// <summary>The drift over one step at a time: QuantLib's <c>driftStep</c>.</summary>
    /// <param name="driftTime">The time.</param>
    /// <returns>The process's drift at that time and the initial value, times the step.</returns>
    protected double DriftStep(double driftTime) => TreeProcess.Drift(driftTime, X0) * Dt;

    private static int ColumnsFor(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        return steps + 1;
    }
}
