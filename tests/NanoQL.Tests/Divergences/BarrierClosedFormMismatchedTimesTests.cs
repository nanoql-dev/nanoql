// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The barrier family's closed forms on structures that measure time differently (fixed). QuantLib's double-barrier,
/// double-barrier binary, partial-time, soft and two-asset engines read each rate as its curve's zero rate over the
/// risk-free curve's time and the volatility over the same time, and combine them as though every structure shared that
/// clock, so on an Actual/360 risk-free curve beside an Actual/365 Fixed volatility they are off their own values on
/// the market restated onto one clock with the same discounts and variances by date (−3.33%, +0.88% and +1.64% on the
/// double barrier, the partial-time and the soft barrier). nanoql reads each rate on the volatility's clock, the
/// curve's zero rate times its own time over the volatility's, and each time on it, as the analytic barrier engine
/// reads them; the two-asset engine on the clock across both processes, the first's risk-free day counter. QuantLib's
/// check: its own engine on the market restated onto that clock, each flat structure's rate or volatility scaled so its
/// discounts or variances by date are unchanged, which nanoql's engine prices with QuantLib's bits (the vectors).
/// </summary>
public class BarrierClosedFormMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    // The market as given: an Actual/360 risk-free curve beside an Actual/365 Fixed dividend curve and volatility.
    private static readonly GeneralizedBlackScholesProcess Mismatched = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05, A360),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    // The same market on the volatility's clock: the risk-free rate scaled to Actual/365 Fixed, its discounts by date
    // unchanged.
    private static readonly GeneralizedBlackScholesProcess Restated = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05 * 365.0 / 360.0, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    [Fact]
    public void TheDoubleBarrierPricesAsOnTheRestatedMarket()
    {
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise);
        double restated = AnalyticDoubleBarrierEngine.Calculate(option, Context, Restated).NPV!.Value;
        Assert.Equal(restated, AnalyticDoubleBarrierEngine.Calculate(option, Context, Mismatched).NPV!.Value, 1e-14);
    }

    [Fact]
    public void TheDoubleBarrierBinaryPricesAsOnTheRestatedMarket()
    {
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0), Exercise);
        double restated = AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, Restated).NPV!.Value;
        Assert.Equal(restated, AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, Mismatched).NPV!.Value, 1e-14);
    }

    [Fact]
    public void ThePartialTimeBarrierPricesAsOnTheRestatedMarket()
    {
        var option = new PartialTimeBarrierOption(
            BarrierType.DownOut,
            PartialBarrierRange.EndB1,
            100.0,
            0.0,
            Today + 180,
            new PlainVanillaPayoff(OptionType.Call, 90.0),
            Exercise);
        double restated = AnalyticPartialTimeBarrierOptionEngine.Calculate(option, Context, Restated).NPV!.Value;
        Assert.Equal(
            restated, AnalyticPartialTimeBarrierOptionEngine.Calculate(option, Context, Mismatched).NPV!.Value, 1e-14);
    }

    [Fact]
    public void TheSoftBarrierPricesAsOnTheRestatedMarket()
    {
        var option = new SoftBarrierOption(
            BarrierType.DownOut, 90.0, 95.0, new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise);
        double restated = AnalyticSoftBarrierEngine.Calculate(option, Context, Restated).NPV!.Value;
        Assert.Equal(restated, AnalyticSoftBarrierEngine.Calculate(option, Context, Mismatched).NPV!.Value, 1e-14);
    }

    [Fact]
    public void TheTwoAssetBarrierPricesAsOnTheRestatedMarket()
    {
        // the clock across both processes is the first's Actual/360 risk-free curve: the other structures restated on
        // it, each Actual/365 Fixed rate scaled by 360/365 and each variance kept
        GeneralizedBlackScholesProcess second = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.01, A365),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        GeneralizedBlackScholesProcess firstRestated = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02 * 360.0 / 365.0, A360),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25 * Math.Sqrt(360.0 / 365.0), A360));
        GeneralizedBlackScholesProcess secondRestated = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.01 * 360.0 / 365.0, A360),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.2 * Math.Sqrt(360.0 / 365.0), A360));
        var option = new TwoAssetBarrierOption(
            BarrierType.DownOut, 95.0, new PlainVanillaPayoff(OptionType.Call, 90.0), Exercise);
        double restated = AnalyticTwoAssetBarrierEngine.Calculate(
            option, Context, firstRestated, secondRestated, 0.5).NPV!.Value;
        Assert.Equal(
            restated, AnalyticTwoAssetBarrierEngine.Calculate(option, Context, Mismatched, second, 0.5).NPV!.Value, 1e-13);
    }
}
