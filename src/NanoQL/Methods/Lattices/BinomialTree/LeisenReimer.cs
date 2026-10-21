// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp ql/methods/lattices/binomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>The Leisen-Reimer binomial tree: centered on the strike, its probabilities from the Peizer-Pratt
/// inversion, on an odd number of steps.</summary>
/// <remarks>Immutable.</remarks>
public sealed class LeisenReimer : BinomialTree<LeisenReimer>, IBinomialTree<LeisenReimer>
{
    private readonly double up;
    private readonly double down;
    private readonly double pu;
    private readonly double pd;

    /// <summary>Initializes a new instance of the <see cref="LeisenReimer"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps; an even count builds on the next odd one, as QuantLib's does, its
    /// <see cref="Tree.Columns"/> less one.</param>
    /// <param name="strike">The strike the tree centers on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The up probability is outside (0, 1).</exception>
    public LeisenReimer(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, OddSteps(steps))
    {
        if (!(strike > 0.0))
        {
            throw new ArgumentException("strike must be positive", nameof(strike));
        }

        int oddSteps = OddSteps(steps);
        double variance = process.Variance(0.0, X0, end);
        double ermqdt = Math.Exp(DriftPerStep + (0.5 * variance / oddSteps));
        double d2 = (Math.Log(X0 / strike) + (DriftPerStep * oddSteps)) / Math.Sqrt(variance);
        pu = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(d2, oddSteps);
        if (!(pu > 0.0 && pu < 1.0))
        {
            throw new NanoQLException($"invalid up probability: {pu.ToString(CultureInfo.InvariantCulture)}");
        }

        pd = 1.0 - pu;
        double pdash = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(d2 + Math.Sqrt(variance), oddSteps);
        up = ermqdt * pdash / pu;
        down = (ermqdt - (pu * up)) / (1.0 - pu);
    }

    /// <inheritdoc/>
    public static LeisenReimer Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    public override double Underlying(int i, int index) =>
        X0 * Math.Pow(down, (double)((long)i - index)) * Math.Pow(up, index);

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch) => branch == 1 ? pu : pd;

    private static int OddSteps(int steps) => steps % 2 != 0 ? steps : steps + 1;
}
