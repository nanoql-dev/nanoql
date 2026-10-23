// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodel.hpp ql/models/shortrate/onefactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>A recombining trinomial tree discretizing a one-factor model's state variable.</summary>
/// <remarks>
/// <para>
/// QuantLib's <c>OneFactorModel::ShortRateTree</c>. Immutable once built: <see cref="SetSpread"/> returns a copy, which
/// shares the trinomial tree, the dynamics and the state prices this tree stored when it was built.
/// </para>
/// <para>
/// Hull-White's fitted tree stores each step's state prices as it fits the dynamics to the curve, before the tree is
/// returned, as does the constructor that fits a term-structure fitting parameter numerically; the tree is sealed, so
/// the members its state prices read are its own.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="OneFactorModelShortRateTree"/> class: the tree build-up with the
    /// numerical fitting of a parameter to the parameter's curve, one value per step, each the root, by
    /// <see cref="Brent"/> (1,000 evaluations, accuracy 1e-7, in [−100, 100] from the previous step's value), at which
    /// the step's state prices discount to the curve's discount at the next time.
    /// </summary>
    /// <param name="tree">The trinomial tree of the state variable.</param>
    /// <param name="dynamics">The short-rate dynamics, whose short rate reads the fitting parameter.</param>
    /// <param name="theta">The fitting parameter's numerical values, which the constructor fills.</param>
    /// <param name="timeGrid">The time grid.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">A step's value can't be solved within the bracket.</exception>
    internal OneFactorModelShortRateTree(
        TrinomialTree tree,
        OneFactorModelShortRateDynamics dynamics,
        TermStructureFittingParameter.NumericalImpl theta,
        TimeGrid timeGrid)
        : this(tree, dynamics, timeGrid)
    {
        ArgumentNullException.ThrowIfNull(theta);
        theta.Reset();
        double value = 1.0;
        double vMin = -100.0;
        double vMax = 100.0;
        for (int i = 0; i < timeGrid.Size - 1; i++)
        {
            double discountBond = theta.TermStructure.Discount(TimeGrid[i + 1]);
            var finder = new Helper(i, discountBond, theta, this);
            Brent s1d = new Brent().SetMaxEvaluations(1000);
            value = s1d.Solve(finder, 1e-7, value, vMin, vMax);
            // vMin = value - 1.0;
            // vMax = value + 1.0;
            theta.Change(value);
        }
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

    // The function the fitting solves at a step: the curve's discount less the step's state prices, each discounted on
    // the node's short rate at the trial value. Its construction sets the step's value to 0, as QuantLib's does, and
    // stores the step's state prices.
    private readonly struct Helper : ISolver1DFunction
    {
        private readonly int size;
        private readonly int i;
        private readonly ImmutableArray<double> statePrices;
        private readonly double discountBondPrice;
        private readonly TermStructureFittingParameter.NumericalImpl theta;
        private readonly OneFactorModelShortRateTree tree;

        public Helper(int i, double discountBondPrice, TermStructureFittingParameter.NumericalImpl theta, OneFactorModelShortRateTree tree)
        {
            size = tree.Size(i);
            this.i = i;
            tree.ComputeStatePrices(i);
            statePrices = tree.StatePrices(i);
            this.discountBondPrice = discountBondPrice;
            this.theta = theta;
            this.tree = tree;
            theta.Set(tree.TimeGrid[i], 0.0);
        }

        public double Evaluate(double x)
        {
            double value = discountBondPrice;
            theta.Change(x);
            for (int j = 0; j < size; j++)
            {
                value -= statePrices[j] * tree.Discount(i, j);
            }

            return value;
        }
    }

    private static int BranchesOf(TrinomialTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        // QuantLib's tree->size(1): the second column's nodes, 3 for every trinomial tree
        return tree.Size(1);
    }
}
