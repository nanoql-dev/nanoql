// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// A binomial tree a binomial engine builds: the constructor QuantLib's engine template calls, <c>T(process, end,
/// steps, strike)</c> (<c>binomialengine.hpp:113-114</c>), as a static factory.
/// </summary>
/// <typeparam name="TSelf">The tree's own type.</typeparam>
public interface IBinomialTree<TSelf>
    where TSelf : Tree, IBinomialTree<TSelf>
{
    /// <summary>Builds the tree of a process from time 0 to <paramref name="end"/>.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps asked for; a tree may build on another (its <see cref="Tree.Columns"/>
    /// less one).</param>
    /// <param name="strike">The strike, which some trees center on.</param>
    /// <returns>The tree.</returns>
    static abstract TSelf Create(StochasticProcess1D process, double end, int steps, double strike);
}
