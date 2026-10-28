// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CdoBuilders;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A CDO and a random model on a basket with a realized default (the S-xcr plan's XR8 (ii)): the CDO's premium leg,
/// upfront and remaining notional stand on the tranche's outstanding notional, and the random models simulate the live
/// names alone. A (10) and B (20) alive and C (30) defaulted in June 2006 and settled at 0.4, the [10%, 50%] tranche
/// from 1 January 2006: QuantLib refuses the pool model on the realized basket ("sizes differ: 3 vs 2"), and on the
/// restated basket (A and B, [0, 40%]) its mid-point CDO at 1% upfront and 2% running reads NPV
/// −0.28931799290637461, premium 0.98631843637350081, protection 1.3956199920495744, fair premium 0.025866624453865835
/// and remaining notional 12, its integral CDO NPV −0.28056449432303487; at correlation 0 its random model on the
/// restated basket reads P(≥ 2) 0.0088 and on the realized one P(≥ 3) 0.0004 (the S-xcr run's <c>c2_facts.cpp</c>).
/// A basket whose first name has defaulted, the models' recoveries and rows differing by name, checks each model's
/// restriction to the live rows, a seasoned CDO's paid first coupon and the random model's shifted loss distribution
/// against QuantLib on its restated basket (the S-xcr run's <c>c2_o12.cpp</c>; the S-xcr code review's C2 O1-2).
/// </summary>
public class CdoRealizedDefaultsTests
{
    private static readonly Date Inception = new(1, January, 2006);

    [Fact]
    public void TheCdoIsQuantLibsOnTheRestatedBasket()
    {
        Basket basket = Realized(0.1, 0.5).SetLossModel(Pool(3, 0.3));
        SyntheticCDOResults mid = Mid(Cdo(basket, ProtectionSide.Seller, 0.01, 0.02));
        SyntheticCDOResults integral = Integral(Cdo(basket, ProtectionSide.Seller, 0.01, 0.02));

        Assert.Equal(-0.28931799290637461, mid.NPV!.Value, 1e-13);
        Assert.Equal(0.98631843637350081, mid.PremiumValue!.Value, 1e-13);
        Assert.Equal(1.3956199920495744, mid.ProtectionValue!.Value, 1e-13);
        Assert.Equal(0.025866624453865835, mid.FairPremium!.Value, 1e-14);
        Assert.Equal(12.0, mid.RemainingNotional!.Value, 1e-13);
        Assert.Equal(-0.28056449432303487, integral.NPV!.Value, 1e-13);
    }

    [Fact]
    public void TheRandomModelSimulatesTheLiveNames()
    {
        // the order less the realized default: QuantLib's restated P(>= 2) bit for bit, at independence about pd^2
        Basket basket = Realized(0.0, 1.0).SetLossModel(Random(3, 0.0, 5000));

        Assert.Equal(0.0088, basket.ProbAtLeastNEvents(3, D5), 1e-15);
        double pd = Flat(Today, 0.02).DefaultProbability(D5);
        Assert.Equal(pd * pd, basket.ProbAtLeastNEvents(3, D5), 3 * Math.Sqrt(pd * pd / 5000));
    }

    [Fact]
    public void TheLhpModelPricesTheLiveNamesRecoveries()
    {
        // QuantLib on the restated basket, recoveries 0.5 and 0.6: 2.2611633149989752, plus the realized 1.5
        Basket basket = FirstDefaulted().SetLossModel(new GaussianLHPLossModel(0.3, Recoveries));
        Assert.Equal(2.2611633149989752 + 1.5, basket.ExpectedTrancheLoss(D5), 1e-13);
    }

    [Fact]
    public void TheRandomDefaultModelSimulatesTheLiveNamesRecoveries()
    {
        // QuantLib on the restated basket: 2.2436, plus the realized 1.5
        Basket basket = FirstDefaulted().SetLossModel(RandomOnRecoveries(5000));
        Assert.Equal(2.2436 + 1.5, basket.ExpectedTrancheLoss(D5), 1e-13);
    }

