// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The loss distribution of varying volumes and default probabilities by a Monte Carlo simulation of
/// independent defaults, each a Mersenne Twister draw at or below its probability.</summary>
/// <remarks>
/// The seed is required, in [1, 4294967295]; QuantLib's defaults to 42, which gives its draws.
/// </remarks>
[Experimental("NQL0001")]
public sealed class LossDistMonteCarlo : LossDist
{
    private readonly int nBuckets;
    private readonly double maximum;
    private readonly int simulations;
    private readonly long seed;
    private readonly double epsilon;

    /// <summary>Initializes a new instance of the <see cref="LossDistMonteCarlo"/> class.</summary>
    /// <param name="nBuckets">The number of buckets.</param>
    /// <param name="maximum">The largest loss.</param>
    /// <param name="simulations">The number of paths.</param>
    /// <param name="seed">The Mersenne Twister's seed, in [1, 4294967295]; QuantLib's default is 42.</param>
    /// <param name="epsilon">The shift each path's loss takes into its bucket.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> lies outside [1,
    /// 4294967295].</exception>
    public LossDistMonteCarlo(int nBuckets, double maximum, int simulations, long seed, double epsilon = 1e-6)
    {
        _ = MersenneTwisterUniformRng.Create(seed);
        this.nBuckets = nBuckets;
        this.maximum = maximum;
        this.simulations = simulations;
        this.seed = seed;
        this.epsilon = epsilon;
    }

    /// <inheritdoc/>
    public override int Buckets => nBuckets;

    /// <inheritdoc/>
    public override double Maximum => maximum;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    public override Distribution Evaluate(IReadOnlyList<double> volumes, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(probabilities);
        var dist = new Distribution(nBuckets, 0.0, maximum);
        var rng = MersenneTwisterUniformRng.Create(seed);
        for (int i = 0; i < simulations; i++)
        {
            double e = 0;
            for (int j = 0; j < volumes.Count; j++)
            {
                double r = rng.Next().Value;
                if (r <= probabilities[j])
                {
                    e += volumes[j];
                }
            }

            dist.Add(e + epsilon);
        }

        dist.Normalize();

        return dist;
    }
}
