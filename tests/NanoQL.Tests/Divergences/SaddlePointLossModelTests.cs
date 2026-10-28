// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The saddle-point model's tail reads (the S-xcr plan's XR8 (xxiii), (xlvii)), on pools of n names at hazard 0.03,
/// recovery 0.4 and notional 1 (the S-xcr run's <c>c3_facts.cpp</c> and <c>c3_saddle_pct.cpp</c>):
/// <list type="bullet">
/// <item>Above the whole tranche the probability is 0: on 100 names over [0, 30%] QuantLib's reads 0.0016176810243568769
/// at a fraction of 1.5 (it compares the fraction with the detachment amount); and the shortfall is at most the
/// tranche's remaining notional, 30, where QuantLib's caps it at the tranche notional times the remaining ratio,
/// 9.0000000000000302.</item>
/// <item>Where a factor node's names are all at 0 or 1, QuantLib's saddle search throws ("invalid range"): at the
/// evaluation date, and on 100 names from correlation 0.8 at five years, 0.85 from three months, 0.9 from a month and
/// 0.95 throughout; its tail elsewhere reads the exact (recursive) model's within 1% at 10% and 7% to 8% at 1% a year
/// out (0.2165 for 0.2021 at 0.7, 0.1899 for 0.1755 at 0.75). Here those nodes are exact, the tail within 2% at 10%
/// through correlation 0.9 and 5% at 0.95 (the run's stop 2, ruled on 2026-10-28), and 10% at 1%, of the exact
/// one.</item>
/// <item>A tail outside [0, 1] is refused: on ten names, 26.815515432090283 a day out at correlation 0.548 and
/// 2.6118595103824398 at a week at 0.3, where the exact ones are 0.00073 and 0.0055.</item>
/// <item>The percentile reads the exact distribution's within its loss unit where single nodes leave [0, 1] (the
/// run's stop 1, ruled on 2026-10-28): on 100 names over the whole pool, QuantLib's 4.9485835 at 90% a year out at
/// correlation 0.3, 55.627361 at 99% ten years out at 0.5 and 0.064772838 at 50% 30 days out at 0.1, the exact 4.8,
/// 55.8 and 0 at its loss unit 0.6; its search solves below QuantLib's bracket (stop 3), so it isn't QuantLib's to the
/// digit.</item>
/// <item>Where the level falls outside the bracket QuantLib places on its name of largest loss, it returns the
/// bracket's end (the run's stop 3, ruled on 2026-10-28; the S-xcr code review's C3 O1-3): on 95 names at hazard 0.03
/// and notional 1 with five at 0.001 and 3, it reads 0.97841938 at 1% a year out at correlation 0.7 and 0.20082286 at
/// 10% 30 days out at 0.9, and throws a year out at 0.9, where the exact tails are 0.198289, 0.00494071 and 0.0564909;
/// here the bracket widens and the point is solved.</item>
/// <item>Above the largest loss a factor node reaches, the widened search's end reads 0/0 in the density and the
/// conditional shortfall, so a tranche detaching above the pool's largest loss read NaN (the S-xcr code review's C3
/// O2-1); here both read 0 there, as the tail does, and the tranche reads its shortfall detaching at the largest loss.
/// The split's contributions read ∞/∞ at a widened point on QuantLib's form; here they take the cumulants' treatment
/// and add up to the loss (O2-2).</item>
/// </list>
/// </summary>
public class SaddlePointLossModelTests
{
    [Fact]
    public void NoTrancheLossExceedsTheTranche()
    {
        Basket basket = Pool(100, 0.3, 0.0, 0.3);

        // QuantLib: 0.0016176810243568769 at 1.5, and the shortfall capped at 9.0000000000000302
        Assert.Equal(0.0, basket.ProbOverLoss(D5, 1.5));
        Assert.Equal(30.0, basket.Percentile(D5, 0.99), 1e-9);
        Assert.Equal(30.0, basket.ExpectedShortfall(D5, 0.99), 1e-9);
    }

