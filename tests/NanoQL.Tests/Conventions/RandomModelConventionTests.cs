// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CdoBuilders;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The simulation models' and the CDO's own rules (the S-xcr plan's XR4 (a), XR8 (xxix), (xxxiv), (xliv), (xlv)): the
/// path count, the seed and the horizon required within their ranges, a statistic past the horizon or reading past
/// the sorted losses refused, the spot model's parameter of 0 refused, and the CDO's engines refusing a basket of
/// another evaluation date; each refusal's type and parameter.
/// </summary>
public class RandomModelConventionTests
{
    [Fact]
    public void ThePathsTheSeedAndTheHorizonAreRequired()
    {
        // QuantLib defaults to no path ("stats will crash on div by zero, FIX ME") and seeds 2863311530
        var lm = new GaussianConstantLossLM(0.3, [0.4, 0.4, 0.4], LatentModelIntegrationType.GaussianQuadrature, 3);

        Assert.Equal("nSims", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianRandomDefaultLM(lm, 0, 42)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianRandomDefaultLM(lm, 100, 0)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianRandomDefaultLM(lm, 100, 4294967296)).ParamName);
        Assert.Equal("maxHorizon", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianRandomDefaultLM(lm, 100, 42, maxHorizon: 0)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => new LossDistMonteCarlo(10, 1.0, 100, 0)).ParamName);
    }

    [Fact]
    public void AStatisticPastTheHorizonIsRefused()
    {
        Basket basket = Three().SetLossModel(Random(3, 0.3, 500, maxHorizon: 365));

        Assert.Equal(0.0, basket.ExpectedTrancheLoss(Today));
        _ = basket.ProbAtLeastNEvents(1, Today + 365);
        Assert.Equal("d", Assert.Throws<ArgumentException>(() => basket.ProbAtLeastNEvents(1, Today + 366)).ParamName);
        Assert.Equal("d", Assert.Throws<ArgumentException>(() => basket.ProbAtLeastNEvents(1, Today)).ParamName);
    }

    [Fact]
    public void AReadPastTheSortedLossesIsRefused()
    {
        // on 500 paths: the shortfall at 0.999 reads position 500, the percentile at 1 position 500
        Basket basket = Three().SetLossModel(Random(3, 0.3, 500));
        var model = (GaussianRandomDefaultLM)basket.LossModel!;

        Assert.Equal("percent", Assert.Throws<ArgumentException>(() => basket.ExpectedShortfall(D5, 0.999)).ParamName);
        _ = basket.ExpectedShortfall(D5, 0.997);
        Assert.Equal("percentile", Assert.Throws<ArgumentException>(() => basket.Percentile(D5, 1.0)).ParamName);
        (double value, double lower, _) = model.PercentileAndInterval(D5, 0.0);
        Assert.Equal(value, lower);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => basket.ProbsBeingNthEvent(4, D5)).ParamName);
    }

    [Fact]
    public void TheSpotModelsParameterIsntZero()
    {
        // QuantLib's conditional recovery divides by it
        double[][] w = [.. Enumerable.Repeat(new[] { 0.5 }, 6)];

        Assert.Equal("modelA", Assert.Throws<ArgumentException>(() =>
            new GaussianSpotLossLM(w, [0.4, 0.4, 0.4], 0.0, LatentModelIntegrationType.GaussianQuadrature)).ParamName);
        Assert.Throws<ArgumentException>(() => new GaussianSpotLossLM(w[..5], [0.4, 0.4, 0.4], 1.0, LatentModelIntegrationType.GaussianQuadrature));
    }

    [Fact]
    public void ACdoEngineRefusesABasketOfAnotherEvaluationDate()
    {
        SyntheticCDO cdo = Cdo(EightNames(0.03, 0.07).SetLossModel(Pool(8, 0.3)), ProtectionSide.Seller, 0.0, 0.02);
        var later = new EvaluationContext { EvaluationDate = Today + 1 };

        Assert.Throws<NanoQLException>(() => MidPointCDOEngine.Calculate(cdo, later, Discount()));
        Assert.Throws<NanoQLException>(() => IntegralCDOEngine.Calculate(cdo, later, Discount()));
        // a schedule starting before the basket's inception: "Basket did not exist before contract start."
        Schedule early = new MakeSchedule().From(Today - 30).To(D5).WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        Assert.Equal("basket", Assert.Throws<ArgumentException>(() => Cdo(cdo.Basket, ProtectionSide.Seller, 0.0, 0.02, schedule: early)).ParamName);
    }

    private static Basket Three()
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        return new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
    }
}
