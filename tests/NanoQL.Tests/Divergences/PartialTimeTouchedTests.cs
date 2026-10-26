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
/// A start-window partial-time barrier with the spot beyond its barrier (the S-bar plan's BR5 (xiv), refused under R8).
/// QuantLib checks no trigger and prices −4.174446 for a down-and-out call at spot 80, barrier 85 (the planner's P2),
/// where the family's analytic, binomial and Monte Carlo engines refuse "barrier touched". nanoql refuses it so; the end
/// windows, whose monitoring starts at the cover event, are unaffected.
/// </summary>
public class PartialTimeTouchedTests
{
    [Fact]
    public void AStartWindowSpotBeyondTheBarrierIsRefused()
    {
        var today = new Date(16, September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            80.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var exercise = new EuropeanExercise(today + 365);
        var start = new PartialTimeBarrierOption(
            BarrierType.DownOut, PartialBarrierRange.Start, 85.0, 0.0, today + 120, payoff, exercise);
        var e = Assert.Throws<NanoQLException>(() => AnalyticPartialTimeBarrierOptionEngine.Calculate(start, context, process));
        Assert.Equal("barrier touched", e.Message);

        var end = new PartialTimeBarrierOption(
            BarrierType.DownOut, PartialBarrierRange.EndB1, 85.0, 0.0, today + 120, payoff, exercise);
        Assert.True(double.IsFinite(AnalyticPartialTimeBarrierOptionEngine.Calculate(end, context, process).NPV!.Value));
    }
}
