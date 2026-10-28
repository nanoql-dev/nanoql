// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The binomial loss distribution: n names of one volume and one default probability.</summary>
/// <param name="nBuckets">The number of buckets.</param>
/// <param name="maximum">The largest loss.</param>
/// <remarks>
/// Immutable (the S-xcr plan's XR5): <see cref="EvaluateWithDetails"/> returns QuantLib's mutable by-products with the
/// distribution. A loss is kept where the given volume times its count is at most the maximum; QuantLib's tests its
/// never-set <c>volume_</c> member instead (<c>lossdistribution.cpp:155</c>; XR8 (xxvii), an unused class;
/// <c>Divergences/DistributionMembersTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class LossDistBinomial(int nBuckets, double maximum) : LossDist
{
    /// <inheritdoc/>
    public override int Buckets => nBuckets;

    /// <inheritdoc/>
    public override double Maximum => maximum;

    /// <summary>The distribution of n names' loss.</summary>
    /// <param name="n">The number of names.</param>
    /// <param name="volume">Each name's volume.</param>
    /// <param name="probability">Each name's default probability.</param>
    /// <returns>The distribution.</returns>
    public Distribution Evaluate(int n, double volume, double probability) => EvaluateWithDetails(n, volume, probability).Distribution;

    /// <summary>The distribution of n names' loss, with the probability of each number of defaults.</summary>
    /// <param name="n">The number of names.</param>
    /// <param name="volume">Each name's volume.</param>
    /// <param name="probability">Each name's default probability.</param>
    /// <returns>The distribution and its by-products.</returns>
    public LossDistResult EvaluateWithDetails(int n, double volume, double probability)
    {
        double[] probabilities = new double[n + 1];
        var dist = new Distribution(nBuckets, 0.0, maximum);
        var binomial = new BinomialDistribution(probability, n);
        for (int i = 0; i <= n; i++)
        {
            if (volume * i <= maximum)
            {
                probabilities[i] = binomial.Evaluate(i);
                int bucket = dist.Locate(volume * i);
                dist.AddDensity(bucket, probabilities[i] / dist.Dx(bucket));
                dist.AddAverage(bucket, volume * i);
            }
        }

        double[] excessProbability = new double[n + 1];
        excessProbability[n] = probabilities[n];
        for (int k = n - 1; k >= 0; k--)
        {
            excessProbability[k] = excessProbability[k + 1] + probabilities[k];
        }

        dist.Normalize();

        return new LossDistResult(dist, n, volume, [.. probabilities], [.. excessProbability]);
    }

    /// <summary>The distribution of the names' loss, on the first name's volume and probability.</summary>
    /// <param name="volumes">The names' volumes, the first read.</param>
    /// <param name="probabilities">The names' default probabilities, the first read.</param>
    /// <returns>The distribution.</returns>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    public override Distribution Evaluate(IReadOnlyList<double> volumes, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(probabilities);
        return Evaluate(volumes.Count, volumes[0], probabilities[0]);
    }
}
