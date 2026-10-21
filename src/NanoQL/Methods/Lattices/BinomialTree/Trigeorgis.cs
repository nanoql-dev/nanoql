// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp ql/methods/lattices/binomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The Trigeorgis binomial tree: equal jumps matching the step's variance and squared drift, the
/// probabilities carrying the drift.</summary>
/// <remarks>Immutable.</remarks>
public sealed class Trigeorgis : EqualJumpsBinomialTree<Trigeorgis>, IBinomialTree<Trigeorgis>
{
    /// <summary>Initializes a new instance of the <see cref="Trigeorgis"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">Not read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="NanoQLException">The up probability is outside [0, 1].</exception>
    public Trigeorgis(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
        Dx = Math.Sqrt(process.Variance(0.0, X0, Dt) + (DriftPerStep * DriftPerStep));
        Pu = 0.5 + (0.5 * DriftPerStep / Dx);
        Pd = 1.0 - Pu;
        if (!(Pu <= 1.0))
        {
            throw new NanoQLException("negative probability");
        }

        if (!(Pu >= 0.0))
        {
            throw new NanoQLException("negative probability");
        }
    }

    /// <inheritdoc/>
    public static Trigeorgis Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);
}
