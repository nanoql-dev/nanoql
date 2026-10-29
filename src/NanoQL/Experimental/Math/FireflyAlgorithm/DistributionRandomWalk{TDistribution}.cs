// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A random walk whose step's radius is drawn from a distribution, its direction isotropic and its sphere an
/// ellipsoid over the problem's bounds, scaled by δ: QuantLib's <c>DistributionRandomWalk</c>.
/// </summary>
/// <remarks>
/// Its isotropic walk draws the radius on a <c>std::mt19937</c> and the direction on a Mersenne Twister, both on the
/// seed, which is required: QuantLib's default seeds from the clock (the S-xrt plan's XRD3).
/// </remarks>
/// <typeparam name="TDistribution">The radius's distribution.</typeparam>
[Experimental("NQL0001")]
public class DistributionRandomWalk<TDistribution> : FireflyAlgorithmRandomWalk
    where TDistribution : class, IRandomWalkDistribution
{
    private readonly TDistribution distribution;
    private readonly long seed;
    private readonly IsotropicRandomWalk<TDistribution> walkRandom;

    /// <summary>Initializes a new instance of the <see cref="DistributionRandomWalk{TDistribution}"/> class.</summary>
    /// <param name="dist">The radius's distribution.</param>
    /// <param name="delta">The step's scale δ, QuantLib's default 0.9.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public DistributionRandomWalk(TDistribution dist, double delta, long seed)
    {
        ArgumentNullException.ThrowIfNull(dist);
        distribution = dist;
        this.seed = seed;
        walkRandom = new IsotropicRandomWalk<TDistribution>(new MersenneTwisterUniformRng(seed), dist, 1, new Vector(1, 1.0), seed);
        Delta = delta;
    }

    /// <summary>Gets or sets the step's scale δ, QuantLib's <c>delta_</c>, which a derived walk may change.</summary>
    protected double Delta { get; set; }

    /// <summary>Gets the distribution.</summary>
    protected TDistribution Distribution => distribution;

    /// <summary>Gets the seed.</summary>
    protected long Seed => seed;

    /// <inheritdoc/>
    public override FireflyAlgorithmRandomWalk Clone() => new DistributionRandomWalk<TDistribution>(distribution, Delta, seed);

    /// <inheritdoc/>
    /// <remarks>The isotropic walk's step, times δ.</remarks>
    protected override void WalkImpl(Vector xRW)
    {
        ArgumentNullException.ThrowIfNull(xRW);
        walkRandom.NextReal(xRW.AsSpan());
        xRW *= Delta;
    }

    /// <inheritdoc/>
    /// <remarks>The isotropic walk's dimension and ellipsoid set from the problem's bounds.</remarks>
    protected override void Init() => walkRandom.SetDimension(N, LowerBound, UpperBound);
}
