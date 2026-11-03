// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: Divergences/ParticleSwarmTopologyTests.cs (QuantLib's keeps the largest personal best and skips the best particle at every call, so no QuantLib route reaches the fixed social best)

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The global topology: each particle's social best is the best of all the particles' personal bests: QuantLib's
/// <c>GlobalTopology</c>.
/// </summary>
/// <remarks>
/// The best is the smallest personal best, and every particle's social best is set to it, the best particle's own
/// included: QuantLib's keeps the largest (<c>if (bestF &lt; pBF_[i])</c>), steering every particle toward the worst,
/// and skips the best particle, leaving its social best at the zero vector it started with; on a shifted sphere it
/// stops at 6.4e-5 where the k-neighbours topology reaches 2.7e-18 (its standard QuantLib's own definition above the
/// class; <c>Divergences/ParticleSwarmTopologyTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class GlobalTopology : ParticleSwarmOptimizationTopology
{
    private int M;

    /// <inheritdoc/>
    public override void SetSize(int M) => this.M = M;

    /// <inheritdoc/>
    public override void FindSocialBest()
    {
        double bestF = PersonalBestF[0];
        int bestP = 0;
        for (int i = 1; i < M; i++)
        {
            if (PersonalBestF[i] < bestF)
            {
                bestF = PersonalBestF[i];
                bestP = i;
            }
        }

        for (int i = 0; i < M; i++)
        {
            SetSocialBest(i, bestP, bestF);
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationTopology Clone() => new GlobalTopology();
}
