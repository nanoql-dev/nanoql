// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp ql/methods/lattices/binomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Methods;

/// <summary>Joshi's fourth-order binomial tree: centered on the strike, its probabilities from Joshi's expansion,
/// on an odd number of steps.</summary>
/// <remarks>Immutable.</remarks>
public sealed class Joshi4 : BinomialTree<Joshi4>, IBinomialTree<Joshi4>
{
    private readonly double up;
    private readonly double down;
    private readonly double pu;
    private readonly double pd;

    /// <summary>Initializes a new instance of the <see cref="Joshi4"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps; an even count builds on the next odd one, as QuantLib's does, its
    /// <see cref="Tree.Columns"/> less one.</param>
    /// <param name="strike">The strike the tree centers on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The up probability is outside (0, 1).</exception>
    public Joshi4(StochasticProcess1D process, double end, int steps, double strike)
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
        pu = ComputeUpProb((oddSteps - 1.0) / 2.0, d2);
        if (!(pu > 0.0 && pu < 1.0))
        {
            throw new NanoQLException($"invalid up probability: {pu.ToString(CultureInfo.InvariantCulture)}");
        }

        pd = 1.0 - pu;
        double pdash = ComputeUpProb((oddSteps - 1.0) / 2.0, d2 + Math.Sqrt(variance));
        up = ermqdt * pdash / pu;
        down = (ermqdt - (pu * up)) / (1.0 - pu);
    }

    /// <inheritdoc/>
    public static Joshi4 Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    public override double Underlying(int i, int index) =>
        X0 * Math.Pow(down, (double)((long)i - index)) * Math.Pow(up, index);

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch) => branch == 1 ? pu : pd;

    private static double ComputeUpProb(double k, double dj)
    {
        double alpha = dj / Math.Sqrt(8.0);
        double alpha2 = alpha * alpha;
        double alpha3 = alpha * alpha2;
        double alpha5 = alpha3 * alpha2;
        double alpha7 = alpha5 * alpha2;
        double beta = (-0.375 * alpha) - alpha3;
        double gamma = (5.0 / 6.0 * alpha5) + (13.0 / 12.0 * alpha3) + (25.0 / 128.0 * alpha);
        double delta = (-0.1025 * alpha) - (0.9285 * alpha3) - (1.43 * alpha5) - (0.5 * alpha7);
        double p = 0.5;
        double rootk = Math.Sqrt(k);
        p += alpha / rootk;
        p += beta / (k * rootk);
        p += gamma / (k * k * rootk);

        // delete next line to get results for j three tree
        p += delta / (k * k * k * rootk);
        return p;
    }

    private static int OddSteps(int steps) => steps % 2 != 0 ? steps : steps + 1;
}
