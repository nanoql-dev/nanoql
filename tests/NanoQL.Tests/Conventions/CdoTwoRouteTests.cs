// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using static NanoQL.Tests.Conventions.CdoBuilders;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Two routes to one value for the CDOs: the mid-point engine against the integral one; the homogeneous pool model
/// against the inhomogeneous one on an equal pool; the latent model's quadrature against the random default model; and
/// the LHP model's implied correlation recovering the correlation that priced the CDO.
/// </summary>
public class CdoTwoRouteTests
{
    [Fact]
    public void TheMidPointAndIntegralEnginesAgree()
    {
        // QuantLib's two read 0.10676 and 0.10609 on this tranche: the mid-point's default dates against the steps'
        SyntheticCDO cdo = Cdo(EightNames(0.03, 0.07).SetLossModel(Pool(8, 0.3)), ProtectionSide.Seller, 0.0, 0.02);

        double mid = Mid(cdo).FairPremium!.Value;
        double integral = Integral(cdo).FairPremium!.Value;
        Assert.Equal(mid, integral, 0.01 * mid);
    }

    [Fact]
    public void TheHomogeneousAndInhomogeneousModelsAgreeOnAnEqualPool()
    {
        // exact convolution against Hull-White bucketing, each on 100 buckets
        Basket basket = EightNames(0.03, 0.07);
        var lm = new GaussianConstantLossLM(0.3, [.. Enumerable.Repeat(0.4, 8)], LatentModelIntegrationType.GaussianQuadrature, 8);

        double homogeneous = basket.SetLossModel(new HomogGaussPoolLossModel(lm, 100, 5.0, -5.0, 15)).ExpectedTrancheLoss(D5);
        double inhomogeneous = basket.SetLossModel(new IHGaussPoolLossModel(lm, 100, 5.0, -5.0, 15)).ExpectedTrancheLoss(D5);
        Assert.Equal(homogeneous, inhomogeneous, 0.01 * homogeneous);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheQuadratureAndTheSimulationAgree(int n)
    {
        // the constant-loss model integrates the event count; the random model counts it on 5,000 paths
        Basket basket = EightNames(0.0, 1.0);
        double quadrature = basket.SetLossModel(Constant(8, 0.3)).ProbAtLeastNEvents(n, D5);
        double simulated = basket.SetLossModel(Random(8, 0.3, 5000)).ProbAtLeastNEvents(n, D5);

        Assert.Equal(quadrature, simulated, 3 * Math.Sqrt(quadrature * (1 - quadrature) / 5000));
    }

    [Fact]
    public void TheImpliedCorrelationIsThePricingOne()
    {
        // a CDO at the LHP model's value at 0.3, its implied correlation solved back
        Basket basket = EightNames(0.03, 0.07);
        double[] recoveries = [.. Enumerable.Repeat(0.4, 8)];
        SyntheticCDO cdo = Cdo(basket.SetLossModel(Pool(8, 0.3)), ProtectionSide.Seller, 0.0, 0.02);
        double target = Mid(Cdo(basket.SetLossModel(new GaussianLHPLossModel(0.3, recoveries)), ProtectionSide.Seller, 0.0, 0.02)).NPV!.Value;

        Assert.Equal(0.3, cdo.ImplicitCorrelation(recoveries, Discount(), Context, target, 1e-8), 1e-6);
        Assert.IsType<IHGaussPoolLossModel>(cdo.Basket.LossModel);
        Assert.Null(new GaussianLHPLossModel(0.3, recoveries).Basket);
    }
}
