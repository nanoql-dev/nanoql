// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp ql/experimental/math/particleswarmoptimization.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: Divergences/ParticleSwarmTopologyTests.cs (QuantLib's window starts one particle early and its search starts from particle 0's position at every call, so no QuantLib route reaches the fixed social bests)

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The K-neighbours topology: each particle's social best is the best of the K particles before and the K after it,
/// the swarm a ring: QuantLib's <c>KNeighbors</c>.
/// </summary>
/// <remarks>
/// For particle i the window is [i − K, i + K], wrapping around, as the class documents, and a particle that is its
/// window's best is its own social best: QuantLib's window runs from i − K − 1 to i + K − 1, and its search starts from
/// particle i's value paired with particle 0's position, so a particle that is its window's best takes particle 0's
/// position (<c>Divergences/ParticleSwarmTopologyTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class KNeighbors : ParticleSwarmOptimizationTopology
{
    private readonly int K;
    private int M;

    /// <summary>Initializes a new instance of the <see cref="KNeighbors"/> class.</summary>
    /// <param name="K">The neighbours on each side, positive; QuantLib's default 1.</param>
    /// <exception cref="ArgumentException"><paramref name="K"/> isn't positive, with QuantLib's text.</exception>
    public KNeighbors(int K = 1)
    {
        if (!(K > 0))
        {
            throw new ArgumentException("Neighbors need to be larger than 0", nameof(K));
        }

        this.K = K;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">K isn't below the swarm's size, with QuantLib's text.</exception>
    public override void SetSize(int M)
    {
        this.M = M;
        if (!(K < M))
        {
            throw new NanoQLException("Number of neighbors need to be smaller than total particles in swarm");
        }
    }

    /// <inheritdoc/>
    public override void FindSocialBest()
    {
        for (int i = 0; i < M; i++)
        {
            double bestF = PersonalBestF[i];
            int bestX = i;
            for (int d = -K; d <= K; d++)
            {
                int j = (((i + d) % M) + M) % M;
                if (PersonalBestF[j] < bestF)
                {
                    bestF = PersonalBestF[j];
                    bestX = j;
                }
            }

            SetSocialBest(i, bestX, bestF);
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationTopology Clone() => new KNeighbors(K);
}