    [Fact]
    public void TheRandomModelsLossDistributionIsShiftedByTheRealizedTrancheLoss()
    {
        // QuantLib's restated distribution, 151 points from (0, 0.8104) to (21.85430463576159, 1), shifted by the
        // realized tranche loss 1.5, not the settled loss 7.5
        ImmutableSortedDictionary<double, double> dist = FirstDefaulted().SetLossModel(RandomOnRecoveries(5000)).LossDistribution(D5);

        Assert.Equal(151, dist.Count);
        Assert.Equal(1.5, dist.Keys.First(), 1e-14);
        Assert.Equal(0.8104, dist.Values.First(), 1e-15);
        Assert.Equal(9.9072847682119196 + 1.5, dist.Keys.ElementAt(68), 1e-13);
        Assert.Equal(0.9282, dist.Values.ElementAt(68), 1e-15);
        Assert.Equal(21.85430463576159 + 1.5, dist.Keys.Last(), 1e-13);
        Assert.Equal(1.0, dist.Values.Last(), 1e-15);
    }

    [Fact]
    public void TheRandomLossModelPairsTheLiveNamesRows()
    {
        // QuantLib on the restated basket, the spot model's default rows 0.4 and 0.3 and recovery rows 0.2 and 0.45, at
        // A = 3, where no simulated recovery rounds to 1 (XR8 (xxv)): 2.2075976562499999, plus the realized 1.5
        var spot = new GaussianSpotLossLM([[0.5], [0.4], [0.3], [0.35], [0.2], [0.45]], Recoveries, 3.0, LatentModelIntegrationType.GaussianQuadrature);
        Basket basket = FirstDefaulted().SetLossModel(new GaussianRandomLossLM(spot, 2000, 2863311530));
        Assert.Equal(2.2075976562499999 + 1.5, basket.ExpectedTrancheLoss(D5), 1e-13);
    }

    [Fact]
    public void ASeasonedCdoStartsFromTheRealizedLoss()
    {
        // its first coupons paid: QuantLib on the restated basket, mid-point NPV -0.24617124789349365, premium
        // 1.7456191386025994, protection 1.9917903864960931, fair premium 0.022820446252560661; integral NPV
        // -0.23471478895531983, protection 1.9804579457978784
        Basket basket = FirstDefaulted().SetLossModel(
            new IHGaussPoolLossModel(new GaussianConstantLossLM(0.3, Recoveries, LatentModelIntegrationType.GaussianQuadrature, 3), 100, 5.0, -5.0, 15));
        Schedule seasoned = new MakeSchedule().From(Inception).To(new Date(1, January, 2011))
            .WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        SyntheticCDOResults mid = Mid(Cdo(basket, ProtectionSide.Seller, 0.01, 0.02, schedule: seasoned));
        SyntheticCDOResults integral = Integral(Cdo(basket, ProtectionSide.Seller, 0.01, 0.02, schedule: seasoned));

        Assert.Equal(-0.24617124789349365, mid.NPV!.Value, 1e-13);
        Assert.Equal(1.7456191386025994, mid.PremiumValue!.Value, 1e-13);
        Assert.Equal(1.9917903864960931, mid.ProtectionValue!.Value, 1e-13);
        Assert.Equal(0.022820446252560661, mid.FairPremium!.Value, 1e-14);
        Assert.Equal(-0.23471478895531983, integral.NPV!.Value, 1e-13);
        Assert.Equal(1.9804579457978784, integral.ProtectionValue!.Value, 1e-13);
    }

    // A (10, hazard 0.02) defaulted in June 2006 and settled at 0.25, B (20, 0.03) and C (30, 0.015) alive, the
    // [10%, 50%] tranche from 1 January 2006: QuantLib's restated basket is B and C over [0, 45%], the realized tranche
    // loss 7.5 - 6 = 1.5.
    private static Basket FirstDefaulted()
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.03), Flat(Today, 0.015)];
        var a = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, July, 2006), 0.25);
        return new Basket(Inception, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves, [[a], null, null]), Context, 0.1, 0.5);
    }

    private static double[] Recoveries => [0.3, 0.5, 0.6];

    private static GaussianRandomDefaultLM RandomOnRecoveries(int paths) =>
        new(new GaussianConstantLossLM(0.3, Recoveries, LatentModelIntegrationType.GaussianQuadrature, 3), paths, 2863311530);

    private static Basket Realized(double a, double d)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        var c = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, July, 2006), 0.4);
        return new Basket(Inception, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves, [null, null, [c]]), Context, a, d);
    }
}
