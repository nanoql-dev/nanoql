// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 John Maiden
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/lattices/extendedbinomialtree.hpp ql/experimental/lattices/extendedbinomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// A time-dependent binomial tree with both branches' probabilities 1/2, its nodes at the drift plus a multiple of the
/// up move at the step's time: QuantLib's <c>ExtendedEqualProbabilitiesBinomialTree</c>.
/// </summary>
/// <typeparam name="T">The derived tree.</typeparam>
/// <remarks>
/// Immutable once built; a derived tree gives the up move at a time. QuantLib's <c>up_</c> member, which its trees set
/// and nothing reads, isn't ported.
/// </remarks>
[Experimental("NQL0001")]
public abstract class ExtendedEqualProbabilitiesBinomialTree<T> : ExtendedBinomialTree<T>
    where T : ExtendedEqualProbabilitiesBinomialTree<T>
{
    /// <summary>Initializes a new instance of the <see cref="ExtendedEqualProbabilitiesBinomialTree{T}"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    protected ExtendedEqualProbabilitiesBinomialTree(StochasticProcess1D process, double end, int steps)
        : base(process, end, steps)
    {
    }

    /// <inheritdoc/>
    public override double Underlying(int i, int index)
    {
        double stepTime = i * Dt;
        long j = (2L * index) - i;

        // exploiting the forward value tree centering
        return X0 * Math.Exp((i * DriftStep(stepTime)) + (j * UpStep(stepTime)));
    }

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch) => 0.5;

    /// <summary>The tree's up move at a step's time, in the logarithm of the underlying.</summary>
    /// <param name="stepTime">The step's time.</param>
    /// <returns>The up move.</returns>
    protected abstract double UpStep(double stepTime);
}
