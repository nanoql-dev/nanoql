// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: Divergences/ParticleSwarmInertiaTests.cs (QuantLib's loops over a swarm size it never sets, undefined)

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A decreasing inertia: each velocity times the optimizer's inertia times a factor falling linearly with the
/// iterations to the threshold at the last: QuantLib's <c>DecreasingInertia</c>.
/// </summary>
/// <remarks>
/// The factor is threshold + (1 − threshold)(K − k)/K at the k-th call of K iterations, k counted from 1, so the last
/// iteration's is the threshold. QuantLib's never sets its swarm size, which it loops over (undefined), and never
/// advances its iteration, so its factor stays 1 and the inertia never decreases
/// (<c>Divergences/ParticleSwarmInertiaTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class DecreasingInertia : ParticleSwarmOptimizationInertia
{
    private readonly double threshold;
    private double c0;
    private int M;
    private int maxIterations;
    private int iteration;

    /// <summary>Initializes a new instance of the <see cref="DecreasingInertia"/> class.</summary>
    /// <param name="threshold">The last iteration's factor, in [0, 1); QuantLib's default 0.5.</param>
    /// <exception cref="ArgumentException"><paramref name="threshold"/> is outside [0, 1), with QuantLib's text.</exception>
    public DecreasingInertia(double threshold = 0.5)
    {
        if (!(threshold >= 0.0 && threshold < 1.0))
        {
            throw new ArgumentException("Threshold must be a Real in [0, 1)", nameof(threshold));
        }

        this.threshold = threshold;
    }

    /// <inheritdoc/>
    public override void SetSize(int M, int N, double c0, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(endCriteria);
        this.M = M;
        this.c0 = c0;
        iteration = 0;
        maxIterations = endCriteria.MaxIterations;
    }

    /// <inheritdoc/>
    public override void SetValues()
    {
        iteration++;
        double c = c0 * (threshold + ((1.0 - threshold) * (maxIterations - iteration) / maxIterations));
        for (int i = 0; i < M; i++)
        {
            Vector v = V[i];
            v *= c;
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationInertia Clone() => new DecreasingInertia(threshold);
}
