// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// A Levy flight random walk: the step's radius from a Levy flight distribution, its direction isotropic, scaled by δ:
/// QuantLib's <c>LevyFlightWalk</c>.
/// </summary>
[Experimental("NQL0001")]
public sealed class LevyFlightWalk : DistributionRandomWalk<LevyFlightDistribution>
{
    /// <summary>Initializes a new instance of the <see cref="LevyFlightWalk"/> class.</summary>
    /// <param name="alpha">The distribution's exponent α, positive.</param>
    /// <param name="xm">The distribution's lower end x_m, QuantLib's default 0.5.</param>
    /// <param name="delta">The step's scale δ, QuantLib's default 0.9.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException"><paramref name="alpha"/> isn't positive, with QuantLib's text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public LevyFlightWalk(double alpha, double xm, double delta, long seed)
        : base(new LevyFlightDistribution(xm, alpha), delta, seed)
    {
    }

    /// <inheritdoc/>
    public override FireflyAlgorithmRandomWalk Clone() => new LevyFlightWalk(Distribution.Alpha, Distribution.Xm, Delta, Seed);
}
