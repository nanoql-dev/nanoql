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
/// The binary barrier engine's rebate (fixed). The option takes a rebate and QuantLib's engine never reads it: a
/// down-and-out cash-or-nothing call, cash 10, strike 100, barrier 90 at r 5%, q 1%, σ 20% over a year prices
/// 3.678406488567 at rebates 0 and 5. nanoql prices it by the analytic barrier engine's rebate terms, a knock-out's
/// paid at the hit and a knock-in's at expiry if never hit. Checks: QuantLib's routes for the same payments, a
/// one-touch at the hit by its analytic digital American engine (2.786734823341 below 90, 3.267438031090 above 110 for
/// 5) and a strike-0 cash-or-nothing knock-out by this engine (2.060934934335 and 1.600028420441). A knock-out already
/// hit is worth its rebate, paid now, where QuantLib's engine prices 0: its check QuantLib's one-touch at the same
/// spot, 5.000000000000 on and beyond the barrier, and the engine's own value a hair inside, 5.000015883 at 90.0001.
/// </summary>
public class BinaryBarrierRebateTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly AmericanExercise AtExpiry = new(Today, Today + 365, true);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.01, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    [Theory]
    [InlineData(BarrierType.DownOut, 90.0, OptionType.Put)]
    [InlineData(BarrierType.UpOut, 110.0, OptionType.Call)]
    public void AKnockOutsRebateIsAOneTouchAtTheHit(BarrierType type, double barrier, OptionType touchType)
    {
        var oneTouch = new VanillaOption(
            new CashOrNothingPayoff(touchType, barrier, 5.0), new AmericanExercise(Today, Today + 365, false));
        double expected = AnalyticDigitalAmericanEngine.Calculate(oneTouch, Context, Process).NPV!.Value;
        Assert.Equal(expected, RebatePart(type, barrier), 1e-12);
    }

    [Theory]
    [InlineData(BarrierType.DownIn, BarrierType.DownOut, 90.0)]
    [InlineData(BarrierType.UpIn, BarrierType.UpOut, 110.0)]
    public void AKnockInsRebateIsANoTouchAtExpiry(BarrierType type, BarrierType knockOut, double barrier)
    {
        var noTouch = new BarrierOption(knockOut, barrier, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 5.0), AtExpiry);
        double expected = AnalyticBinaryBarrierEngine.Calculate(noTouch, Context, Process).NPV!.Value;
        Assert.Equal(expected, RebatePart(type, barrier), 1e-12);
    }

    [Theory]
    [InlineData(BarrierType.DownOut, 90.0, 90.0, 90.0001, OptionType.Put)]
    [InlineData(BarrierType.DownOut, 90.0, 89.0, 90.0001, OptionType.Put)]
    [InlineData(BarrierType.UpOut, 110.0, 110.0, 109.9999, OptionType.Call)]
    [InlineData(BarrierType.UpOut, 110.0, 111.0, 109.9999, OptionType.Call)]
    public void AKnockOutAlreadyHitIsWorthItsRebate(BarrierType type, double barrier, double spot, double inside, OptionType touchType)
    {
        double Price(double s) => AnalyticBinaryBarrierEngine.Calculate(
            new BarrierOption(type, barrier, 5.0, new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), AtExpiry),
            Context,
            ProcessAt(s)).NPV!.Value;
        var oneTouch = new VanillaOption(
            new CashOrNothingPayoff(touchType, barrier, 5.0), new AmericanExercise(Today, Today + 365, false));
        double expected = AnalyticDigitalAmericanEngine.Calculate(oneTouch, Context, ProcessAt(spot)).NPV!.Value;
        Assert.Equal(5.0, expected, 1e-12);
        Assert.Equal(expected, Price(spot), 1e-12);
        Assert.Equal(Price(spot), Price(inside), 1e-4);
    }

    private static GeneralizedBlackScholesProcess ProcessAt(double spot) => new BlackScholesMertonProcess(
        spot,
        new FlatForward(Today, 0.01, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    private static double RebatePart(BarrierType type, double barrier)
    {
        double Price(double rebate) => AnalyticBinaryBarrierEngine.Calculate(
            new BarrierOption(type, barrier, rebate, new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), AtExpiry),
            Context,
            Process).NPV!.Value;
        return Price(5.0) - Price(0.0);
    }
}
