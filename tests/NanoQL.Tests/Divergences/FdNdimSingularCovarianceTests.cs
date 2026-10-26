// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The n-dimensional finite-difference basket engine on a singular covariance. QuantLib meshes each principal
/// direction over 1.3√λ·√T standard normal quantiles (<c>fdndimblackscholesvanillaengine.cpp:144-163</c>): an
/// eigenvalue of 0 (a correlation of 1, a zero volatility) meshes its direction at one point, which its spline refuses
/// ("Dimension 1 : invalid data"), and a rounding negative eigenvalue meshes NaN, while its price is continuous towards
/// the singular case (an eigenvalue of 1e-10 prices 10.3739, its Monte Carlo engine 10.3459 ± 0.0506 at ρ = 1). nanoql
/// floors the mesh's eigenvalue at the largest one's times machine epsilon, the operator keeping QuantLib's (the S-exo
/// plan's ER5 (vi)): QuantLib's own value at near-singular covariances and the Monte Carlo engine are the checks.
/// </summary>
public class FdNdimSingularCovarianceTests
{
    [Fact]
    public void ACorrelationOfOnePricesTheLimit()
    {
        // QuantLib's engine at ρ = 1 − 1e-10 and 1 − 1e-12: 10.373940605520879 and 10.373940605770496; at 1 it refuses;
        // its Monte Carlo engine at 1 (seed 42, 65535 samples) 10.345930387662095 ± 0.05055059956281837
        double price = Price(Process(95.0, Flat(0.03), Flat(0.05), Vol(0.3)), 1.0);
        Assert.Equal(10.373940605770496, price, 1e-8);
        Assert.InRange(price, 10.345930387662095 - (3 * 0.05055059956281837), 10.345930387662095 + (3 * 0.05055059956281837));
    }

    [Fact]
    public void AZeroVolatilityPricesTheLimit()
    {
        // QuantLib's engine at a second volatility of 1e-5 and 1e-8: 4.879726403556374 and 4.879635818467327; at 0 it
        // refuses
        Assert.Equal(4.879635818467327, Price(Process(95.0, Flat(0.03), Flat(0.05), Vol(0.0)), 0.5), 1e-6);
    }

    private static double Price(GeneralizedBlackScholesProcess p2, double correlation)
    {
        var rho = new Matrix(2, 2, 1.0);
        rho[0, 1] = rho[1, 0] = correlation;
        return FdndimBlackScholesVanillaEngine.Calculate(
            new BasketOption(AveragePut(), European()),
            new EvaluationContext { EvaluationDate = new Time.Date(15, Time.Month.October, 2026) },
            [Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25)), p2],
            rho,
            31,
            25,
            0,
            Methods.FdmSchemeDesc.Douglas()).NPV!.Value;
    }
}
