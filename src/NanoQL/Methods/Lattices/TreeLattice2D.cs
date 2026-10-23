// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/lattice2d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>A two-dimensional tree-based lattice: two trinomial trees, correlated, primarily for the G2 short-rate model.</summary>
/// <remarks>
/// <para>
/// A node at step i is the pair of the two trees' nodes, numbered index₁ + index₂·n₁(i), n₁ the first tree's size at
/// the step; a branch is the pair of the trees' branches, numbered branch₁ + branch₂·3. A branch's probability is the
/// product of the trees', corrected for the correlation ρ by |ρ|·M[branch₁, branch₂]/36, QuantLib's matrix M
/// mirrored where ρ is negative.
/// </para>
/// <para>
/// A derived class implements <see cref="TreeLattice.Discount"/>. QuantLib's <c>grid</c> isn't implemented, and
/// <see cref="Grid(double)"/> refuses with QuantLib's text. An extension point, as in QuantLib: the two-factor model's
/// short-rate tree derives from it.
/// </para>
/// </remarks>
public abstract class TreeLattice2D : TreeLattice
{
    private readonly double[,] m;
    private readonly double rho;

    /// <summary>Initializes a new instance of the <see cref="TreeLattice2D"/> class.</summary>
    /// <param name="tree1">The first tree.</param>
    /// <param name="tree2">The second tree, on the first's time grid.</param>
    /// <param name="correlation">The correlation between the two trees' variables.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tree1"/> or <paramref name="tree2"/> is null.</exception>
    protected TreeLattice2D(TrinomialTree tree1, TrinomialTree tree2, double correlation)
        : base(GridOf(tree1), tree1.Branches * tree1.Branches)
    {
        ArgumentNullException.ThrowIfNull(tree2);
        Tree1 = tree1;
        Tree2 = tree2;
        rho = Math.Abs(correlation);

        // what happens here?
        if (correlation < 0.0 && tree1.Branches == 3)
        {
            m = new double[,] { { -1.0, -4.0, 5.0 }, { -4.0, 8.0, -4.0 }, { 5.0, -4.0, -1.0 } };
        }
        else
        {
            m = new double[,] { { 5.0, -4.0, -1.0 }, { -4.0, 8.0, -4.0 }, { -1.0, -4.0, 5.0 } };
        }
    }

    /// <summary>Gets the first tree.</summary>
    protected TrinomialTree Tree1 { get; }

    /// <summary>Gets the second tree.</summary>
    protected TrinomialTree Tree2 { get; }

    /// <summary>The number of nodes at a step: the product of the two trees' sizes.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <returns>The number of nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a column of the trees.</exception>
    public override int Size(int i) => Tree1.Size(i) * Tree2.Size(i);

    /// <summary>The index, at the next step, of a node's descendant along a branch.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The descendant's index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public override int Descendant(int i, int index, int branch)
    {
        int modulo = Tree1.Size(i);

        int index1 = index % modulo;
        int index2 = index / modulo;
        int branch1 = branch % Tree1.Branches;
        int branch2 = branch / Tree1.Branches;

        modulo = Tree1.Size(i + 1);
        return Tree1.Descendant(i, index1, branch1) + (Tree2.Descendant(i, index2, branch2) * modulo);
    }

    /// <summary>The probability of a node's branch: the two trees' product, corrected for the correlation.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public override double Probability(int i, int index, int branch)
    {
        int modulo = Tree1.Size(i);

        int index1 = index % modulo;
        int index2 = index / modulo;
        int branch1 = branch % Tree1.Branches;
        int branch2 = branch / Tree1.Branches;

        double prob1 = Tree1.Probability(i, index1, branch1);
        double prob2 = Tree2.Probability(i, index2, branch2);

        // does the 36 below depend on T::branches?
        return (prob1 * prob2) + (rho * m[branch1, branch2] / 36.0);
    }

    /// <summary>Refuses: QuantLib's two-dimensional lattice doesn't implement its grid.</summary>
    /// <param name="t">The time.</param>
    /// <returns>Nothing: it always throws.</returns>
    /// <exception cref="NanoQLException">Always: "not implemented".</exception>
    public override Vector Grid(double t) => throw new NanoQLException("not implemented");

    private static TimeGrid GridOf(TrinomialTree tree1)
    {
        ArgumentNullException.ThrowIfNull(tree1);
        return tree1.TimeGrid;
    }
}
