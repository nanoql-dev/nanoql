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
/// Turnbull-Wakeman with every fixing past (the S-bar plan's BR5 (xxix), fixed under R8). Where the effective strike is
/// positive, QuantLib's engine reads <c>times.back()</c> of an empty vector of fixing times and crashes the process
/// (0xC0000005, the planner's BP19); its certain-exercise branch covers only an effective strike at or below zero.
/// nanoql prices the known payoff discounted, as QuantLib's Choi engine prices it (<c>choiasianengine.cpp:71-77</c>):
/// on spot 100, q 3%, r 6%, σ 20% (Act/360), a put at 110 on two past fixings at 95 and 105, exercised in a month.
/// Check: Choi's engine, and the payoff on the average discounted.
/// </summary>
public class TurnbullWakemanPastFixingsTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.03, A360), new FlatForward(Today, 0.06, A360), new BlackConstantVol(Today, new NullCalendar(), 0.20, A360));

    [Fact]
    public void EveryFixingPastPricesTheKnownPayoff()
    {
        var option = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, [Today - 60, Today - 30], new PlainVanillaPayoff(OptionType.Put, 110.0), new EuropeanExercise(Today + 30), [95.0, 105.0]);
        double known = 10.0 * Process.RiskFreeRate.Discount(Today + 30);
        OneAssetOptionResults tw = TurnbullWakemanAsianEngine.Calculate(option, Context, Process);
        Assert.Equal(known, tw.NPV!.Value, 1e-14);
        Assert.Equal(ChoiAsianEngine.Calculate(option, Context, Process).NPV, tw.NPV);
    }
}
