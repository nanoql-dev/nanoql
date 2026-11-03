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
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The time-dependent Leisen-Reimer binomial tree: centered on the strike, its probabilities from the Peizer-Pratt
/// inversion at the step's time, on an odd number of steps: QuantLib's <c>ExtendedLeisenReimer</c>.
/// </summary>
/// <remarks>
/// Immutable; exact on constant parameters (<see cref="ExtendedBinomialTree{T}"/>). An even number of steps is raised
/// by one. The constructor checks the strike and the up probability at time 0 only, with the standard tree's texts
/// (<see cref="LeisenReimer"/>); its <c>up_</c>, <c>down_</c>, <c>pu_</c> and <c>pd_</c> members, which nothing reads
/// after that check, aren't ported.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedLeisenReimer : ExtendedBinomialTree<ExtendedLeisenReimer>, IBinomialTree<ExtendedLeisenReimer>
{
    private readonly double end;
    private readonly int oddSteps;
    private readonly double strike;

    /// <summary>Initializes a new instance of the <see cref="ExtendedLeisenReimer"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps; an even one is raised by one, the tree's <see cref="Tree.Columns"/>
    /// less one.</param>
    /// <param name="strike">The strike the tree centers on, positive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The up probability at time 0 isn't in (0, 1).</exception>
    public ExtendedLeisenReimer(StochasticProcess1D process, double end, int steps, double strike)
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
        double pu = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(d2, oddSteps);
        if (!(pu > 0.0 && pu < 1.0))
        {
            throw new NanoQLException($"invalid up probability: {pu.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <inheritdoc/>
    public static ExtendedLeisenReimer Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    public override double Underlying(int i, int index)
    {
        double stepTime = i * Dt;
        double variance = TreeProcess.Variance(stepTime, X0, end);
        double ermqdt = Math.Exp(DriftStep(stepTime) + (0.5 * variance / oddSteps));
        double d2 = (Math.Log(X0 / strike) + (DriftStep(stepTime) * oddSteps)) / Math.Sqrt(variance);
        double pu = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(d2, oddSteps);
        double pdash = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(d2 + Math.Sqrt(variance), oddSteps);
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
        double pu = BinomialDistributionFunctions.PeizerPrattMethod2Inversion(d2, oddSteps);
        double pd = 1.0 - pu;
        return branch == 1 ? pu : pd;
    }

    private static int OddSteps(int steps) => steps % 2 != 0 ? steps : steps + 1;
}
