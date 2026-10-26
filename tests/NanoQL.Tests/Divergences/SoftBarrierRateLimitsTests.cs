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
/// The soft barrier's rate limits (the S-bar plan's BR8 (d), a refusal lifted under the user's rule of 2026-10-20).
/// QuantLib refuses "Interest rate must be between -5% and 100%" and "Dividend yield must be between -10% and 100%"
/// (<c>analyticsoftbarrierengine.cpp:174-175</c>), limits no formula needs. nanoql doesn't port them: every rate takes
/// the engine's routes, here the band average, Haug's form being NaN at b + σ² = 2.5e-3 and σ 1.0. Check: the band
/// average by Simpson's rule (<see cref="SoftBarrierBandAverage"/>), and the reviewer's r2a, 3.761756955001.
/// </summary>
public class SoftBarrierRateLimitsTests
{
    [Fact]
    public void ADividendYieldAbove100PercentIsPriced()
    {
        var today = new Date(16, September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 1.0075, dc), new FlatForward(today, 0.01, dc),
            new BlackConstantVol(today, new NullCalendar(), 1.0, dc));
        var exercise = new EuropeanExercise(today + 365);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        double value = AnalyticSoftBarrierEngine.Calculate(
            new SoftBarrierOption(BarrierType.DownIn, 85.0, 95.0, payoff, exercise), context, process).NPV!.Value;
        double expected = SoftBarrierBandAverage.KnockIn(BarrierType.DownIn, 85.0, 95.0, payoff, exercise, context, process);
        Assert.Equal(expected, value, 1e-10);
        Assert.Equal(3.761756955001, value, 1e-9);
    }
}
