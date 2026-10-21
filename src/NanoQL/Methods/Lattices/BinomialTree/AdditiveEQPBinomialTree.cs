// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp ql/methods/lattices/binomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The additive equal-probabilities binomial tree: equal probabilities, the up move matching the step's
/// first two moments with the drift.</summary>
/// <remarks>Immutable.</remarks>
public sealed class AdditiveEQPBinomialTree
    : EqualProbabilitiesBinomialTree<AdditiveEQPBinomialTree>, IBinomialTree<AdditiveEQPBinomialTree>
{
    /// <summary>Initializes a new instance of the <see cref="AdditiveEQPBinomialTree"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">Not read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="NanoQLException">The drift per step is too large for the step's variance.</exception>
    public AdditiveEQPBinomialTree(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
        double radicand = (4.0 * process.Variance(0.0, X0, Dt)) - (3.0 * DriftPerStep * DriftPerStep);
        if (!(radicand >= 0.0))
        {
            throw new NanoQLException("drift per step too large for the additive EQP tree: more steps are needed");
        }

        Up = (-0.5 * DriftPerStep) + (0.5 * Math.Sqrt(radicand));
    }

    /// <inheritdoc/>
    public static AdditiveEQPBinomialTree Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);
}
