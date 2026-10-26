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
/// The analytic lookback engines at zero volatility (the S-bar plan's BR5 (xxiv), #2853's class). Each of QuantLib's
/// formulas divides by the volatility and is NaN at σ = 0: on spot 100, r 5%, q 1% over a year, its floating call is
/// NaN, 3.8820409248455316 at σ = 1e-8 (the planner's P8, rerun as the S-bar run's <c>c2_zerovol.py</c>). nanoql takes
/// each engine's limit below machine-epsilon variance, the payoff on the forward path, which is monotone, so its
/// extremum over the window lies at an end: the floating call's minimum the spot, the fixed call's maximum the forward,
/// the partial fixed call's the forward, the partial floating call's minimum over [0, t₁] the spot. Check: QuantLib's
/// values at σ = 1e-8.
/// </summary>
public class LookbackZeroVolatilityTests
{
    private static readonly Date Today = new(1, January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.01, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.0, A365));

    [Theory]
    [InlineData("floating call", 3.8820409248455316)]
    [InlineData("floating put", 1.2375622921864547e-13)]
    [InlineData("fixed call", 3.8820409248455365)]
    [InlineData("fixed put", 4.756147122503688)]
    [InlineData("partial fixed call", 3.8820409248455365)]
    [InlineData("partial floating call", 3.8820409248455316)]
    public void EachEngineTakesTheForwardPathsPayoff(string option, double quantLibAtOneE8)
    {
        double value = option switch
        {
            "floating call" => AnalyticContinuousFloatingLookbackEngine.Calculate(
                new ContinuousFloatingLookbackOption(100.0, new FloatingTypePayoff(OptionType.Call), AYear), Context, Process).NPV!.Value,
            "floating put" => AnalyticContinuousFloatingLookbackEngine.Calculate(
                new ContinuousFloatingLookbackOption(100.0, new FloatingTypePayoff(OptionType.Put), AYear), Context, Process).NPV!.Value,
            "fixed call" => AnalyticContinuousFixedLookbackEngine.Calculate(
                new ContinuousFixedLookbackOption(100.0, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear), Context, Process).NPV!.Value,
            "fixed put" => AnalyticContinuousFixedLookbackEngine.Calculate(
                new ContinuousFixedLookbackOption(100.0, new PlainVanillaPayoff(OptionType.Put, 105.0), AYear), Context, Process).NPV!.Value,
            "partial fixed call" => AnalyticContinuousPartialFixedLookbackEngine.Calculate(
                new ContinuousPartialFixedLookbackOption(Today + 183, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear),
                Context,
                Process).NPV!.Value,
            _ => AnalyticContinuousPartialFloatingLookbackEngine.Calculate(
                new ContinuousPartialFloatingLookbackOption(100.0, 1.0, Today + 183, new FloatingTypePayoff(OptionType.Call), AYear),
                Context,
                Process).NPV!.Value,
        };
        Assert.True(double.IsFinite(value));
        Assert.Equal(quantLibAtOneE8, value, 1e-10);
    }
}
