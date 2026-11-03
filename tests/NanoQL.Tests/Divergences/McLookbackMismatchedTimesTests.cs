// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo lookback engine's grid on structures that measure time differently (fixed). QuantLib's grid runs on
/// the risk-free curve's time and its process reads the other structures at it, as though they shared that clock.
/// nanoql's paths and discount run on the engine's clock, <c>FdmBlackScholesOp.Clock</c>, each structure read at the
/// instants its times denote. Check: the engine on the market restated onto the clock, the dividend rate and the
/// volatility scaled so their discounts and variances by date are unchanged, on the same seed: the paths draw the same
/// numbers, so the prices agree to rounding; and a risk-free curve settling two days after the others, read from its
/// own date on the clock from the earliest, where QuantLib's grid starts at the risk-free curve's date, against the
/// market restated with every structure today.
/// </summary>
public class McLookbackMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void Act365StructuresBesideAnAct360CurvePriceAsOnTheRestatedMarket()
    {
        var option = new ContinuousFixedLookbackOption(100.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        var mismatched = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02, A365),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var restated = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02 * 360.0 / 365.0, A360),
            new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25 * Math.Sqrt(360.0 / 365.0), A360));
        double Price(GeneralizedBlackScholesProcess process) => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 50, null, false, false, 5000, null, null, 42).NPV!.Value;
        Assert.Equal(Price(restated), Price(mismatched), 1e-11);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        var option = new ContinuousFixedLookbackOption(100.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        GeneralizedBlackScholesProcess Process(YieldTermStructure riskFree) => new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        double Price(GeneralizedBlackScholesProcess process) => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 50, null, false, false, 5000, null, null, 42).NPV!.Value;

        // the risk-free curve settling two days later, and the market restated with every structure today: the
        // risk-free discount 1 to T+2 and 5% after (a log-linear discount curve)
        var restated = new DiscountCurve([Today, Today + 2, Today + 3652], [1.0, 1.0, Math.Exp(-0.05 * 3650.0 / 365.0)], A365);
        Assert.Equal(Price(Process(restated)), Price(Process(new FlatForward(Today + 2, 0.05, A365))), 1e-11);
    }
}