    [Theory]
    [InlineData(0.0, 0.3, 0.9, 25.285174391083832, 9.0000000000000302)]
    [InlineData(0.1, 0.3, 0.9, 15.285174360379145, 4.0000000000000275)]
    [InlineData(0.15, 0.4, 0.99, 23.719998340632571, 6.2499999999999494)]
    public void ATranchesShortfallIsItsTrancheLoss(double a, double d, double p, double exact, double quantLibs)
    {
        // the tranche loss's shortfall, within the expansion's error of the exact (recursive) model's; QuantLib's
        // integrates the portfolio's tail above the tranche's percentile and caps it at quantLibs (the S-xcr code
        // review's C3 O1-2, its rv_c3_misc.cpp)
        double esf = Pool(100, 0.3, a, d).ExpectedShortfall(D5, p);
        Assert.Equal(exact, esf, 0.03 * exact);
        Assert.True(esf <= (d - a) * 100.0);
        Assert.NotEqual(quantLibs, esf, 1.0);
    }

    [Fact]
    public void ARealizedDefaultLeavesTheLiveNames()
    {
        // A (10) defaulted in June 2006 at 0.25, B (20) and C (30) alive, [10%, 50%]: the restated basket B and C over
        // [0, 45%], the realized tranche loss 1.5
        var inception = new Date(1, Month.January, 2006);
        string[] abc = ["A", "B", "C"];
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.03), Flat(Today, 0.015)];
        var e = new BankruptcyEvent(new Date(1, Month.June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, Month.July, 2006), 0.25);
        var realized = new Basket(inception, abc, [10.0, 20.0, 30.0], PoolOf(abc, curves, [[e], null, null]), Context, 0.1, 0.5);
        string[] bc = ["B", "C"];
        var restated = new Basket(inception, bc, [20.0, 30.0], PoolOf(bc, [Flat(Today, 0.03), Flat(Today, 0.015)]), Context, 0.0, 0.45);
        SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> Model(double[] r) =>
            new(new GaussianConstantLossLM(0.3, r, LatentModelIntegrationType.GaussianQuadrature, r.Length));
        Assert.Equal(
            restated.SetLossModel(Model([0.5, 0.6])).ExpectedTrancheLoss(D5) + 1.5,
            realized.SetLossModel(Model([0.3, 0.5, 0.6])).ExpectedTrancheLoss(D5),
            1e-13);
    }

    [Fact]
    public void TheEvaluationDatesTailIsExact()
    {
        // every probability 0: QuantLib's search throws
        Basket basket = Pool(10, 0.3);
        Assert.Equal(0.0, basket.ProbOverLoss(Today, 0.1));
        Assert.Equal(1.0, basket.ProbOverLoss(Today, 0.0), 1e-11);
    }

    [Theory]
    [InlineData(0.8, 1826, 0.44204873292720814, 0.25078814965833629, 0.02)]
    [InlineData(0.85, 91, 0.044026744570138776, 0.014214988874399093, 0.02)]
    [InlineData(0.85, 365, 0.13174265685441477, 0.05837342611181906, 0.02)]
    [InlineData(0.9, 30, 0.012841285162115801, 0.0059026205935650555, 0.02)]
    [InlineData(0.9, 365, 0.10952846267092553, 0.058421231146187802, 0.02)]
    [InlineData(0.95, 365, 0.06326649003592888, 0.057780373340682645, 0.05)]
    public void TheDegenerateNodesAreExact(double correlation, int days, double exact1, double exact10, double band)
    {
        // QuantLib throws; the exact recursive model's tails at 1% and 10%
        Basket basket = Pool(100, correlation);
        Date d = Today + days;
        Assert.Equal(exact10, basket.ProbOverLoss(d, 0.1), band * exact10);
        if (days >= 365)
        {
            Assert.Equal(exact1, basket.ProbOverLoss(d, 0.01), 0.1 * exact1);
        }
    }

    [Theory]
    [InlineData(0.548, 1)]
    [InlineData(0.3, 7)]
    public void ATailOutsideItsDomainIsRefused(double correlation, int days)
    {
        // QuantLib: 26.815515432090283 and 2.6118595103824398
        NanoQLException e = Assert.Throws<NanoQLException>(() => Pool(10, correlation).ProbOverLoss(Today + days, 0.01));
        Assert.Contains("saddle-point expansion", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASmallPoolsTailInsideTheDomainIsQuantLibs()
    {
        // kept, the method's domain: QuantLib's 0.98306239823951058 for an exact 0.022312221771084606
        Assert.Equal(0.98306239823951058, Pool(10, 0.3).ProbOverLoss(Today + 30, 0.01), 1e-12);
    }

    [Theory]
    [InlineData(0.3, 365, 0.9, 4.8)]
    [InlineData(0.5, 3653, 0.99, 55.8)]
    [InlineData(0.1, 30, 0.5, 0.0)]
    public void ThePercentileIsTheExactOnesWithinItsLossUnit(double correlation, int days, double level, double exact)
    {
        // QuantLib: 4.9485835, 55.627361 and 0.064772838
        Assert.Equal(exact, Pool(100, correlation).Percentile(Today + days, level), 0.6);
    }

    [Theory]
    [InlineData("mix", 0.7, 365, 0.01, 0.07)]
    [InlineData("mix", 0.7, 1826, 0.1, 0.025)]
    [InlineData("mix", 0.9, 30, 0.1, 0.025)]
    [InlineData("mix", 0.9, 365, 0.1, 0.025)]
    [InlineData("het", 0.9, 30, 0.1, 0.025)]
    [InlineData("het", 0.9, 365, 0.1, 0.025)]
    [InlineData("het", 0.9, 1826, 0.01, 0.07)]
    [InlineData("het", 0.7, 1826, 0.1, 0.025)]
    [InlineData("het", 0.95, 365, 0.1, 0.025)]
    [InlineData("mix", 0.95, 365, 0.1, 0.025)]
    public void AHeterogeneousPoolsTailIsTheExactOnes(string pool, double correlation, int days, double level, double band)
    {
        // mix: QuantLib's 0.97841938 for the exact 0.198289 at the first, 0.20082286 for 0.00494071 at the third, a
        // throw at the fourth; at 0.95 the names at 0 read 0 times infinity where the saddle point runs to thousands,
        // unless they leave the sums
        Date d = Today + days;
        (Basket recursive, double total) = HetPool(pool, correlation, true);
        double exact = 1.0 - recursive.LossDistribution(d)
            .Where(kv => kv.Key < (level * total) - 1e-9).Select(kv => kv.Value).DefaultIfEmpty(0.0).Last();
        double tail = HetPool(pool, correlation, false).Basket.ProbOverLoss(d, level);
        Assert.Equal(exact, tail, band * exact);
    }

    [Theory]
    [InlineData(0.1, 0.61, 0.9, 16.729887379694436)]
    [InlineData(0.1, 0.7, 0.9, 16.729887379694436)]
    [InlineData(0.1, 0.7, 0.5, 4.937854581865258)]
    [InlineData(0.0, 0.65, 0.9, 26.72988737961641)]
    [InlineData(0.3, 0.9, 0.5, 0.28894261617611255)]
    public void ATrancheDetachingAboveTheLargestLossReadsItsShortfallThere(double a, double d, double p, double exact)
    {
        // the pool's largest loss is 60; the exact (recursive) model's shortfall is the same detaching at 60 or above,
        // this one within the tranche percentile's search (1.5e-4 at [0, 65%]) and the expansion's error (5.1% at the
        // last); QuantLib's reads NaN
        double atLargest = Pool(100, 0.3, a, 0.6).ExpectedShortfall(D5, p);
        double above = Pool(100, 0.3, a, d).ExpectedShortfall(D5, p);
        Assert.Equal(atLargest, above, 5e-4 * atLargest);
        Assert.Equal(exact, above, 0.06 * exact);
    }

    [Fact]
    public void TheDensityAboveTheLargestLossIsZero()
    {
        var model = (SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>)Pool(100, 0.3).LossModel!;
        Assert.True(model.ProbDensity(D5, 59.9) > 0.0);
        foreach (double loss in new[] { 60.5, 65.0, 90.0 })
        {
            Assert.Equal(0.0, model.ProbDensity(D5, loss));
        }
    }

    [Theory]
    [InlineData("het", 30, 0.01)]
    [InlineData("het", 365, 0.1)]
    [InlineData("mix", 30, 0.1)]
    [InlineData("mix", 365, 0.01)]
    public void AHeterogeneousPoolsSplitAddsUpToTheLoss(string pool, int days, double level)
    {
        // Martin's identity at correlation 0.9, short of it by the nodes certain of their loss (1.3e-4 at the last);
        // QuantLib's form read NaN for 50, 80, 100 and 95 of the 100 names
        (Basket basket, double total) = HetPool(pool, 0.9, false);
        double loss = level * total;
        Assert.Equal(loss, basket.SplitVaRLevel(Today + days, loss).Sum(), 1e-3 * loss);
    }

    [Fact]
    public void TheLossDistributionIsTheExactOnesWithinTheExpansionsAccuracy()
    {
        // 224 points, a 500th of the notional apart, to 45% of it; within 5% of the exact (recursive) model's tail
        // wherever that is 1e-4 or more (4.1% at most), at correlation 0.3 five years out
        (Basket saddle, double total) = HetPool("het", 0.3, false);
        (Basket recursive, _) = HetPool("het", 0.3, true);
        var distribution = saddle.LossDistribution(D5);
        var exact = recursive.LossDistribution(D5);
        Assert.Equal(224, distribution.Count);
        int k = 1;
        foreach (KeyValuePair<double, double> point in distribution)
        {
            Assert.Equal(k++ * total / 500.0, point.Key, 1e-9);
            double exactTail = 1.0 - exact.Where(e => e.Key < point.Key - 1e-9).Select(e => e.Value).DefaultIfEmpty(0.0).Last();
            if (exactTail >= 1e-4)
            {
                Assert.Equal(exactTail, 1.0 - point.Value, 0.05 * exactTail);
            }
        }
    }

    private static (Basket Basket, double Total) HetPool(string pool, double correlation, bool recursive)
    {
        // mix: 95 names at hazard 0.03 and notional 1, five at 0.001 and 3; het: hazards 0.0002 to 0.12 with
        // notionals 6 to 1, the safest the largest, cycled by ten
        bool mix = pool == "mix";
        double[] h = [.. Enumerable.Range(0, 100).Select(i => mix ? (i < 95 ? 0.03 : 0.001) : Hazards[i % 10])];
        double[] n = [.. Enumerable.Range(0, 100).Select(i => mix ? (i < 95 ? 1.0 : 3.0) : Notionals[i % 10])];
        string[] names = [.. Enumerable.Range(0, 100).Select(i => $"N{i}")];
        DefaultProbabilityTermStructure[] curves = [.. h.Select(x => (DefaultProbabilityTermStructure)Flat(Today, x))];
        var basket = new Basket(Today, names, n, PoolOf(names, curves), Context, 0.0, 1.0);
        var latent = new GaussianConstantLossLM(correlation, [.. Enumerable.Repeat(0.4, 100)], LatentModelIntegrationType.GaussianQuadrature, 100);
        IDefaultLossModel model = recursive
            ? new RecursiveGaussLossModel(latent)
            : new SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(latent);
        return (basket.SetLossModel(model), n.Sum());
    }

    private static double[] Hazards => [0.0002, 0.0005, 0.001, 0.002, 0.005, 0.01, 0.02, 0.04, 0.08, 0.12];

    private static double[] Notionals => [6, 5, 5, 4, 4, 3, 3, 2, 2, 1];

    private static Basket Pool(int n, double correlation, double a = 0.0, double d = 1.0)
    {
        string[] names = [.. Enumerable.Range(0, n).Select(i => $"N{i}")];
        DefaultProbabilityTermStructure[] curves = [.. names.Select(_ => (DefaultProbabilityTermStructure)Flat(Today, 0.03))];
        var basket = new Basket(Today, names, Enumerable.Repeat(1.0, n), PoolOf(names, curves), Context, a, d);
        var lm = new GaussianConstantLossLM(correlation, [.. Enumerable.Repeat(0.4, n)], LatentModelIntegrationType.GaussianQuadrature, n);
        return basket.SetLossModel(new SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(lm));
    }
}
