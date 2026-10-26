// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference Heston barrier engines on structures that measure time differently (the S-bar plan's BR5
/// (iv), the S-hes plan's HR7 form, fixed under R8). QuantLib reads the process's time, the risk-free curve's, and the
/// dividend curve at it (<c>fdhestonbarrierengine.cpp:72</c>), as though the two curves shared a clock. nanoql's times
/// run on <c>FdmHestonOp.Clock</c>, each curve read at the instants they denote. QuantLib's check: its own engine on the
/// market restated onto the risk-free curve's day counter, the dividend rate scaled so its discounts by date are
/// unchanged; and a risk-free curve settling two days after the dividend curve, read from its own date on the clock
/// from the earliest, where QuantLib's times start at the risk-free curve's date (QuantLib's values from the S-bar run's
/// <c>c1_clock_later.py</c>).
/// </summary>
public class FdHestonBarrierMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AnAct365DividendCurveBesideAnAct360CurvePricesAsOnTheRestatedMarket()
    {
        var option = new BarrierOption(
            BarrierType.DownOut, 90.0, 2.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        var mismatched = new HestonModel(new HestonProcess(
            new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.04, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.5));
        var restated = new HestonModel(new HestonProcess(
            new FlatForward(Today, 0.05, A360),
            new FlatForward(Today, 0.04 * 360.0 / 365.0, A360),
            100.0,
            0.04,
            1.5,
            0.04,
            0.3,
            -0.5));
        double expected = FdHestonBarrierEngine.Calculate(option, Context, restated, 50, 100, 30).NPV!.Value;
        double calculated = FdHestonBarrierEngine.Calculate(option, Context, mismatched, 50, 100, 30).NPV!.Value;
        Assert.Equal(expected, calculated, 1e-11);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        var option = new BarrierOption(
            BarrierType.DownOut, 90.0, 2.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        HestonModel Model(YieldTermStructure riskFree) => new(new HestonProcess(
            riskFree, new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.5));
        double expected = FdHestonBarrierEngine.Calculate(option, Context, Model(SettlingLaterRestated()), 50, 100, 30).NPV!.Value;
        Assert.Equal(8.31784695856607, expected, 1e-9); // QuantLib restated; on this market, 8.318888433526231
        double calculated = FdHestonBarrierEngine.Calculate(option, Context, Model(SettlingLater()), 50, 100, 30).NPV!.Value;
        Assert.Equal(expected, calculated, 1e-10);
    }

    // the risk-free curve settling two days later; QuantLib's restated market: every structure today, the risk-free
    // discount 1 to T+2 and 5% after (a log-linear discount curve)
    private static YieldTermStructure SettlingLater() => new FlatForward(Today + 2, 0.05, A365);

    private static YieldTermStructure SettlingLaterRestated() => new DiscountCurve(
        [Today, Today + 2, Today + 3652], [1.0, 1.0, Math.Exp(-0.05 * 3650.0 / 365.0)], A365);
}
