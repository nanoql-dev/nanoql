// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 John Maiden
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/lattices/extendedbinomialtree.hpp ql/experimental/lattices/extendedbinomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The time-dependent Joshi binomial tree of order 4: centered on the strike, its probabilities from Joshi's expansion
/// at the step's time, on an odd number of steps: QuantLib's <c>ExtendedJoshi4</c>.
/// </summary>
/// <remarks>
/// Immutable; exact on constant parameters (<see cref="ExtendedBinomialTree{T}"/>, XRR6). An even number of steps is
/// raised by one. The constructor checks the strike and the up probability at time 0 only, with the standard tree's
/// texts (<see cref="Joshi4"/>); its <c>up_</c>, <c>down_</c>, <c>pu_</c> and <c>pd_</c> members, which nothing reads
/// after that check, aren't ported.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedJoshi4 : ExtendedBinomialTree<ExtendedJoshi4>, IBinomialTree<ExtendedJoshi4>
{
    private readonly double end;
    private readonly int oddSteps;
    private readonly double strike;

    /// <summary>Initializes a new instance of the <see cref="ExtendedJoshi4"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps; an even one is raised by one, the tree's <see cref="Tree.Columns"/>
    /// less one.</param>
    /// <param name="strike">The strike the tree centers on, positive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The up probability at time 0 isn't in (0, 1).</exception>
    public ExtendedJoshi4(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, OddSteps(steps))
    {
        this.end = end;
        oddSteps = OddSteps(steps);
        this.strike = strike;
        if (!(strike > 0.0))
        {
            throw new ArgumentException("strike must be positive", nameof(strike));
        }

        double variance = process.Variance(0.0, X0, end);
        double d2 = (Math.Log(X0 / strike) + (DriftStep(0.0) * oddSteps)) / Math.Sqrt(variance);
        double pu = ComputeUpProb((oddSteps - 1.0) / 2.0, d2);
        if (!(pu > 0.0 && pu < 1.0))
        {
            throw new NanoQLException($"invalid up probability: {pu.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <inheritdoc/>
    public static ExtendedJoshi4 Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    public override double Underlying(int i, int index)
    {
        double stepTime = i * Dt;
        double variance = TreeProcess.Variance(stepTime, X0, end);
        double ermqdt = Math.Exp(DriftStep(stepTime) + (0.5 * variance / oddSteps));
        double d2 = (Math.Log(X0 / strike) + (DriftStep(stepTime) * oddSteps)) / Math.Sqrt(variance);
        double pu = ComputeUpProb((oddSteps - 1.0) / 2.0, d2);
        double pdash = ComputeUpProb((oddSteps - 1.0) / 2.0, d2 + Math.Sqrt(variance));
        double up = ermqdt * pdash / pu;
        double down = (ermqdt - (pu * up)) / (1.0 - pu);
        return X0 * Math.Pow(down, (double)((long)i - index)) * Math.Pow(up, index);
    }

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch)
    {
        double stepTime = i * Dt;
        double variance = TreeProcess.Variance(stepTime, X0, end);
        double d2 = (Math.Log(X0 / strike) + (DriftStep(stepTime) * oddSteps)) / Math.Sqrt(variance);
        double pu = ComputeUpProb((oddSteps - 1.0) / 2.0, d2);
        double pd = 1.0 - pu;
        return branch == 1 ? pu : pd;
    }

    /// <summary>Joshi's up probability of order 4: QuantLib's <c>computeUpProb</c>.</summary>
    /// <param name="k">Half the steps less one.</param>
    /// <param name="dj">The d₂ of the strike.</param>
    /// <returns>The probability.</returns>
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
