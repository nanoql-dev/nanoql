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
/// The analytic lookback engines near zero carry (the S-bar plan's BR5 (xxiii), the user's ruling (b) of 2026-10-26).
/// As the carry b = r − q vanishes, each formula's terms over 2b/σ² cancel: over a year at σ 20%, QuantLib's floating
/// call (spot 100, minimum 95) is off its 50-digit value by 2.0e-9 at b = 1e-7 and 1.4e-4 at 1e-12, its partial fixed
/// call (strike 100, the window from half a year) by 1.4e-9 and 1.9e-4, and every engine is NaN at b = 0 (#2856 takes
/// the full engines' limit below |λ| of 1e-7; the partial engines stay NaN). nanoql evaluates the full engines'
/// formulas in double-double below u = |2b√T/σ| of 1e-3, the cubic through double-double values below 1e-18, and
/// #2856's limit at 0; and the partial engines' as the cubic in b through QuantLib's double values at ±c and ±2c, c the
/// carry at a cut of 6e-4 to 2e-3 in u. Checks: the formulas at 50 digits, the bivariate normal by quadrature (the
/// S-bar run's <c>c2_smallcarry_refs.py</c>), within 1e-12 of max(|value|, 1); over the run's fit, corner and holdout
/// grids the worst is 1.6e-13 for the full engines and 1.1e-12 for the partial ones (<c>c2_smallcarry_cut.py</c>,
/// <c>c2_smallcarry_rule.py</c>, and the engines on the holdout grid).
/// </summary>
public class LookbackSmallCarryTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

    [Theory]
    [InlineData(1e-6, 14.682617066146925)]
    [InlineData(-1e-6, 14.682502767103997)]
    [InlineData(1e-7, 14.682565631484879)]
    [InlineData(1e-12, 14.682559916588946)]
    [InlineData(0.0, 14.682559916531796)]
    public void TheFloatingLookbackHoldsItsDigits(double carry, double expected)
    {
        var option = new ContinuousFloatingLookbackOption(95.0, new FloatingTypePayoff(OptionType.Call), AYear);
        Check(expected, AnalyticContinuousFloatingLookbackEngine.Calculate(option, Context, Process(carry)).NPV!.Value);
    }

    [Theory]
    [InlineData(1e-6, 4.2830835517775373)]
    [InlineData(-1e-6, 4.2830212190641116)]
    [InlineData(1e-7, 4.2830555019671722)]
    [InlineData(1e-12, 4.2830523853617650)]
    [InlineData(0.0, 4.2830523853305987)]
    public void TheFixedLookbackHoldsItsDigits(double carry, double expected)
    {
        var option = new ContinuousFixedLookbackOption(100.0, new PlainVanillaPayoff(OptionType.Call, 120.0), AYear);
        Check(expected, AnalyticContinuousFixedLookbackEngine.Calculate(option, Context, Process(carry)).NPV!.Value);
    }

    [Theory]
    [InlineData(1e-6, 0.42719153767830216)]
    [InlineData(-1e-6, 0.42718102354682950)]
    [InlineData(1e-7, 0.42718680629097442)]
    [InlineData(1e-12, 0.42718628058937337)]
    [InlineData(0.0, 0.42718628058411630)]
    public void ThePartialFloatingLookbackHoldsItsDigitsAndPricesZeroCarry(double carry, double expected)
    {
        var option = new ContinuousPartialFloatingLookbackOption(95.0, 1.5, Today + 182, new FloatingTypePayoff(OptionType.Call), AYear);
        Check(expected, AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, Context, Process(carry)).NPV!.Value);
    }

    [Theory]
    [InlineData(1e-6, 13.599911405005754)]
    [InlineData(-1e-6, 13.599784685823246)]
    [InlineData(1e-7, 13.599854381279810)]
    [InlineData(1e-12, 13.599848045383096)]
    [InlineData(0.0, 13.599848045319737)]
    public void ThePartialFixedLookbackHoldsItsDigitsAndPricesZeroCarry(double carry, double expected)
    {
        var option = new ContinuousPartialFixedLookbackOption(Today + 182, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        Check(expected, AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, Context, Process(carry)).NPV!.Value);
    }

    private static void Check(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= 1e-12 * Math.Max(Math.Abs(expected), 1.0), $"expected {expected:R}, got {actual:R}");

    // spot 100, r 5%, q = r − b, σ 20%, on Actual/365 (Fixed) from the evaluation date
    private static GeneralizedBlackScholesProcess Process(double carry) => new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.05 - carry, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));
}
