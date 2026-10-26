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
/// The analytic lookback engines at small, non-zero volatilities (the S-bar code review's B2-1, BR5 (xxiv)'s class,
/// fixed under R8). λ = 2b/σ² grows as σ falls, so QuantLib's s^(−λ) overflows to +∞ while its normal or bivariate
/// factor underflows to 0, and their product is NaN where the value is finite: on spot 100, r 5%, q 1% over a year, a
/// fixed call struck at 105 with a running maximum of 110 is 4.75614712250357 at σ 0.3% and NaN in QuantLib, as is
/// every engine from about σ 0.3% at a moneyness of 1.1 (σ² below 2|b ln s|/709). nanoql takes such a product in logs,
/// exp(a ln s + ln P), ln P finite in the tails (<c>LookbackTails</c>), where QuantLib's isn't finite, QuantLib's
/// product and bits elsewhere. Check: QuantLib's formulas at 80 digits, the bivariate normal by quadrature outward from
/// its peak (the S-bar run's <c>c2_smallvol_refs2.py</c> and <c>c2_tiny_refs.py</c>; the reviewer's agree on the full
/// engines), within 1e-12 of max(|value|, 1), and the values far below 1 to 1e-9 relative. The partial fixed call at
/// 110 is the full one's to 20 digits, its window's opening, half a year in, unreached at these volatilities. The rows
/// at σ 1e-4 and 1e-6 hold the bivariate normal's arguments of order 1e4 and 1e5, where its exponent's terms of order
/// h² and h must cancel analytically.
/// </summary>
public class LookbackSmallVolatilityTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

    [Theory]
    [InlineData(OptionType.Call, 105.0, 110.0, 0.003, 4.7561471225035700323)]
    [InlineData(OptionType.Call, 110.0, 100.0, 0.003, 7.7404368278035699174e-78)]
    [InlineData(OptionType.Call, 105.0, 110.0, 0.001, 4.7561471225035700323)]
    public void TheFixedLookbackIsFinite(OptionType type, double strike, double minmax, double vol, double expected)
    {
        var option = new ContinuousFixedLookbackOption(minmax, new PlainVanillaPayoff(type, strike), AYear);
        Check(expected, AnalyticContinuousFixedLookbackEngine.Calculate(option, Context, Process(vol)).NPV!.Value);
    }

    [Theory]
    [InlineData(OptionType.Put, 110.0, 0.003, 5.6302533201617353729)]
    [InlineData(OptionType.Put, 110.0, 0.001, 5.6302533201617353729)]
    public void TheFloatingLookbackIsFinite(OptionType type, double minmax, double vol, double expected)
    {
        var option = new ContinuousFloatingLookbackOption(minmax, new FloatingTypePayoff(type), AYear);
        Check(expected, AnalyticContinuousFloatingLookbackEngine.Calculate(option, Context, Process(vol)).NPV!.Value);
    }

    [Theory]
    [InlineData(OptionType.Call, 110.0, 0.003, 7.7404368278035699174e-78)]
    [InlineData(OptionType.Call, 110.0, 0.001, 9.3568415810189838119e-670)]
    [InlineData(OptionType.Call, 90.0, 1e-4, 13.394347545475467)]
    [InlineData(OptionType.Put, 110.0, 1e-4, 7.5960128342225521)]
    [InlineData(OptionType.Put, 110.0, 1e-6, 7.5960007055310437512)]
    public void ThePartialFixedLookbackIsFinite(OptionType type, double strike, double vol, double expected)
    {
        var option = new ContinuousPartialFixedLookbackOption(Today + 182, new PlainVanillaPayoff(type, strike), AYear);
        Check(expected, AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, Context, Process(vol)).NPV!.Value);
    }

    [Theory]
    [InlineData(OptionType.Put, 110.0, 0.9, 0.003, 1.4908632889608838194e-64)]
    [InlineData(OptionType.Call, 90.0, 1.1, 0.003, 4.8332703493461110944)]
    [InlineData(OptionType.Call, 90.0, 1.1, 0.001, 4.8332703493461110944)]
    [InlineData(OptionType.Call, 90.0, 1.1, 1e-4, 4.8332703493461111)]
    [InlineData(OptionType.Call, 90.0, 1.1, 1e-6, 4.8332703493461110944)]
    [InlineData(OptionType.Put, 110.0, 1.0, 1e-4, 5.6302533201617354)]
    public void ThePartialFloatingLookbackIsFinite(OptionType type, double minmax, double lambda, double vol, double expected)
    {
        var option = new ContinuousPartialFloatingLookbackOption(minmax, lambda, Today + 182, new FloatingTypePayoff(type), AYear);
        Check(expected, AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, Context, Process(vol)).NPV!.Value);
    }

    // within 1e-12 of max(|value|, 1), and a value far below 1 (9.4e-670 rounds to 0 in double) to 1e-9 relative
    private static void Check(double expected, double actual)
    {
        Assert.True(double.IsFinite(actual), $"{actual} for {expected:R}");
        Assert.True(Math.Abs(actual - expected) <= 1e-12 * Math.Max(Math.Abs(expected), 1.0), $"{actual:R} for {expected:R}");
        if (expected != 0.0 && Math.Abs(expected) < 1.0)
        {
            Assert.True(Math.Abs(actual - expected) <= 1e-9 * Math.Abs(expected), $"{actual:R} for {expected:R}");
        }
    }

    private static GeneralizedBlackScholesProcess Process(double vol) => new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.01, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), vol, A365));
}
