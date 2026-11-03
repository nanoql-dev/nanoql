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
/// The analytic barrier engine on structures that measure time differently (fixed). QuantLib reads every structure by
/// the exercise date (<c>analyticbarrierengine.cpp:128-170</c>), each rate over its own curve's time and the variance
/// over the volatility's, and combines them as though they shared a clock: on a 5% Actual/360 risk-free curve a
/// one-year down-and-out call at 90, strike 100, rebate 2, is 9.489629845557584 against 9.494843368487192 on the
/// Actual/365 Fixed curve with the same discount factors, and in-out parity fails by 3.7e-5; on a risk-free curve
/// settling two days later, or beside a dividend curve and volatility dated later, it is off its own engine on the
/// market restated from the earliest date by up to 2.6e-3; and an exercise before the risk-free curve's date throws
/// "negative time" (QuantLib-Python 1.44). nanoql reads each rate on the volatility's clock, the curve's zero rate
/// times its own accrual time to the exercise over the volatility's, and each discount and the variance as accrued,
/// nothing before a structure's own date (the playbook's row for structures on another clock): a factor of 1.0, and
/// QuantLib's bits, where the clocks agree; the curve's own rate where the volatility accrues nothing, an option
/// expiring on the evaluation date. QuantLib's check: its own engine on the market restated from the earliest date,
/// each restated structure on Actual/365 Fixed (a curve dated later a discount curve at 1 up to its date, a volatility
/// dated later a variance curve at 0 up to its date), and its master's value at expiry.
/// </summary>
public class BarrierMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly Date Settlement = new(19, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void AnAct360MarketPricesAsQuantLibOnTheEquivalentCurves()
    {
        GeneralizedBlackScholesProcess act360 = Process(new FlatForward(Today, 0.05, new Actual360()));

        // QuantLib on the equivalent Actual/365 Fixed curve (QuantLib-Python 1.44); on the Actual/360 curve it gives
        // 9.489629845557584, 1.4491375197740932, 3.616252220429545 and 11.660195264208374
        (BarrierType Type, double Barrier, double Expected)[] cases =
        [
            (BarrierType.DownOut, 90.0, 9.494843368487192),
            (BarrierType.UpOut, 110.0, 1.449044305624758),
            (BarrierType.DownIn, 90.0, 3.6107639416290245),
            (BarrierType.UpIn, 110.0, 11.660134920223296),
        ];
        foreach (var c in cases)
        {
            Assert.Equal(c.Expected, Npv(c.Type, c.Barrier, 2.0, act360, Exercise), 1e-12);
        }
    }

    [Fact]
    public void InOutParityHoldsOnMixedDayCounters()
    {
        GeneralizedBlackScholesProcess act360 = Process(new FlatForward(Today, 0.05, new Actual360()));
        double european = AnalyticEuropeanEngine.Calculate(
            new EuropeanOption(new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise), Context, act360).NPV!.Value;

        // QuantLib: 2.99809812535263 + 8.158549416932953 against 11.156684126657153, −3.7e-5
        double knockIn = Npv(BarrierType.DownIn, 90.0, 0.0, act360, Exercise);
        double knockOut = Npv(BarrierType.DownOut, 90.0, 0.0, act360, Exercise);
        Assert.Equal(11.156684126657153, european);
        Assert.Equal(european, knockIn + knockOut, 1e-12);
    }

    [Fact]
    public void AnOptionExpiringOnTheEvaluationDateIsPricedAtExpiry()
    {
        var context = new EvaluationContext { EvaluationDate = Today, IncludeReferenceDateEvents = true };
        GeneralizedBlackScholesProcess act360 = Process(new FlatForward(Today, 0.05, new Actual360()));
        var expiry = new EuropeanExercise(Today);
        double Value(double barrier) => AnalyticBarrierEngine.Calculate(
            new BarrierOption(BarrierType.DownOut, barrier, 2.0, new PlainVanillaPayoff(OptionType.Call, 95.0), expiry),
            context,
            act360).NPV!.Value;

        // the spot on the barrier, a hit: QuantLib NaN, its master the rebate, paid now
        Assert.Equal(2.0, Value(100.0));

        // not hit: QuantLib's own 5.0, the payoff
        Assert.Equal(5.0, Value(90.0));
    }

    [Fact]
    public void AnImpliedVolatilityOnMixedDayCountersIsTheOneOnTheEquivalentCurves()
    {
        var option = new BarrierOption(
            BarrierType.DownOut, 90.0, 2.0, new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise);

        // QuantLib on the equivalent curve; 0.08000610981958221 on the Actual/360 curve
        double act360 = option.ImpliedVolatility(
            5.0, Process(new FlatForward(Today, 0.05, new Actual360())), Context, 1e-8);
        double act365 = option.ImpliedVolatility(
            5.0, Process(new FlatForward(Today, 0.05 * 365.0 / 360.0, A365)), Context, 1e-8);
        Assert.Equal(0.08006718836073619, act365, 1e-8);
        Assert.Equal(act365, act360, 1e-8);
    }

    [Fact]
    public void OnACurveSettlingLaterTheEngineIsQuantLibsOnTheRestatedMarket()
    {
        // the risk-free curve settling T+2; on the market as given QuantLib gives 11.17079768558903, 2.343793580575789,
        // 1.8031106776761405 and 11.70570620385384
        GeneralizedBlackScholesProcess process = Process(new FlatForward(Settlement, 0.05, A365));
        Check(process, [11.169471882477193, 2.345465860856291, 1.806904442756528, 11.702147162186597], 1e-15);
    }

    [Fact]
    public void OnStructuresDatedLaterTheEngineIsQuantLibsOnTheRestatedMarket()
    {
        // the dividend curve and the volatility dated T+2: QuantLib on the restated market, its variance curve with a
        // node at the exercise; with nodes at T+2 and ten years alone it interpolates the variance and rounds up to
        // 1.8e-14 apart (11.168259889389148, ...). On the market as given QuantLib gives 11.16693416795971,
        // 2.333666508159107, 1.817645569707967 and 11.677111309406321
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Settlement, 0.02, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Settlement, new NullCalendar(), 0.25, A365));
        Check(process, [11.168259889389144, 2.332039397850544, 1.813797809934174, 11.68077116560208], 1e-14);
    }

    [Fact]
    public void AnExerciseBeforeTheRiskFreeCurvesDateIsPriced()
    {
        // a call expiring on 16 October beside the risk-free curve settling T+2: QuantLib throws "negative time
        // (-0.00821918) given"; on the restated market the knock-outs are 0.5192867322841082, the knock-ins their
        // rebate, 2.5, undiscounted
        GeneralizedBlackScholesProcess process = Process(new FlatForward(Settlement, 0.05, A365));
        var exercise = new EuropeanExercise(Today + 1);
        Assert.Equal(0.5192867322841082, Npv(BarrierType.DownOut, 85.0, 2.5, process, exercise), 1e-15);
        Assert.Equal(2.5, Npv(BarrierType.DownIn, 85.0, 2.5, process, exercise), 1e-15);
        Assert.Equal(0.5192867322841082, Npv(BarrierType.UpOut, 120.0, 2.5, process, exercise), 1e-15);
        Assert.Equal(2.5, Npv(BarrierType.UpIn, 120.0, 2.5, process, exercise), 1e-15);
    }

    [Fact]
    public void AnImpliedVolatilityOnACurveSettlingLaterIsTheOneOnTheRestatedMarket()
    {
        var option = new BarrierOption(
            BarrierType.DownOut, 85.0, 2.5, new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise);

        // the restated market's price at 25%: QuantLib solves 0.24999999999999986 on the restated market, and
        // 0.2499474175003563 on the market as given
        double volatility = option.ImpliedVolatility(
            11.169471882477193, Process(new FlatForward(Settlement, 0.05, A365)), Context, 1e-10);
        Assert.Equal(0.24999999999999986, volatility, 1e-10);
    }

    // a one-year call at 100 with a rebate of 2.5, the down-and-out and down-and-in at 85, the up-and-out and up-and-in
    // at 120: QuantLib's engine on the restated market, in that order
    private static void Check(GeneralizedBlackScholesProcess process, double[] quantLib, double tolerance)
    {
        Assert.Equal(quantLib[0], Npv(BarrierType.DownOut, 85.0, 2.5, process, Exercise), tolerance);
        Assert.Equal(quantLib[1], Npv(BarrierType.DownIn, 85.0, 2.5, process, Exercise), tolerance);
        Assert.Equal(quantLib[2], Npv(BarrierType.UpOut, 120.0, 2.5, process, Exercise), tolerance);
        Assert.Equal(quantLib[3], Npv(BarrierType.UpIn, 120.0, 2.5, process, Exercise), tolerance);
    }

    // spot 100, dividend yield 2% and volatility 25% on Actual/365 Fixed, dated today
    private static BlackScholesMertonProcess Process(YieldTermStructure riskFree) => new(
        100.0, new FlatForward(
            Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static double Npv(
        BarrierType type, double barrier, double rebate, GeneralizedBlackScholesProcess process, Exercise exercise) =>
        AnalyticBarrierEngine.Calculate(
            new BarrierOption(type, barrier, rebate, new PlainVanillaPayoff(OptionType.Call, 100.0), exercise),
            Context,
            process).NPV!.Value;
}
