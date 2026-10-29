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
/// A time-dependent binomial tree whose up and down moves are equal in the logarithm of the underlying, the
/// probabilities carrying the drift at the step's time: QuantLib's <c>ExtendedEqualJumpsBinomialTree</c>.
/// </summary>
/// <typeparam name="T">The derived tree.</typeparam>
/// <remarks>
/// Immutable once built; a derived tree gives the move and the up probability at a time. QuantLib's <c>dx_</c>,
/// <c>pu_</c> and <c>pd_</c> members, which its trees set for their constructors' checks and nothing reads, aren't
/// ported.
/// </remarks>
[Experimental("NQL0001")]
public abstract class ExtendedEqualJumpsBinomialTree<T> : ExtendedBinomialTree<T>
    where T : ExtendedEqualJumpsBinomialTree<T>
{
    /// <summary>Initializes a new instance of the <see cref="ExtendedEqualJumpsBinomialTree{T}"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    protected ExtendedEqualJumpsBinomialTree(StochasticProcess1D process, double end, int steps)
        : base(process, end, steps)
    {
    }

    /// <inheritdoc/>
    public override double Underlying(int i, int index)
    {
        double stepTime = i * Dt;
        long j = (2L * index) - i;

        // exploiting equal jump and the x0_ tree centering
        return X0 * Math.Exp(j * DxStep(stepTime));
    }

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch)
    {
        double stepTime = i * Dt;
        double upProb = ProbUp(stepTime);
        double downProb = 1 - upProb;
        return branch == 1 ? upProb : downProb;
    }

    /// <summary>The probability of an up move at a step's time.</summary>
    /// <param name="stepTime">The step's time.</param>
    /// <returns>The probability.</returns>
    protected abstract double ProbUp(double stepTime);

    /// <summary>The move at a step's time, in the logarithm of the underlying.</summary>
    /// <param name="stepTime">The step's time.</param>
    /// <returns>The move.</returns>
    protected abstract double DxStep(double stepTime);
}
