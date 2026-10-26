// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// C2's engines on structures dated apart (the playbook's dates row; the S-exo plan's ER5 (xviii)). The spread
/// engines, Stulz, the Gaussian copula and the cash-dividend engine read by date, which QuantLib refuses for an option
/// expiring before a later structure's date ("negative time"); the two-asset correlation engine reads each volatility
/// at its own process's time and everything else at the second's (+0.84% on the plan's market with asset 2's curves
/// on Act/360), and the American Margrabe engine builds flat curves from the evaluation date with rates over the
/// risk-free curve's time (<c>analytictwoassetcorrelationengine.cpp:54-66</c>,
/// <c>analyticamericanmargrabeengine.cpp:53-79</c>). nanoql's by-date reads accrue nothing before a structure's date,
/// and its clocked engines read every structure on the clock across both processes; the cash-dividend engine dates and
/// counts each dividend's volatility on the clock, where QuantLib sizes it on the risk-free curve's time but reads it
/// on the volatility's own (+0.48% with the volatility on Act/360 under curves on Act/365,
/// <c>cashdividendeuropeanengine.cpp:139-151</c>). QuantLib's engine on the market restated from the evaluation date,
/// onto one clock, is the check.
/// </summary>
public class TwoAssetMismatchedTimesTests
{
    [Fact]
    public void AnExpiryBeforeTheRiskFreeCurvesDateDiscountsNothing()
    {
        // the risk-free curve dated four days on, the option expiring in two; restated, a zero rate from today
        var later = new FlatForward(Today + 4, 0.05, A365);
        Exercise exercise = new EuropeanExercise(Today + 2);
        var p1 = Process(110.0, 0.02, later, 0.3);
        var p2 = Process(100.0, 0.03, later, 0.2);
        var r1 = Process(110.0, 0.02, 0.0, 0.3);
        YieldTermStructure zero = r1.RiskFreeRate;
        var r2 = Process(100.0, 0.03, zero, 0.2);

        var spread = new BasketOption(new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0)), exercise);
        Assert.Equal(
            KirkEngine.Calculate(spread, Context, r1, r2, 0.5).NPV, KirkEngine.Calculate(spread, Context, p1, p2, 0.5).NPV);
        Assert.Equal(
            PearsonSpreadEngine.Calculate(spread, Context, r1, r2, 0.5).NPV,
            PearsonSpreadEngine.Calculate(spread, Context, p1, p2, 0.5).NPV);
        Assert.Equal(
            GaussianCopulaSpreadEngine.Calculate(spread, Context, r1, r2, 0.5).NPV,
            GaussianCopulaSpreadEngine.Calculate(spread, Context, p1, p2, 0.5).NPV);

        var minCall = new BasketOption(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 95.0)), exercise);
        Assert.Equal(
            StulzEngine.Calculate(minCall, Context, r1, r2, 0.5).NPV, StulzEngine.Calculate(minCall, Context, p1, p2, 0.5).NPV);

        var vanilla = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 105.0), exercise);
        System.Collections.Immutable.ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([Today + 1], [2.0]);
        Assert.Equal(
            CashDividendEuropeanEngine.Calculate(vanilla, Context, r1, dividends).NPV!.Value,
            CashDividendEuropeanEngine.Calculate(vanilla, Context, p1, dividends).NPV!.Value,
            1e-12);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsPricedFromTheEvaluationDate()
    {
        // the risk-free curve settling two days on; restated from today, its discount 1 up to its date and its rate
        // beyond, which a log-linear discount curve holds exactly
        var settling = new FlatForward(Today + 2, 0.05, A365);
        var restated = new DiscountCurve(
            [Today, Today + 2, Today + 2 + 3650], [1.0, 1.0, Math.Exp(-0.05 * 10.0)], A365);
        var p1 = Process(52.0, 0.0, settling, 0.2);
        var p2 = Process(65.0, 0.0, settling, 0.3);
        var r1 = Process(52.0, 0.0, restated, 0.2);
        var r2 = Process(65.0, 0.0, restated, 0.3);

        var twoAsset = new TwoAssetCorrelationOption(OptionType.Call, 50.0, 70.0, European());
        double expected = AnalyticTwoAssetCorrelationEngine.Calculate(twoAsset, Context, r1, r2, 0.75).NPV!.Value;
        Assert.Equal(
            expected, AnalyticTwoAssetCorrelationEngine.Calculate(twoAsset, Context, p1, p2, 0.75).NPV!.Value, 1e-12 * expected);

        var margrabe = new MargrabeOption(1, 1, new AmericanExercise(Today, Today + 365));
        expected = AnalyticAmericanMargrabeEngine.Calculate(margrabe, Context, r1, r2, 0.5).NPV!.Value;
        Assert.Equal(
            expected, AnalyticAmericanMargrabeEngine.Calculate(margrabe, Context, p1, p2, 0.5).NPV!.Value, 1e-12 * expected);

        var european = new MargrabeOption(1, 1, European());
        expected = AnalyticEuropeanMargrabeEngine.Calculate(european, Context, r1, r2, 0.5).Theta!.Value;
        Assert.Equal(
            expected, AnalyticEuropeanMargrabeEngine.Calculate(european, Context, p1, p2, 0.5).Theta!.Value, 1e-10);
    }

    [Fact]
    public void ACashDividendVolatilityCountingItsOwnDaysIsReadOnTheClock()
    {
        // the volatility on Act/360 under curves on Act/365; restated, σ√(365/360) on Act/365, the same variances by
        // date, whose spot-model value is QuantLib's (its engine on the restated market, 15.243766501194571)
        var exercise = new EuropeanExercise(Today + 365);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 90.0), exercise);
        System.Collections.Immutable.ImmutableArray<Dividend> dividends =
            DividendFunctions.DividendVector([Today + 90, Today + 270], [2.0, 3.0]);
        var onAct360 = new BlackScholesMertonProcess(
            100.0, Flat(0.02), Flat(0.05), new BlackConstantVol(Today, new NullCalendar(), 0.30, new Actual360()));
        var restated = new BlackScholesMertonProcess(
            100.0, Flat(0.02), Flat(0.05), Vol(0.30 * Math.Sqrt(365.0 / 360.0)));
        double expected = CashDividendEuropeanEngine.Calculate(call, Context, restated, dividends).NPV!.Value;
        Assert.Equal(15.243766501194571, expected, 1e-14 * expected);
        Assert.Equal(
            expected, CashDividendEuropeanEngine.Calculate(call, Context, onAct360, dividends).NPV!.Value, 1e-14 * expected);
    }
}
