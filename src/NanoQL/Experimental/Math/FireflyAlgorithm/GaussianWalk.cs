// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// A Gaussian random walk: the step's radius normal with standard deviation σ, its direction isotropic and its sphere
/// an ellipsoid over the problem's bounds, scaled by δ: QuantLib's <c>GaussianWalk</c>.
/// </summary>
/// <remarks>
/// The radius is <c>std::normal_distribution&lt;Real&gt;(0.0, σ)</c> on a <c>std::mt19937</c>, drawn as MSVC draws it
/// (<see cref="NormalRandomWalkDistribution"/>, the S-xrt plan's XRD4), and the direction on a Mersenne Twister, both on
/// the seed, which is required: QuantLib's default seeds from the clock (XRD3).
/// </remarks>
[Experimental("NQL0001")]
public class GaussianWalk : DistributionRandomWalk<NormalRandomWalkDistribution>
{
    /// <summary>Initializes a new instance of the <see cref="GaussianWalk"/> class.</summary>
    /// <param name="sigma">The radius's standard deviation σ.</param>
    /// <param name="delta">The step's scale δ, QuantLib's default 0.9.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public GaussianWalk(double sigma, double delta, long seed)
        : base(new NormalRandomWalkDistribution(0.0, sigma), delta, seed)
    {
    }

    /// <summary>Gets the radius's standard deviation σ.</summary>
    protected double Sigma => Distribution.Sigma;

    /// <inheritdoc/>
    public override FireflyAlgorithmRandomWalk Clone() => new GaussianWalk(Sigma, Delta, Seed);
}
