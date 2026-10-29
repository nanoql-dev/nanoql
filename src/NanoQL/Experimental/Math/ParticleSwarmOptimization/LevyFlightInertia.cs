// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A Levy flight inertia: while a particle keeps improving its personal best the inertia is a random one, and after
/// more than threshold iterations without improvement its velocity is a Levy flight, u^(−1/α): QuantLib's
/// <c>LevyFlightInertia</c>.
/// </summary>
/// <remarks>
/// The random factor's draws are a Mersenne Twister's and the flight an isotropic walk's on a <c>std::mt19937</c>, its
/// sphere an ellipsoid over the problem's bounds, all on the seed, which is required: QuantLib's default seeds from the
/// clock (the S-xrt plan's XRD3). Each minimization starts them from the seed.
/// </remarks>
[Experimental("NQL0001")]
public sealed class LevyFlightInertia : ParticleSwarmOptimizationInertia
{
    private readonly double alpha;
    private readonly int threshold;
    private readonly long seed;
    private readonly MersenneTwisterUniformRng rng;
    private readonly IsotropicRandomWalk<LevyFlightDistribution> flight;
    private Vector personalBestF = new();
    private int[] adaptiveCounter = [];
    private double c0;
    private int M, N;

    /// <summary>Initializes a new instance of the <see cref="LevyFlightInertia"/> class.</summary>
    /// <param name="alpha">The flight's exponent α, positive.</param>
    /// <param name="threshold">The iterations without improvement after which the flight starts.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException"><paramref name="alpha"/> isn't positive, with QuantLib's text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public LevyFlightInertia(double alpha, int threshold, long seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        this.alpha = alpha;
        this.threshold = threshold;
        this.seed = seed;
        rng = new MersenneTwisterUniformRng(seed);
        flight = new IsotropicRandomWalk<LevyFlightDistribution>(
            new MersenneTwisterUniformRng(seed), new LevyFlightDistribution(1.0, alpha), 1, new Vector(1, 1.0), seed);
    }

    /// <inheritdoc/>
    public override void SetSize(int M, int N, double c0, EndCriteria endCriteria)
    {
        this.M = M;
        this.N = N;
        this.c0 = c0;
        adaptiveCounter = new int[M];
    }

    /// <inheritdoc/>
    public override void SetValues()
    {
        for (int i = 0; i < M; i++)
        {
            if (PersonalBestF[i] < personalBestF[i])
            {
                personalBestF[i] = PersonalBestF[i];
                adaptiveCounter[i] = 0;
            }
            else
            {
                adaptiveCounter[i]++;
            }

            if (adaptiveCounter[i] <= threshold)
            {
                // simple random inertia
                Vector v = V[i];
                v *= c0 * (0.5 + (0.5 * rng.NextReal()));
            }
            else
            {
                // without a new personal best after threshold iterations, a Levy flight pattern for the speed
                flight.NextReal(V[i].AsSpan());
            }
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationInertia Clone() => new LevyFlightInertia(alpha, threshold, seed);

    /// <inheritdoc/>
    protected override void Init()
    {
        personalBestF = PersonalBestF.Clone();
        flight.SetDimension(N, LowerBound, UpperBound);
    }
}
