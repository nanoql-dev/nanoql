// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root process's stationary density past α ≈ 170 (the S-hes plan's HR7 (xiii), under R8). QuantLib computes
/// β^α·v^{α−1}·e^{−βv−lnΓ(α)} (<c>squarerootprocessrndcalculator.cpp:71</c>), whose first factor overflows and the
/// product is NaN from α of about 170 (σ ≤ 0.02 at κ 2, θ 0.04). nanoql computes the same density in logarithms,
/// exp(α ln β + (α − 1) ln v − βv − lnΓ(α)), where the product isn't finite, and QuantLib's product where it is. The
/// check: Boost's gamma density, the stationary law being Gamma(α, θ/α), through the harness
/// (<c>tools/cxx/src/boost/math/distributions/gamma.cpp</c>); QuantLib's bits where the product is finite.
/// </summary>
public class SquareRootStationaryDensityTests
{
    [Fact]
    public void PastTheOverflowTheDensityIsBoostsGammaDensity()
    {
        // QuantLib 1.44 returns NaN for each [executed on the wheel]; Boost 1.90's gamma_distribution pdf at shape
        // alpha = 2 kappa theta / sigma^2 and scale theta / alpha [executed through the harness's gamma::pdf]
        foreach ((double kappa, double theta, double sigma, double v, double boost) in new[]
                 {
                     (2.0, 0.04, 0.02, 0.032, 0.02378225077120718),
                     (2.0, 0.04, 0.02, 0.04, 199.4295880503304),
                     (2.0, 0.04, 0.02, 0.044000000000000004, 27.77740648379206),
                     (2.0, 0.04, 0.015, 0.032, 2.36710331192213e-05),
                     (2.0, 0.04, 0.015, 0.04, 265.93035473016107),
                     (2.0, 0.04, 0.015, 0.044000000000000004, 8.610143802624133),
                     (1.0, 0.09, 0.01, 0.072, 1.9018829556848894e-16),
                     (1.0, 0.09, 0.01, 0.09, 188.05448808817198),
                     (1.0, 0.09, 0.01, 0.099, 0.03687377147069516),
                 })
        {
            double calculated = new SquareRootProcessRNDCalculator(0.04, kappa, theta, sigma).StationaryPdf(v);
            Assert.True(Math.Abs((calculated / boost) - 1.0) < 1e-10, $"kappa {kappa}, sigma {sigma}, v {v}: {calculated} against {boost}");
        }
    }

    [Fact]
    public void WhereTheProductIsFiniteTheDensityIsQuantLibs()
    {
        // QuantLib 1.44's stationary_pdf at v0 0.17, kappa 1, theta 0.09, sigma 0.5 [executed on the wheel]
        var calculator = new SquareRootProcessRNDCalculator(0.17, 1.0, 0.09, 0.5);
        Assert.Equal(11.81795976103558, calculator.StationaryPdf(0.01));
        Assert.Equal(3.3682640952190015, calculator.StationaryPdf(0.09));
        Assert.Equal(0.4481107448294674, calculator.StationaryPdf(0.3));
    }
}
