// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The binomial tree base: two branches from each node, node <c>index</c> of column <c>i</c> going to nodes
/// <c>index</c> and <c>index + 1</c>.</summary>
/// <typeparam name="T">The derived tree, QuantLib's curiously recurring parameter.</typeparam>
/// <remarks>Immutable once built.</remarks>
public abstract class BinomialTree<T> : Tree
    where T : BinomialTree<T>
{
    /// <summary>Initializes a new instance of the <see cref="BinomialTree{T}"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    protected BinomialTree(StochasticProcess1D process, double end, int steps)
        : base(ColumnsFor(steps))
    {
        ArgumentNullException.ThrowIfNull(process);
        X0 = process.X0;
        Dt = end / steps;
        DriftPerStep = process.Drift(0.0, X0) * Dt;
    }

    /// <inheritdoc/>
    public override int Branches => 2;

    /// <summary>Gets the underlying's value at time 0.</summary>
    protected double X0 { get; }

    /// <summary>Gets the drift over one step.</summary>
    protected double DriftPerStep { get; }

    /// <summary>Gets the step's length.</summary>
    protected double Dt { get; }

    /// <inheritdoc/>
    public override int Size(int i) => i + 1;

    /// <inheritdoc/>
    public override int Descendant(int i, int index, int branch) => index + branch;

    private static int ColumnsFor(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        return steps + 1;
    }
}
