// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The recursive model's percentile and loss distribution, a model set on two baskets and a basket with a realized
/// default. QuantLib's percentile at 0 and 1 returns the distribution's probability, 0.66930409955890324 and
/// 1.0000000003070473 on five names at five years; on two names of notionals 10 and 30 it labels the attainable losses
/// 0, 6, 18 and 24 as 0, 6, 12 and 18, their cumulatives 0.79243383385445709, 0.86063723644644652, 0.9729912416586074
/// and 1.0000000003070473; a model set on a basket of notionals 10, 20, 30 and then on one of 30, 20, 15 at hazard 0.05
/// reads 11.950518324119658 where a fresh model reads 7.9670122160797714. The cumulatives integrate a vector over the
/// factor, where QuantLib's quadrature leaves its last node unweighted, so they agree to that node's weight.
/// </summary>
public class RecursiveLossModelTests
{
    private static readonly string[] Abc = ["A", "B", "C"];

    [Fact]
    public void ThePercentileEndsAreLosses()
    {
        Basket basket = Five().SetLossModel(new RecursiveGaussLossModel(Latent(5, 0.4)));

        // QuantLib: 0.66930409955890324 and 1.0000000003070473, probabilities
        Assert.Equal(0.0, basket.Percentile(D5, 0.0));
        Assert.Equal(30.0, basket.Percentile(D5, 1.0), 1e-12);
    }

    [Fact]
    public void TheDistributionIsLabelledByItsAttainableLosses()
    {
        string[] names = ["N0", "N1"];
        var basket = new Basket(Today, names, [10.0, 30.0], PoolOf(names, [Flat(Today, 0.02), Flat(Today, 0.03)]), Context);
        ImmutableSortedDictionary<double, double> dist = basket.SetLossModel(new RecursiveGaussLossModel(Latent(2, 0.4))).LossDistribution(D5);

        Assert.Equal([0.0, 6.0, 18.0, 24.0], dist.Keys.ToArray());
        double[] quantLibs = [0.79243383385445709, 0.86063723644644652, 0.9729912416586074, 1.0000000003070473];
        double[] probs = [.. dist.Values];
        for (int i = 0; i < probs.Length; i++)
        {
            Assert.Equal(quantLibs[i], probs[i], 1e-8);
        }
    }

    [Fact]
    public void AModelSetOnTwoBasketsWeighsEach()
    {
        RecursiveGaussLossModel model = new(Latent(3, 0.4));
        DefaultProbabilityTermStructure[] low = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        DefaultProbabilityTermStructure[] high = [Flat(Today, 0.05), Flat(Today, 0.05), Flat(Today, 0.05)];
        double first = new Basket(Today, Abc, [10.0, 20.0, 30.0], PoolOf(Abc, low), Context).SetLossModel(model).ExpectedTrancheLoss(D5);
        double second = new Basket(Today, Abc, [30.0, 20.0, 15.0], PoolOf(Abc, high), Context).SetLossModel(model).ExpectedTrancheLoss(D5);

        // QuantLib: 3.4276377936484597, then 11.950518324119658 for a fresh model's 7.9670122160797714
        Assert.Equal(3.4276377936484597, first, 1e-12);
        Assert.Equal(7.9670122160797714, second, 1e-12);
    }

    [Fact]
    public void ARealizedDefaultLeavesTheLiveNames()
    {
        // A (10) defaulted in June 2006 at 0.25, B (20) and C (30) alive, [10%, 50%] from 1 January 2006: the
        // restated basket is B and C over [0, 45%], the realized tranche loss 1.5
        var inception = new Date(1, January, 2006);
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.03), Flat(Today, 0.015)];
        var a = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, July, 2006), 0.25);
        var realized = new Basket(inception, Abc, [10.0, 20.0, 30.0], PoolOf(Abc, curves, [[a], null, null]), Context, 0.1, 0.5);
        string[] bc = ["B", "C"];
        var restated = new Basket(inception, bc, [20.0, 30.0], PoolOf(bc, [Flat(Today, 0.03), Flat(Today, 0.015)]), Context, 0.0, 0.45);

        double onRealized = realized.SetLossModel(new RecursiveGaussLossModel(new GaussianConstantLossLM(0.3, [0.3, 0.5, 0.6], LatentModelIntegrationType.GaussianQuadrature, 3))).ExpectedTrancheLoss(D5);
        double onRestated = restated.SetLossModel(new RecursiveGaussLossModel(new GaussianConstantLossLM(0.3, [0.5, 0.6], LatentModelIntegrationType.GaussianQuadrature, 2))).ExpectedTrancheLoss(D5);
        Assert.Equal(onRestated + 1.5, onRealized, 1e-13);
    }

    [Theory]
    [InlineData(0.0, 0.3, 15.0)]
    [InlineData(0.1, 0.3, 10.0)]
    public void ThePercentileAtOneIsATrancheLoss(double a, double d, double width)
    {
        // QuantLib's recursive model reads 1.0000000003070473 at 1, its binomial the pool's 29.999999999976612; just
        // below 1, the tranche's width
        Basket basket = FiveNames(a, d).SetLossModel(new RecursiveGaussLossModel(Latent04()));
        Assert.Equal(width, basket.Percentile(D5, 1.0), 1e-12);
        Assert.Equal(width, basket.Percentile(D5, 0.999999), 1e-9);
    }

    [Fact]
    public void AShortfallTheDistributionNeverReachesIsRefused()
    {
        // QuantLib's reads 0, its comment "well, we are in error.... fix: FAIL"
        Basket basket = FiveNames(0.0, 1.0).SetLossModel(new RecursiveGaussLossModel(Latent04()));
        Assert.Throws<NanoQLException>(() => basket.ExpectedShortfall(D5, 1.0));
    }

    // five names of 10 at hazards 0.01 to 0.03, recovery 0.4, correlation 0.4, over a tranche
    private static Basket FiveNames(double a, double d)
    {
        string[] names = ["N0", "N1", "N2", "N3", "N4"];
        DefaultProbabilityTermStructure[] curves = [.. new[] { 0.01, 0.015, 0.02, 0.025, 0.03 }.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))];
        return new Basket(Today, names, Enumerable.Repeat(10.0, 5), PoolOf(names, curves), Context, a, d);
    }

    private static GaussianConstantLossLM Latent04() =>
        new(0.4, [.. Enumerable.Repeat(0.4, 5)], LatentModelIntegrationType.GaussianQuadrature, 5);

    private static Basket Five()
    {
        string[] names = ["N0", "N1", "N2", "N3", "N4"];
        DefaultProbabilityTermStructure[] curves = [.. new[] { 0.01, 0.03, 0.02, 0.015, 0.025 }.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))];
        return new Basket(Today, names, Enumerable.Repeat(10.0, 5), PoolOf(names, curves), Context);
    }

    private static GaussianConstantLossLM Latent(int n, double recovery) =>
        new(0.3, [.. Enumerable.Repeat(recovery, n)], LatentModelIntegrationType.GaussianQuadrature, n);
}
