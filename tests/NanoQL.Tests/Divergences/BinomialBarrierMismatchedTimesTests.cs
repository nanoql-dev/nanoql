// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The binomial barrier engine's tree on structures that measure time differently (the S-bar plan's BR5 (ii), fixed
/// under R8). QuantLib flattens each curve from the risk-free curve's reference date on its own day counter and runs
/// the tree over the risk-free curve's time (<c>binomialbarrierengine.hpp:99-125</c>), so a volatility on another day
/// counter integrates the wrong variance. nanoql's tree is <see cref="BinomialVanillaEngine"/>'s: on the risk-free
/// curve's day counter from the earliest reference date, each structure on another day counter flattened on it from
/// its variance or discount at maturity. QuantLib's check: its own engine on the market restated onto the risk-free
/// curve's day counter, the volatility scaled so its variances by date are unchanged; and a risk-free curve settling
/// two days after the others, flattened from its own date on the tree from the earliest, where QuantLib's tree starts at
/// the risk-free curve's date (QuantLib's values from the S-bar run's
/// <c>c1_clock_later.py</c>).
/// </summary>
public class BinomialBarrierMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AnAct365VolatilityBesideAnAct360CurvePricesAsOnTheRestatedMarket()
    {
        var option = new BarrierOption(
            BarrierType.DownOut, 90.0, 2.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        var mismatched = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02, A360),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var restated = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02, A360),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25 * Math.Sqrt(360.0 / 365.0), A360));
        double expected = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
            option, Context, restated, 400).NPV!.Value;
        double calculated = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
            option, Context, mismatched, 400).NPV!.Value;
        Assert.Equal(expected, calculated, 1e-12);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        var option = new BarrierOption(
            BarrierType.DownOut, 90.0, 2.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        GeneralizedBlackScholesProcess Process(YieldTermStructure riskFree) => new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        // the Derman-Kani asset, which QuantLib-SWIG binds
        double expected = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniBarrierOption>(
            option, Context, Process(SettlingLaterRestated()), 400).NPV!.Value;
        Assert.Equal(9.459609696800934, expected, 1e-9); // QuantLib restated; on this market, 9.459692434755278
        double calculated = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniBarrierOption>(
            option, Context, Process(SettlingLater()), 400).NPV!.Value;
        Assert.Equal(expected, calculated, 1e-12);
    }

    // the risk-free curve settling two days later; QuantLib's restated market: every structure today, the risk-free
    // discount 1 to T+2 and 5% after (a log-linear discount curve)
    private static YieldTermStructure SettlingLater() => new FlatForward(Today + 2, 0.05, A365);

    private static YieldTermStructure SettlingLaterRestated() => new DiscountCurve(
        [Today, Today + 2, Today + 3652], [1.0, 1.0, Math.Exp(-0.05 * 3650.0 / 365.0)], A365);
}
