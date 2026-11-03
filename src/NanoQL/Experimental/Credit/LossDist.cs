// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The loss distributions' base: a pool's distribution of losses from each name's volume and default probability, and
/// the probabilities of n defaults.
/// </summary>
/// <remarks>
/// <see cref="BinomialProbabilityOfAtLeastNEvents"/> at n of 0 or below is 1, where QuantLib's wraps n − 1 to the
/// largest unsigned value and returns 0 (<c>lossdistribution.cpp:37-38</c>;
/// <c>Divergences/DistributionMembersTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public abstract class LossDist
{
    /// <summary>Gets the number of buckets of the distributions built.</summary>
    public abstract int Buckets { get; }

    /// <summary>Gets the distributions' largest loss.</summary>
    public abstract double Maximum { get; }

    /// <summary>The binomial probability of n defaults on the first name's probability.</summary>
    /// <param name="n">The number of defaults; below 0 none is possible.</param>
    /// <param name="p">The names' probabilities, the first read.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    public static double BinomialProbabilityOfNEvents(int n, IReadOnlyList<double> p)
    {
        ArgumentNullException.ThrowIfNull(p);

        // QuantLib's negative n wraps to an unsigned count above the pool's, of probability 0
        return n < 0 ? 0.0 : new BinomialDistribution(p[0], p.Count).Evaluate(n);
    }

    /// <summary>The binomial probability of at least n defaults on the first name's probability.</summary>
    /// <param name="n">The number of defaults; at or below 0, 1.</param>
    /// <param name="p">The names' probabilities, the first read.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    public static double BinomialProbabilityOfAtLeastNEvents(int n, IReadOnlyList<double> p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (n <= 0)
        {
            return 1.0;
        }

        var binomial = new CumulativeBinomialDistribution(p[0], p.Count);
        return 1.0 - binomial.Evaluate(n - 1);
    }

    /// <summary>
    /// The probability of exactly k default events for every k from 0 to the number of names: Xiaofong Ma, "Numerical
    /// Methods for the Valuation of Synthetic Collateralized Debt Obligations", PhD Thesis, University of Toronto,
    /// 2007 (formula 2.1).
    /// </summary>
    /// <param name="p">The names' probabilities.</param>
    /// <returns>The probabilities, of the number of names plus one entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    public static ImmutableArray<double> ProbabilityOfNEvents(IReadOnlyList<double> p)
    {
        ArgumentNullException.ThrowIfNull(p);
        int n = p.Count;
        double[] probability = new double[n + 1];
        double[] prev;
        probability[0] = 1.0;
        for (int j = 0; j < n; j++)
        {
            prev = (double[])probability.Clone();
            probability[0] = prev[0] * (1.0 - p[j]);
            for (int i = 1; i <= j; i++)
            {
                probability[i] = (prev[i - 1] * p[j]) + (prev[i] * (1.0 - p[j]));
            }

            probability[j + 1] = prev[j] * p[j];
        }

        return [.. probability];
    }

    /// <summary>The probability of exactly k default events.</summary>
    /// <param name="k">The number of defaults, from 0 to the number of names.</param>
    /// <param name="p">The names' probabilities.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> lies outside [0, n], where QuantLib's reads
    /// past its vector.</exception>
    public static double ProbabilityOfNEvents(int k, IReadOnlyList<double> p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (k < 0 || k > p.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "the number of defaults must lie in [0, the number of names]");
        }

        return ProbabilityOfNEvents(p)[k];
    }

    /// <summary>The probability of at least k default events.</summary>
    /// <param name="k">The number of defaults, at most the number of names plus one; at or below 0, 1.</param>
    /// <param name="p">The names' probabilities.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is above the number of names plus one,
    /// where QuantLib's reads past its vector.</exception>
    public static double ProbabilityOfAtLeastNEvents(int k, IReadOnlyList<double> p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (k > p.Count + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "the number of defaults must be at most the number of names plus one");
        }

        ImmutableArray<double> probability = ProbabilityOfNEvents(p);
        double sum = 1.0;
        for (int j = 0; j < k; j++)
        {
            sum -= probability[j];
        }

        return sum;
    }

    /// <summary>The distribution of the pool's loss: QuantLib's <c>operator()</c>.</summary>
    /// <param name="volumes">The names' volumes (losses given default).</param>
    /// <param name="probabilities">The names' default probabilities.</param>
    /// <returns>The distribution.</returns>
    public abstract Distribution Evaluate(IReadOnlyList<double> volumes, IReadOnlyList<double> probabilities);
}
