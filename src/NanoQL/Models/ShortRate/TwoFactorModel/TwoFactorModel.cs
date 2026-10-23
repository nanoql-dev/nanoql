// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/twofactormodel.hpp ql/models/shortrate/twofactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>The abstract two-factor short-rate model class: its short rate a function of two state variables.</summary>
/// <remarks>An extension point, as in QuantLib: G2 derives from it.</remarks>
public abstract class TwoFactorModel : ShortRateModel
{
    /// <summary>Initializes a new instance of the <see cref="TwoFactorModel"/> class.</summary>
    /// <param name="nArguments">The number of arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is negative.</exception>
    protected TwoFactorModel(int nArguments)
        : base(nArguments)
    {
    }

    /// <summary>The model's short-rate dynamics.</summary>
    /// <returns>A new dynamics object.</returns>
    public abstract TwoFactorModelShortRateDynamics Dynamics();

    /// <summary>Builds a two-dimensional trinomial tree of the two state variables on a time grid.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The tree, a <see cref="TwoFactorModelShortRateTree"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        TwoFactorModelShortRateDynamics dyn = Dynamics();

        var tree1 = new TrinomialTree(dyn.XProcess, grid);
        var tree2 = new TrinomialTree(dyn.YProcess, grid);

        return new TwoFactorModelShortRateTree(tree1, tree2, dyn);
    }
}
