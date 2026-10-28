// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CdoBuilders;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The random models' statistics where QuantLib's misread their paths, each checked on the paths themselves or
/// against an exact model; QuantLib's values are the S-xcr run's <c>c2_facts.cpp</c>'s:
/// <list type="bullet">
/// <item>the default correlation, Pearson's on the paths (the S-xcr plan's XR8 (xxiv)): QuantLib divides the joint
/// frequency by n − 1, a name with itself reading 1.0002212831946919 on 5,000 paths;</item>
/// <item>the random loss model's recovery of 256/256 (XR8 (xxv)): QuantLib's 8 bits hold it as 0, a full loss, reading
/// an expected loss of 5.775 at recoveries of 0.9995;</item>
/// <item>the horizon, a parameter (XR8 (xliv)): QuantLib's 4,050 days stop the expected loss of ten names at 3% and
/// correlation 0.3 at 16.9836 at 12 and 20 years, where its recursive model reads 18.149740938526683 and
/// 27.0848313951936;</item>
/// <item>the n-th event, every default of a day counted: QuantLib keys its order by day, so on the random-default
/// vector's whole pool the n-th event's probabilities sum to 0.1685 and 0.071 where at least 4 and 5 events read 0.169
/// and 0.072.</item>
/// </list>
/// </summary>
public class RandomModelStatisticsTests
{
    [Fact]
    public void TheCorrelationIsThePathsPearsons()
    {
        Basket basket = Three(0.02).SetLossModel(Random(3, 0.3, 5000));

        Assert.Equal(1.0, basket.DefaultCorrelation(D5, 0, 0), 1e-14);
        Assert.Equal(basket.DefaultCorrelation(D5, 0, 1), basket.DefaultCorrelation(D5, 1, 0));
        Assert.True(basket.DefaultCorrelation(D5, 0, 1) < 0.12729351990524623);
    }

    [Fact]
    public void AFullRecoveryLosesNothing()
    {
        // every simulated recovery rounds to 256/256 at 0.9995 and A = 10: no loss, QuantLib's 5.775
        double[][] w = [.. Enumerable.Repeat(new[] { 0.5 }, 6)];
        var spot = new GaussianSpotLossLM(w, [0.9995, 0.9995, 0.9995], 10.0, LatentModelIntegrationType.GaussianQuadrature);
        Basket basket = Three(0.02).SetLossModel(new GaussianRandomLossLM(spot, 2000, 2863311530));

        Assert.Equal(0.0, basket.ExpectedTrancheLoss(D5));
        Assert.Equal(0.242, basket.ProbAtLeastNEvents(1, D5), 1e-15);
    }

    [Fact]
    public void TheHorizonIsAParameter()
    {
        Basket basket = Ten(4050);
        Basket longer = Ten(8000);
        Date twelve = Today + new Period(12, TimeUnit.Years);
        Date twenty = Today + new Period(20, TimeUnit.Years);

        Assert.Equal(8.328, basket.ExpectedTrancheLoss(Today + new Period(5, TimeUnit.Years)), 1e-12);
        Assert.Throws<ArgumentException>(() => basket.ExpectedTrancheLoss(twelve));
        var model = (GaussianRandomDefaultLM)longer.LossModel!;
        (double m12, double h12) = model.ExpectedTrancheLossInterval(twelve, 0.9973);
        (double m20, double h20) = model.ExpectedTrancheLossInterval(twenty, 0.9973);
        Assert.Equal(18.149740938526683, m12, h12);
        Assert.Equal(27.0848313951936, m20, h20);
    }

    [Fact]
    public void TheNthEventCountsEveryDefaultOfADay()
    {
        DefaultProbabilityTermStructure[] curves = [.. new[] { 0.03, 0.05, 0.02, 0.04, 0.06 }.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))];
        string[] names = ["Name0", "Name1", "Name2", "Name3", "Name4"];
        Basket basket = new Basket(Today, names, [10.0, 20.0, 30.0, 15.0, 25.0], PoolOf(names, curves), Context)
            .SetLossModel(Random(5, 0.6, 2000));
        Date d10 = Today + 3653;

        for (int n = 1; n <= 5; n++)
        {
            Assert.Equal(basket.ProbAtLeastNEvents(n, d10), basket.ProbsBeingNthEvent(n, d10).Sum(), 1e-14);
        }
    }

    private static Basket Three(double h)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, h), Flat(Today, h), Flat(Today, h)];
        return new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
    }

    private static Basket Ten(int horizon)
    {
        string[] names = [.. Enumerable.Range(0, 10).Select(i => "N" + i)];
        DefaultProbabilityTermStructure[] curves = [.. names.Select(_ => (DefaultProbabilityTermStructure)Flat(Today, 0.03))];
        return new Basket(Today, names, Enumerable.Repeat(10.0, 10), PoolOf(names, curves), Context)
            .SetLossModel(Random(10, 0.3, 5000, horizon));
    }
}
