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
/// The partial-time end-window knock-ins (a refusal lifted). QuantLib refuses "Down-and-in partial-time end barrier is
/// not implemented" and its up twin (<c>analyticpartialtimebarrieroptionengine.cpp:66</c>, <c>:91</c>), where it prices
/// the start window's knock-in as its European less its knock-out (<c>CIA</c>). nanoql applies the same parity to
/// QuantLib's end-window knock-out: B1 at any strike, B2 wherever QuantLib prices its knock-out. Checks: the parity on
/// QuantLib's own engines, and an independent Monte Carlo for a call at 100, barrier 90, window [6 months, 1 year], r
/// 5%, q 2%, σ 25%: 0.934510 ± 0.019692, against parity's 11.123762 − 10.161263 = 0.962499.
/// </summary>
public class PartialTimeEndKnockInTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    [Fact]
    public void AB1KnockInIsTheEuropeanLessItsKnockOut()
    {
        var exercise = new EuropeanExercise(Today + 365);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        Date cover = Today + 182;
        double knockIn = Price(BarrierType.DownIn, PartialBarrierRange.EndB1, 90.0, cover, payoff, exercise);
        double knockOut = Price(BarrierType.DownOut, PartialBarrierRange.EndB1, 90.0, cover, payoff, exercise);
        double european = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, exercise), Context, Process).NPV!.Value;
        Assert.Equal(european - knockOut, knockIn, 1e-12);

        // the independent Monte Carlo: 0.934510 ± 0.019692
        Assert.True(Math.Abs(knockIn - 0.934510) <= 3 * 0.019692, $"{knockIn}");
    }

    [Fact]
    public void AB2KnockInIsTheEuropeanLessItsKnockOut()
    {
        var exercise = new EuropeanExercise(Today + 365);
        foreach ((BarrierType knockIn, BarrierType knockOut, double barrier, OptionType type, double strike) in new[]
        {
            (BarrierType.DownIn, BarrierType.DownOut, 90.0, OptionType.Call, 85.0),
            (BarrierType.UpIn, BarrierType.UpOut, 110.0, OptionType.Put, 115.0),
        })
        {
            var payoff = new PlainVanillaPayoff(type, strike);
            double inValue = Price(knockIn, PartialBarrierRange.EndB2, barrier, Today + 182, payoff, exercise);
            double outValue = Price(knockOut, PartialBarrierRange.EndB2, barrier, Today + 182, payoff, exercise);
            double european = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, exercise), Context, Process).NPV!.Value;
            Assert.Equal(european - outValue, inValue, 1e-12);
        }
    }

    private static double Price(
        BarrierType type, PartialBarrierRange range, double barrier, Date cover, PlainVanillaPayoff payoff, Exercise exercise) =>
        AnalyticPartialTimeBarrierOptionEngine.Calculate(
            new PartialTimeBarrierOption(type, range, barrier, 0.0, cover, payoff, exercise), Context, Process).NPV!.Value;
}
