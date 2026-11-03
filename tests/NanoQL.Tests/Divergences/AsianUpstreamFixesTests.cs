// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to Turnbull and Wakeman's engine after the pin, which nanoql takes: #2954
/// (<c>f461b7852</c>) prices the last fixing on the evaluation date, at time 0, with a zero standard deviation, its σ
/// 0, its delta <c>discount · α · E[A] / S</c> and its spot volatilities read as volatilities. QuantLib 1.44 divides
/// the log moment ratio by the last time, 0, and its Black calculator refuses the NaN standard deviation
/// (<c>turnbullwakemanasianengine.cpp:117-120</c>). nanoql reads the spot volatilities at the future average's own
/// strike, where master reads them at K − A/m; on a flat volatility they agree. Master's every-fixing-past branch
/// refuses an option with no fixings at all, "no fixings given", which nanoql's
/// <see cref="DiscreteAveragingAsianOption"/> accepts and 1.44's engine priced on an average of 0.
/// </summary>
public class AsianUpstreamFixesTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AnOptionWithNoFixingsIsRefused()
    {
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, dc), new FlatForward(Today, 0.03, dc), new BlackConstantVol(Today, new NullCalendar(), 0.25, dc));
        var option = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, [], new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 30));
        Assert.Equal(
            "no fixings given",
            Assert.Throws<NanoQLException>(() => TurnbullWakemanAsianEngine.Calculate(option, Context, process)).Message);
    }

    [Fact]
    public void TestTurnbullWakemanLastFixingToday()
    {
        // Upstream: QuantLib's master's case for #2954 (asianoptions.cpp's testTurnbullWakemanLastFixingToday)
        DayCounter dc = new Actual365Fixed();
        var rTS = new FlatForward(Today, 0.03, dc);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, dc), rTS, new BlackConstantVol(Today, new NullCalendar(), 0.25, dc));

        var fixingDates = new List<Date>();
        for (int i = 5; i > 0; --i)
        {
            fixingDates.Add(Today - (30 * i));
        }

        fixingDates.Add(Today);
        double[] pastFixings = [.. Enumerable.Repeat(100.0, 5)];
        var exercise = new EuropeanExercise(Today + 2);

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double strike in new[] { 95.0, 105.0 })
            {
                string label = $"type: {type}, strike: {strike}";
                var payoff = new PlainVanillaPayoff(type, strike);
                var option = new DiscreteAveragingAsianOption(AverageType.Arithmetic, fixingDates, payoff, exercise, pastFixings);
                OneAssetOptionResults results = TurnbullWakemanAsianEngine.Calculate(option, Context, process);

                // Today's fixing equals spot, so the average is (5 * 100 + 100) / 6.
                double discount = rTS.Discount(exercise.LastDate());
                double expected = payoff.Evaluate(100.0) * discount;
                double expectedDelta = expected > 0.0 ? (type == OptionType.Call ? 1 : -1) * discount / 6.0 : 0.0;
                Assert.True(Math.Abs(results.NPV!.Value - expected) < 1.0e-12, label);
                Assert.True(Math.Abs(results.Delta!.Value - expectedDelta) < 1.0e-12, label);
                Assert.Equal(0.0, results.Gamma);
                Assert.Equal(0.0, (double)results.AdditionalResults["sigma"]);
                var spotVols = (ImmutableArray<double>)results.AdditionalResults["spotVols"];
                Assert.Single(spotVols);
                Assert.True(Math.Abs(spotVols[0] - 0.25) < 1.0e-12, label);
            }
        }
    }
}
