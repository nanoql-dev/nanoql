// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A binomial tree with both branches' probabilities 1/2, its nodes at the drift plus a multiple of the up
/// move.</summary>
/// <typeparam name="T">The derived tree.</typeparam>
/// <remarks>Immutable once built; a derived tree sets <see cref="Up"/> in its constructor.</remarks>
public abstract class EqualProbabilitiesBinomialTree<T> : BinomialTree<T>
    where T : EqualProbabilitiesBinomialTree<T>
{
    /// <summary>Initializes a new instance of the <see cref="EqualProbabilitiesBinomialTree{T}"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    protected EqualProbabilitiesBinomialTree(StochasticProcess1D process, double end, int steps)
        : base(process, end, steps)
    {
    }

    /// <summary>Gets the up move, in the logarithm of the underlying.</summary>
    protected double Up { get; init; }

    /// <inheritdoc/>
    public override double Underlying(int i, int index)
    {
        long j = (2L * index) - i;
        return X0 * Math.Exp((i * DriftPerStep) + (j * Up));
    }

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch) => 0.5;
}
