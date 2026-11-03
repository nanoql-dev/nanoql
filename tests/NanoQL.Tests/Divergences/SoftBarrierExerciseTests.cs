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
/// The soft barrier engine's exercise. QuantLib's engine never reads the exercise's type and prices any exercise to its
/// last date: a down-and-in call at 90 on the band [85, 90], an American exercise to a year, prices 2.906045178994 on
/// the vectors' first market, its European value; its own implied volatility refuses a non-European soft barrier
/// ("engine not available for non-European soft barrier option"). nanoql refuses a non-European exercise, as the
/// family's European engines do: the soft barrier's average, the route off Haug's domain, runs on the analytic barrier
/// engine, which prices European options only.
/// </summary>
public class SoftBarrierExerciseTests
{
    [Fact]
    public void ANonEuropeanExerciseIsRefused()
    {
        var today = new Date(15, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.04, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.22, dc));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 90.0);
        var american = new SoftBarrierOption(BarrierType.DownIn, 85.0, 90.0, payoff, new AmericanExercise(today, today + 365));
        var e = Assert.Throws<NanoQLException>(() => AnalyticSoftBarrierEngine.Calculate(american, context, process));
        Assert.Equal("only european style option are supported", e.Message);

        // QuantLib's value for the American is the European's
        var european = new SoftBarrierOption(BarrierType.DownIn, 85.0, 90.0, payoff, new EuropeanExercise(today + 365));
        Assert.Equal(2.906045178994219, AnalyticSoftBarrierEngine.Calculate(european, context, process).NPV!.Value, 1e-10);
    }
}
