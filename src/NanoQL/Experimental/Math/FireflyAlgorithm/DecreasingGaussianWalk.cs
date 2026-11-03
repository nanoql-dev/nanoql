// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A Gaussian random walk whose step decreases: after each round of the moved fireflies' walks, δ becomes δ²:
/// QuantLib's <c>DecreasingGaussianWalk</c>.
/// </summary>
/// <remarks>
/// δ squares at each round's first walk from the second round, so the fireflies of round r walk at δ₀^(2^(r − 1)).
/// QuantLib's counts its walks and restarts the count at 0 on the call that squares, itself the next round's first
/// walk, so δ squares once every Mfa + 1 walks, at a drifting place within the round, and with one firefly every second
/// round (<c>Divergences/FireflyAlgorithmTests.cs</c>). Each minimization starts again from the given δ.
/// </remarks>
[Experimental("NQL0001")]
public sealed class DecreasingGaussianWalk : GaussianWalk
{
    private readonly double delta0;
    private int iteration;

    /// <summary>Initializes a new instance of the <see cref="DecreasingGaussianWalk"/> class.</summary>
    /// <param name="sigma">The radius's standard deviation σ.</param>
    /// <param name="delta">The step's initial scale δ, QuantLib's default 0.9.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public DecreasingGaussianWalk(double sigma, double delta, long seed)
        : base(sigma, delta, seed)
    {
        delta0 = delta;
    }

    /// <inheritdoc/>
    public override FireflyAlgorithmRandomWalk Clone() => new DecreasingGaussianWalk(Sigma, delta0, Seed);

    /// <inheritdoc/>
    protected override void WalkImpl(Vector xRW)
    {
        iteration++;
        if (iteration > Mfa)
        {
            // every time all the fireflies have been processed, this walk the next round's first
            iteration = 1;
            Delta *= Delta;
        }

        base.WalkImpl(xRW);
    }

    /// <inheritdoc/>
    protected override void Init()
    {
        base.Init();
        iteration = 0;
        Delta = delta0;
    }
}
