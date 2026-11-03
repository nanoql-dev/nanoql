// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// West's bivariate normal at an infinite bound. QuantLib's integrand meets ∞ − ∞ there
/// (<c>bivariatenormaldistribution.cpp:166-176</c>, <c>eqn3</c>'s exponent, and <c>eqn6</c>'s beyond |ρ| = 0.925): NaN
/// at +∞ for ρ &gt; 0 and at −∞ for ρ &lt; 0, and wherever the other bound is 0, where Drezner's returns Φ(b) or 0.
/// nanoql returns the other bound's Φ at +∞ and 0 at −∞, in either argument: QuantLib's own value at ±40 is the check.
/// </summary>
public class BivariateInfiniteArgumentTests
{
    private static readonly double[] Rhos = [-0.95, -0.5, 0.0, 0.5, 0.95];
    private static readonly double[] Bounds = [-1.0, 0.0, 1.0];
    private static readonly CumulativeNormalDistribution Phi = new();

    [Fact]
    public void APlusInfiniteBoundIsTheOtherBoundsMarginal()
    {
        foreach (double rho in Rhos)
        {
            var bvn = new BivariateCumulativeNormalDistributionWe04DP(rho);
            foreach (double b in Bounds)
            {
                Assert.Equal(Phi.Evaluate(b), bvn.Evaluate(double.PositiveInfinity, b));
                Assert.Equal(Phi.Evaluate(b), bvn.Evaluate(b, double.PositiveInfinity));
                Assert.Equal(bvn.Evaluate(40.0, b), bvn.Evaluate(double.PositiveInfinity, b), 1e-15);
            }

            Assert.Equal(1.0, bvn.Evaluate(double.PositiveInfinity, double.PositiveInfinity));
        }
    }

    [Fact]
    public void AMinusInfiniteBoundIsNothing()
    {
        foreach (double rho in Rhos)
        {
            var bvn = new BivariateCumulativeNormalDistributionWe04DP(rho);
            foreach (double b in Bounds)
            {
                Assert.Equal(0.0, bvn.Evaluate(double.NegativeInfinity, b));
                Assert.Equal(0.0, bvn.Evaluate(b, double.NegativeInfinity));
                Assert.Equal(bvn.Evaluate(-40.0, b), bvn.Evaluate(double.NegativeInfinity, b), 1e-15);
            }
        }
    }
}
