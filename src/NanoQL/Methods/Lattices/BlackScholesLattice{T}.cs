// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/bsmlattice.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The Black-Scholes lattice on a binomial tree: the tree's nodes and probabilities, discounted at a flat rate over a
/// uniform grid.
/// </summary>
/// <typeparam name="T">The binomial tree.</typeparam>
/// <remarks>
/// Immutable once built (S-bond's lattice rule). Its step back is QuantLib's, the tree's first-node probabilities
/// applied to every node: <c>(pd·v[j] + pu·v[j + 1])·discount</c>.
/// </remarks>
public sealed class BlackScholesLattice<T> : TreeLattice1D
    where T : Tree
{
    private readonly T tree;
    private readonly double discount;
    private readonly double pd;
    private readonly double pu;

    /// <summary>Initializes a new instance of the <see cref="BlackScholesLattice{T}"/> class.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="riskFreeRate">The flat, continuously compounded risk-free rate.</param>
    /// <param name="end">The grid's last time.</param>
    /// <param name="steps">The grid's number of steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    public BlackScholesLattice(T tree, double riskFreeRate, double end, int steps)
        : base(new TimeGrid(end, steps), 2)
    {
        ArgumentNullException.ThrowIfNull(tree);
        this.tree = tree;
        RiskFreeRate = riskFreeRate;
        Dt = end / steps;
        discount = Math.Exp(-riskFreeRate * Dt);
        pd = tree.Probability(0, 0, 0);
        pu = tree.Probability(0, 0, 1);
    }

    /// <summary>Gets the flat risk-free rate.</summary>
    public double RiskFreeRate { get; }

    /// <summary>Gets the step's length.</summary>
    public double Dt { get; }

    /// <inheritdoc/>
    public override int Size(int i) => tree.Size(i);

    /// <inheritdoc/>
    public override double Discount(int i, int index) => discount;

    /// <inheritdoc/>
    public override double Underlying(int i, int index) => tree.Underlying(i, index);

    /// <inheritdoc/>
    public override int Descendant(int i, int index, int branch) => tree.Descendant(i, index, branch);

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch) => tree.Probability(i, index, branch);

    /// <inheritdoc/>
    public override void Stepback(int i, ReadOnlySpan<double> values, Span<double> newValues)
    {
        int size = Size(i);
        for (int j = 0; j < size; j++)
        {
            newValues[j] = ((pd * values[j]) + (pu * values[j + 1])) * discount;
        }
    }
}
