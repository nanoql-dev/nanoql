// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The base-correlation model after a default: its equity baskets hold the live names, their notionals and recoveries,
/// where QuantLib's take the live notionals on the whole pool and its basket refuses ("unmatched data entry sizes in
/// basket"). A (10) defaulted in June 2006 and settled at 0.25, B (20) and C (30) alive, [10%, 50%] from 1 January
/// 2006, recoveries 0.3, 0.5, 0.6, the Gaussian LHP base on a flat surface of 0.3: QuantLib's model on the basket of
/// the live names (B and C over [0, 45%]) reads 2.2611633149989752 at five years.
/// </summary>
public class BaseCorrelationLossModelTests
{
    [Fact]
    public void TheModelPricesTheLiveNames()
    {
        string[] abc = ["A", "B", "C"];
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.03), Flat(Today, 0.015)];
        var a = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, July, 2006), 0.25);
        var realized = new Basket(new Date(1, January, 2006), abc, [10.0, 20.0, 30.0], PoolOf(abc, curves, [[a], null, null]), Context, 0.1, 0.5);

        double etl = realized.SetLossModel(new GaussianLHPFlatBCLM(Surface(0.3), [0.3, 0.5, 0.6])).ExpectedTrancheLoss(D5);

        // QuantLib refuses the realized basket; on the live one it reads 2.2611633149989752, the realized tranche
        // loss 1.5 added here
        Assert.Equal(2.2611633149989752 + 1.5, etl, 1e-13);
    }

    [Fact]
    public void AnEquityTranchesAttachmentLegIsZero()
    {
        // levels 3% and 7% at 0.1 and 0.3: the surface reads -0.05 at level 0, where QuantLib's LHP base throws; the
        // model is its base at the detachment's correlation
        BaseCorrelationTermStructure<Bilinear> surface = BaseCorrelationTermStructure<Bilinear>.Create(
            0, new TARGET(), BusinessDayConvention.Following, [new Period(1, TimeUnit.Years), new Period(10, TimeUnit.Years)],
            [0.03, 0.07], [[0.1, 0.1], [0.3, 0.3]], new Actual365Fixed(), Context);
        Assert.True(surface.Correlation(D5, 0.0) < 0.0);
        double[] recoveries = [.. Enumerable.Repeat(0.4, 5)];
        double lhp = Equity().SetLossModel(new GaussianLHPFlatBCLM(surface, recoveries)).ExpectedTrancheLoss(D5);
        Assert.Equal(Equity().SetLossModel(new GaussianLHPLossModel(0.1, recoveries)).ExpectedTrancheLoss(D5), lhp, 1e-13);

        // the inhomogeneous pool base, which QuantLib's refuses on every equity tranche ("a out of range")
        double pool = Equity().SetLossModel(new BaseCorrelationLossModel<IHGaussPoolLossModel, Bilinear, GaussianCopulaPolicyInitTraits>(surface, recoveries)).ExpectedTrancheLoss(D5);
        Assert.Equal(
            Equity().SetLossModel(new IHGaussPoolLossModel(new GaussianConstantLossLM(0.1, recoveries, LatentModelIntegrationType.GaussianQuadrature, 5), 500)).ExpectedTrancheLoss(D5),
            pool,
            1e-13);
    }

    private static Basket Equity()
    {
        string[] names = ["N0", "N1", "N2", "N3", "N4"];
        DefaultProbabilityTermStructure[] curves = [.. new[] { 0.01, 0.015, 0.02, 0.025, 0.03 }.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))];
        return new Basket(Today, names, Enumerable.Repeat(10.0, 5), PoolOf(names, curves), Context, 0.0, 0.03);
    }

    internal static BaseCorrelationTermStructure<Bilinear> Surface(double correlation) =>
        BaseCorrelationTermStructure<Bilinear>.Create(
            0,
            new TARGET(),
            BusinessDayConvention.Following,
            [new Period(1, TimeUnit.Years), new Period(10, TimeUnit.Years)],
            [0.05, 0.6],
            [[correlation, correlation], [correlation, correlation]],
            new Actual365Fixed(),
            Context);
}
