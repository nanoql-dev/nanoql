// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp ql/methods/lattices/binomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The Tian binomial tree: up and down moves and probabilities matching the step's first three
/// moments.</summary>
/// <remarks>Immutable.</remarks>
public sealed class Tian : BinomialTree<Tian>, IBinomialTree<Tian>
{
    private readonly double up;
    private readonly double down;
    private readonly double pu;
    private readonly double pd;

    /// <summary>Initializes a new instance of the <see cref="Tian"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">Not read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="NanoQLException">The up probability is outside [0, 1].</exception>
    public Tian(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
        double q = Math.Exp(process.Variance(0.0, X0, Dt));
        double r = Math.Exp(DriftPerStep) * Math.Sqrt(q);
        up = 0.5 * r * q * (q + 1 + Math.Sqrt((q * q) + (2 * q) - 3));
        down = 0.5 * r * q * (q + 1 - Math.Sqrt((q * q) + (2 * q) - 3));
        pu = (r - down) / (up - down);
        pd = 1.0 - pu;
        if (!(pu <= 1.0))
        {
            throw new NanoQLException("negative probability");
        }

        if (!(pu >= 0.0))
        {
            throw new NanoQLException("negative probability");
        }
    }

    /// <inheritdoc/>
    public static Tian Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    public override double Underlying(int i, int index) =>
        X0 * Math.Pow(down, (double)((long)i - index)) * Math.Pow(up, index);

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch) => branch == 1 ? pu : pd;
}
