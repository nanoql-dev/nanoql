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
/// The analytic lookback engines on structures that measure time differently (fixed). QuantLib's engines read each rate
/// as its curve's zero rate over the risk-free curve's time, and the volatility over the same time, so on an Actual/360
/// risk-free curve beside an Actual/365 Fixed volatility they are off their own values on the market restated onto one
/// clock with the same discounts by date: on spot 100, r 5% (Actual/360), q 2%, σ 25% over a year, the floating call
/// +0.468%, the fixed call +0.599%, the partial floating call +0.435% and the partial fixed call +0.563%. nanoql reads
/// each rate on the volatility's clock, the curve's zero rate times its own time over the volatility's, and each time
/// on it, as the analytic barrier engine reads them. QuantLib's check: its own engine on the restated market, the
/// risk-free rate scaled to Actual/365 Fixed so its discounts by date are unchanged, which nanoql's engines price with
/// QuantLib's bits (the vectors).
/// </summary>
public class LookbackMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

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
    public void TheFloatingLookbackPricesAsOnTheRestatedMarket()
    {
        var option = new ContinuousFloatingLookbackOption(95.0, new FloatingTypePayoff(OptionType.Call), AYear);
        Assert.Equal(
            AnalyticContinuousFloatingLookbackEngine.Calculate(option, Context, Restated).NPV!.Value,
            AnalyticContinuousFloatingLookbackEngine.Calculate(option, Context, Mismatched).NPV!.Value,
            1e-12);
    }

    [Fact]
    public void TheFixedLookbackPricesAsOnTheRestatedMarket()
    {
        var option = new ContinuousFixedLookbackOption(105.0, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        Assert.Equal(
            AnalyticContinuousFixedLookbackEngine.Calculate(option, Context, Restated).NPV!.Value,
            AnalyticContinuousFixedLookbackEngine.Calculate(option, Context, Mismatched).NPV!.Value,
            1e-12);
    }

    [Fact]
    public void ThePartialFloatingLookbackPricesAsOnTheRestatedMarket()
    {
        var option = new ContinuousPartialFloatingLookbackOption(95.0, 1.0, Today + 180, new FloatingTypePayoff(OptionType.Call), AYear);
        Assert.Equal(
            AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, Context, Restated).NPV!.Value,
            AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, Context, Mismatched).NPV!.Value,
            1e-12);
    }

    [Fact]
    public void ThePartialFixedLookbackPricesAsOnTheRestatedMarket()
    {
        var option = new ContinuousPartialFixedLookbackOption(Today + 180, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        Assert.Equal(
            AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, Context, Restated).NPV!.Value,
            AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, Context, Mismatched).NPV!.Value,
            1e-12);
    }
}
