// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The BSM-Hull-White engine's variance offset on structures counted differently. QuantLib takes the Hull-White terms
/// over the risk-free curve's time and the equity volatility in the covariance as its own structure quotes it
/// (<c>analyticbsmhullwhiteengine.cpp:86-102</c>), so a volatility on Act/360 beside a curve on Act/365 enters the
/// covariance at a rate its variance doesn't run at. nanoql takes the equity volatility as its variance per unit of the
/// risk-free curve's time. QuantLib's check: its own engine on the market restated on one day counter, the volatility
/// scaled by √(365/360) (QuantLib-Python 1.44). Where the structures share their counts, QuantLib's bits (the engine's
/// vectors).
/// </summary>
public class BsmHullWhiteMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(OptionType.Call, 35.18070135379839)] // QuantLib on Act/360: 35.175212963677936
    [InlineData(OptionType.Put, 13.96069201726354)] // 13.955203627143085
    public void TheCovarianceReadsTheVolatilityPerTheCurvesTime(OptionType type, double restated)
    {
        // q 2% and r 5% on Act/365, volatility 25% on Act/360, a = 0.1, σ = 1%, ρ = 0.3, ten years
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, new Actual360()));
        var option = new VanillaOption(new PlainVanillaPayoff(type, 100.0), new EuropeanExercise(Today + 3650));
        double value = AnalyticBSMHullWhiteEngine.Calculate(option, Context, 0.3, process, new HullWhite(riskFree, 0.1, 0.01))
            .NPV!.Value;
        Assert.True(Math.Abs(value - restated) <= 1e-13 * restated, $"{value} against QuantLib's restated {restated}");
    }
}
