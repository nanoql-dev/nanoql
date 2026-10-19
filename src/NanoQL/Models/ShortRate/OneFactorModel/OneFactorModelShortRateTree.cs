// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodel.hpp ql/models/shortrate/onefactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor fitting the tree to a term structure numerically, with its solver helper, which waits for batch 3's extended Cox-Ingersoll-Ross model, its only user

using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>A recombining trinomial tree discretizing a one-factor model's state variable.</summary>
/// <remarks>
/// <para>
/// QuantLib's <c>OneFactorModel::ShortRateTree</c>. Immutable once built: <see cref="SetSpread"/> returns a copy, which
/// shares the trinomial tree, the dynamics and the state prices this tree stored when it was built.
/// </para>
/// <para>
/// Hull-White's fitted tree stores each step's state prices as it fits the dynamics to the curve, before the tree is
/// returned; the tree is sealed, so the members its state prices read are its own.
/// </para>
/// </remarks>
public sealed class OneFactorModelShortRateTree : TreeLattice1D
{
    private readonly TrinomialTree tree;
    private readonly OneFactorModelShortRateDynamics dynamics;
    private readonly double spread;

    /// <summary>Initializes a new instance of the <see cref="OneFactorModelShortRateTree"/> class: the plain tree build-up from short-rate dynamics.</summary>
    /// <param name="tree">The trinomial tree of the state variable.</param>
    /// <param name="dynamics">The short-rate dynamics.</param>
    /// <param name="timeGrid">The time grid.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OneFactorModelShortRateTree(TrinomialTree tree, OneFactorModelShortRateDynamics dynamics, TimeGrid timeGrid)
        : base(timeGrid, BranchesOf(tree))
    {
        ArgumentNullException.ThrowIfNull(dynamics);
        this.tree = tree;
        this.dynamics = dynamics;
        spread = 0.0;
    }

    private OneFactorModelShortRateTree(OneFactorModelShortRateTree other, double spread)
        : base(other)
    {
        tree = other.tree;
        dynamics = other.dynamics;
        this.spread = spread;
    }

    /// <summary>The number of nodes at a step.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <returns>The number of nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a column of the tree.</exception>
    public override int Size(int i) => tree.Size(i);

    /// <summary>The discount factor over the step that starts at a node: exp(−r·dt), r the node's short rate plus the spread.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <returns>The discount factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public override double Discount(int i, int index)
    {
        double x = tree.Underlying(i, index);
        double r = dynamics.ShortRate(TimeGrid[i], x) + spread;
        return Math.Exp(-r * TimeGrid.Dt(i));
    }

    /// <summary>The state variable's value at a node.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a column of the tree, or <paramref name="index"/> is negative.</exception>
    public override double Underlying(int i, int index) => tree.Underlying(i, index);

    /// <summary>The index, at the next step, of a node's descendant along a branch.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The descendant's index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> or <paramref name="index"/> is out of range, or <paramref name="branch"/> is negative.
    /// </exception>
    public override int Descendant(int i, int index, int branch) => tree.Descendant(i, index, branch);

    /// <summary>The probability of a node's branch.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public override double Probability(int i, int index, int branch) => tree.Probability(i, index, branch);

    /// <summary>A copy of the tree whose short rates carry a spread: QuantLib's <c>setSpread</c>.</summary>
    /// <param name="spread">The spread added to every node's short rate.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    public OneFactorModelShortRateTree SetSpread(double spread) => new(this, spread);

    private static int BranchesOf(TrinomialTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        // QuantLib's tree->size(1): the second column's nodes, 3 for every trinomial tree
        return tree.Size(1);
    }
}
