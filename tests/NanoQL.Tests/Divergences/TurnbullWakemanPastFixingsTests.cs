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

    [Fact]
    public void TestTurnbullWakemanAllFixingsInThePast()
    {
        // Upstream: QuantLib's master's case for #2954 (asianoptions.cpp's testTurnbullWakemanAllFixingsInThePast), the
        // master's fix of this crash: its known payoff discounted, nanoql's since BR5 (xxix)
        DayCounter dc = new Actual365Fixed();
        var rTS = new FlatForward(Today, 0.03, dc);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, dc), rTS, new BlackConstantVol(Today, new NullCalendar(), 0.25, dc));

        var fixingDates = new List<Date>();
        for (int i = 12; i > 0; --i)
        {
            fixingDates.Add(Today - (30 * i));
        }

        double[] pastFixings = [.. Enumerable.Repeat(100.0, 12)];
        var exercise = new EuropeanExercise(Today + 14);

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double strike in new[] { 95.0, 100.0, 105.0 })
            {
                var payoff = new PlainVanillaPayoff(type, strike);
                var option = new DiscreteAveragingAsianOption(AverageType.Arithmetic, fixingDates, payoff, exercise, pastFixings);
                OneAssetOptionResults results = TurnbullWakemanAsianEngine.Calculate(option, Context, process);

                // The fixed average is 100; only discounting to the payment date remains.
                double expected = payoff.Evaluate(100.0) * rTS.Discount(exercise.LastDate());
                Assert.True(Math.Abs(results.NPV!.Value - expected) < 1.0e-12, $"type: {type}, strike: {strike}");
                Assert.Equal(0.0, results.Delta);
                Assert.Equal(0.0, results.Gamma);
            }
        }
    }
}
