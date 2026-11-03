// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp ql/experimental/math/particleswarmoptimization.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: Divergences/ParticleSwarmInertiaTests.cs (QuantLib's unsigned counter wraps at the first improvement, from a swarm's second iteration)

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// An adaptive inertia, after A. Lukic, "Approximating Kinetic Parameters Using Particle Swarm Optimization": halved,
/// within its bounds, after more than sh iterations' net stalls, and doubled below sl: QuantLib's
/// <c>AdaptiveInertia</c>.
/// </summary>
/// <remarks>
/// The counter of improvements less stalls is signed: QuantLib's is unsigned, so its first improvement wraps it to 2⁶⁴
/// − 1 and halves the inertia as if the swarm had stalled (<c>Divergences/ParticleSwarmInertiaTests.cs</c>). The first
/// iteration leaves the inertia unchanged.
/// </remarks>
[Experimental("NQL0001")]
public sealed class AdaptiveInertia : ParticleSwarmOptimizationInertia
{
    private readonly double minInertia, maxInertia;
    private readonly int sh, sl;
    private double c0, best;
    private int M;
    private long adaptiveCounter;
    private bool started;

    /// <summary>Initializes a new instance of the <see cref="AdaptiveInertia"/> class.</summary>
    /// <param name="minInertia">The inertia's lower bound.</param>
    /// <param name="maxInertia">The inertia's upper bound.</param>
    /// <param name="sh">The net stalls above which the inertia halves.</param>
    /// <param name="sl">The net stalls below which the inertia doubles.</param>
    public AdaptiveInertia(double minInertia, double maxInertia, int sh = 5, int sl = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sh);
        ArgumentOutOfRangeException.ThrowIfNegative(sl);
        this.minInertia = minInertia;
        this.maxInertia = maxInertia;
        this.sh = sh;
        this.sl = sl;
    }

    /// <inheritdoc/>
    public override void SetSize(int M, int N, double c0, EndCriteria endCriteria)
    {
        this.M = M;
        this.c0 = c0;
        adaptiveCounter = 0;
        best = NumericLimits.QL_MAX_REAL;
        started = false;
    }

    /// <inheritdoc/>
    public override void SetValues()
    {
        double currBest = PersonalBestF[0];
        for (int i = 1; i < M; i++)
        {
            if (currBest > PersonalBestF[i])
            {
                currBest = PersonalBestF[i];
            }
        }

        if (started)
        {
            // the first iteration leaves the inertia unchanged
            if (currBest < best)
            {
                best = currBest;
                adaptiveCounter--;
            }
            else
            {
                adaptiveCounter++;
            }

            if (adaptiveCounter > sh)
            {
                c0 = Math.Max(minInertia, Math.Min(maxInertia, c0 * 0.5));
            }
            else if (adaptiveCounter < sl)
            {
                c0 = Math.Max(minInertia, Math.Min(maxInertia, c0 * 2.0));
            }
        }
        else
        {
            best = currBest;
            started = true;
        }

        for (int i = 0; i < M; i++)
        {
            Vector v = V[i];
            v *= c0;
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationInertia Clone() => new AdaptiveInertia(minInertia, maxInertia, sh, sl);
}
