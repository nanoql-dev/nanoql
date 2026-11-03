// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A random inertia: each velocity times the optimizer's inertia times a uniform draw in (threshold, 1): QuantLib's
/// <c>SimpleRandomInertia</c>.
/// </summary>
/// <remarks>
/// The draws are a Mersenne Twister's on the seed, which is required: QuantLib's default seeds from the clock. Each
/// minimization starts them from the seed.
/// </remarks>
[Experimental("NQL0001")]
public sealed class SimpleRandomInertia : ParticleSwarmOptimizationInertia
{
    private readonly double threshold;
    private readonly long seed;
    private readonly MersenneTwisterUniformRng rng;
    private double c0;
    private int M;

    /// <summary>Initializes a new instance of the <see cref="SimpleRandomInertia"/> class.</summary>
    /// <param name="threshold">The draws' lower end, in [0, 1); QuantLib's default 0.5.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException"><paramref name="threshold"/> is outside [0, 1), with QuantLib's text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public SimpleRandomInertia(double threshold, long seed)
    {
        if (!(threshold >= 0.0 && threshold < 1.0))
        {
            throw new ArgumentException("Threshold must be a Real in [0, 1)", nameof(threshold));
        }

        this.threshold = threshold;
        this.seed = seed;
        rng = new MersenneTwisterUniformRng(seed);
    }

    /// <inheritdoc/>
    public override void SetSize(int M, int N, double c0, EndCriteria endCriteria)
    {
        this.M = M;
        this.c0 = c0;
    }

    /// <inheritdoc/>
    public override void SetValues()
    {
        for (int i = 0; i < M; i++)
        {
            double val = c0 * (threshold + ((1.0 - threshold) * rng.NextReal()));
            Vector v = V[i];
            v *= val;
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationInertia Clone() => new SimpleRandomInertia(threshold, seed);
}
