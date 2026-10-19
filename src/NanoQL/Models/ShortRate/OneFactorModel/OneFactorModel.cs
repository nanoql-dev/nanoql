// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodel.hpp ql/models/shortrate/onefactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>The single-factor short-rate model abstract class: its short rate a function of one state variable.</summary>
/// <remarks>An extension point, as in QuantLib: the affine one-factor models derive from it, as later ports' Black-Karasinski will.</remarks>
public abstract class OneFactorModel : ShortRateModel
{
    /// <summary>Initializes a new instance of the <see cref="OneFactorModel"/> class.</summary>
    /// <param name="nArguments">The number of arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is negative.</exception>
    protected OneFactorModel(int nArguments)
        : base(nArguments)
    {
    }

    /// <summary>The model's short-rate dynamics.</summary>
    /// <returns>A new dynamics object.</returns>
    public abstract OneFactorModelShortRateDynamics Dynamics();

    /// <summary>Builds a recombining trinomial tree of the state variable on a time grid: QuantLib's default lattice.</summary>
    /// <param name="grid">The time grid.</param>
    /// <returns>The tree, an unfitted <see cref="OneFactorModelShortRateTree"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
    public override Lattice Tree(TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var trinomial = new TrinomialTree(Dynamics().Process, grid);
        return new OneFactorModelShortRateTree(trinomial, Dynamics(), grid);
    }
}
