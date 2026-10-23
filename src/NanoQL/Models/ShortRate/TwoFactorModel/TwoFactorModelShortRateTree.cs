// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/twofactormodel.hpp ql/models/shortrate/twofactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>A recombining two-dimensional tree discretizing a two-factor model's state variables.</summary>
/// <remarks>QuantLib's <c>TwoFactorModel::ShortRateTree</c>: the plain tree build-up from the short-rate dynamics. Immutable once built.</remarks>
public sealed class TwoFactorModelShortRateTree : TreeLattice2D
{
    private readonly TwoFactorModelShortRateDynamics dynamics;

    /// <summary>Initializes a new instance of the <see cref="TwoFactorModelShortRateTree"/> class.</summary>
    /// <param name="tree1">The trinomial tree of the first state variable.</param>
    /// <param name="tree2">The trinomial tree of the second state variable, on the first's grid.</param>
    /// <param name="dynamics">The short-rate dynamics, whose correlation the lattice takes.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TwoFactorModelShortRateTree(TrinomialTree tree1, TrinomialTree tree2, TwoFactorModelShortRateDynamics dynamics)
        : base(tree1, tree2, CorrelationOf(dynamics))
    {
        this.dynamics = dynamics;
    }

    /// <summary>The discount factor over the step that starts at a node: exp(−r·dt), r the short rate at the node's pair of states.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <returns>The discount factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public override double Discount(int i, int index)
    {
        int modulo = Tree1.Size(i);
        int index1 = index % modulo;
        int index2 = index / modulo;

        double x = Tree1.Underlying(i, index1);
        double y = Tree2.Underlying(i, index2);

        double r = dynamics.ShortRate(TimeGrid[i], x, y);
        return Math.Exp(-r * TimeGrid.Dt(i));
    }

    private static double CorrelationOf(TwoFactorModelShortRateDynamics dynamics)
    {
        ArgumentNullException.ThrowIfNull(dynamics);
        return dynamics.Correlation;
    }
}
