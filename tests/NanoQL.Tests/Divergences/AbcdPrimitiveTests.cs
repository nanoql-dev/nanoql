// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The abcd function's primitive at a small c: QuantLib's closed form divides by powers of c and cancels, so the
/// covariance of a = 0.1, b = 0.2, d = 0.15 over the first year for fixings at 2 and 3 years reads, on the wheel,
/// 0.41570091639483625 at c = 1e-4, 0.4112554826093383 at 1e-5, 0.26249003480977007 at 1e-6 and −2220.182821037571 at
/// 1e-7, where its Gauss-Lobatto integral is 0.4157007, 0.4158201, 0.4158320 and 0.41583320. nanoql switches to a
/// cancellation-free series where QuantLib's form cancels; the check is the integral of the covariance's integrand,
/// <see cref="AbcdSquared"/>, by Gauss-Lobatto, the <c>integrals</c> cases' route, and QuantLib's bits where it
/// doesn't cancel.
/// </summary>
public class AbcdPrimitiveTests
{
    [Fact]
    public void ASmallCsCovarianceIsItsIntegral()
    {
        var integral = new GaussLobattoIntegral(10000, 1e-14);
        foreach (double c in new[] { 1e-4, 1e-5, 1e-6, 1e-7 })
        {
            double expected = integral.Integrate(new AbcdSquared(0.1, 0.2, c, 0.15, 2.0, 3.0).Evaluate, 0.0, 1.0);

            Assert.Equal(expected, new AbcdFunction(0.1, 0.2, c, 0.15).Covariance(0.0, 1.0, 2.0, 3.0), 1e-12);
        }

        // the wheel's, at c = 0.54
        Assert.Equal(0.09950017367232834, new AbcdFunction(0.1, 0.2, 0.54, 0.15).Covariance(0.0, 1.0, 2.0, 3.0));
    }
}
