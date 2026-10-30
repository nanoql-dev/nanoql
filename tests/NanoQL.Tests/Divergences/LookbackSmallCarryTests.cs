// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
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
/// <c>c2_smallcarry_rule.py</c>, and the engines on the holdout grid). QuantLib's master fixes the partial engines
/// after the pin (#2925, <c>2d86c20e3</c>): the limit below |2b/σ²| of 1e-7 and 1.44's form above it; nanoql keeps its
/// cubic (the user's ruling (b) of 2026-10-26), and the master's three cases below check it.
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

    [Fact]
    public void TestPartialLookbackZeroCarry()
    {
        // Upstream: QuantLib's master's case for #2925 (lookbackoptions.cpp's testPartialLookbackZeroCarry)
        var today = new Date(20, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();
        GeneralizedBlackScholesProcess ProcessAt(double q) => new BlackScholesMertonProcess(
            100.0, new FlatForward(today, q, dc), new FlatForward(today, 0.05, dc), new BlackConstantVol(today, new NullCalendar(), 0.20, dc));
        var exercise = new EuropeanExercise(today + 360);

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var fixedOption = new ContinuousPartialFixedLookbackOption(today + 180, new PlainVanillaPayoff(type, 100.0), exercise);
            var floating = new ContinuousPartialFloatingLookbackOption(100.0, 1.0, today + 180, new FloatingTypePayoff(type), exercise);

            // Discretely sampled extrema bias the MC reference downwards. The 0.60 tolerance allows for that bias and
            // sampling error on a spot of 100.
            foreach (double yield in new[] { 0.02, 0.05 })
            {
                GeneralizedBlackScholesProcess process = ProcessAt(yield);
                double fixedReference = new MakeMCLookbackEngine<ContinuousPartialFixedLookbackOption, PseudoRandom, GeneralStatistics>(process, 42)
                    .WithSteps(1024).WithSamples(4095).WithAntitheticVariate().ToEngine()(fixedOption, context).NPV!.Value;
                double fixedValue = AnalyticContinuousPartialFixedLookbackEngine.Calculate(fixedOption, context, process).NPV!.Value;
                Assert.True(
                    Math.Abs(fixedValue - fixedReference) < 0.60,
                    $"partial fixed {type}, q={yield}: calculated {fixedValue}, MC reference {fixedReference}");
                double floatingReference = new MakeMCLookbackEngine<ContinuousPartialFloatingLookbackOption, PseudoRandom, GeneralStatistics>(process, 42)
                    .WithSteps(1024).WithSamples(4095).WithAntitheticVariate().ToEngine()(floating, context).NPV!.Value;
                double floatingValue = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(floating, context, process).NPV!.Value;
                Assert.True(
                    Math.Abs(floatingValue - floatingReference) < 0.60,
                    $"partial floating {type}, q={yield}: calculated {floatingValue}, MC reference {floatingReference}");
            }
        }
    }

    [Fact]
    public void TestPartialLookbackZeroCarryBoundaryLimits()
    {
        // Upstream: QuantLib's master's case for #2925 (lookbackoptions.cpp's testPartialLookbackZeroCarryBoundaryLimits)
        var today = new Date(20, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();
        GeneralizedBlackScholesProcess ProcessAt(double q) => new BlackScholesMertonProcess(
            100.0, new FlatForward(today, q, dc), new FlatForward(today, 0.05, dc), new BlackConstantVol(today, new NullCalendar(), 0.20, dc));
        var exercise = new EuropeanExercise(today + 360);

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var payoff = new PlainVanillaPayoff(type, 100.0);
            var fixedOption = new ContinuousPartialFixedLookbackOption(today + 360, payoff, exercise);
            var vanilla = new VanillaOption(payoff, exercise);
            var floatingPayoff = new FloatingTypePayoff(type);
            double minmax = type == OptionType.Call ? 90.0 : 110.0;
            var partial = new ContinuousPartialFloatingLookbackOption(minmax, 1.0, today + 360, floatingPayoff, exercise);
            var full = new ContinuousFloatingLookbackOption(minmax, floatingPayoff, exercise);
            foreach (double yield in new[] { 0.02, 0.05, 0.05 - 1.0e-12, 0.05 + 1.0e-12 })
            {
                GeneralizedBlackScholesProcess process = ProcessAt(yield);

                // A single observation is a vanilla option. Observing to expiry with unit lambda is the full floating
                // lookback contract.
                double fixedValue = AnalyticContinuousPartialFixedLookbackEngine.Calculate(fixedOption, context, process).NPV!.Value;
                double vanillaValue = AnalyticEuropeanEngine.Calculate(vanilla, context, process).NPV!.Value;
                Assert.True(Math.Abs(fixedValue - vanillaValue) < 1.0e-8, $"{type}, q={yield}: {fixedValue} against {vanillaValue}");
                double partialValue = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(partial, context, process).NPV!.Value;
                double fullValue = AnalyticContinuousFloatingLookbackEngine.Calculate(full, context, process).NPV!.Value;
                Assert.True(Math.Abs(partialValue - fullValue) < 1.0e-8, $"{type}, q={yield}: {partialValue} against {fullValue}");
            }
        }
    }

    [Fact]
    public void TestPartialLookbackZeroCarryContinuity()
    {
        // Upstream: QuantLib's master's case for #2925 (lookbackoptions.cpp's testPartialLookbackZeroCarryContinuity)
        var today = new Date(20, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();
        const double r = 0.05;
        const double sigma = 0.20;
        GeneralizedBlackScholesProcess ProcessAt(double q) => new BlackScholesMertonProcess(
            100.0, new FlatForward(today, q, dc), new FlatForward(today, r, dc), new BlackConstantVol(today, new NullCalendar(), sigma, dc));
        var exercise = new EuropeanExercise(today + 360);

        void CheckContinuity(Func<GeneralizedBlackScholesProcess, double> price, string label)
        {
            double ValueAt(double yield)
            {
                double value = price(ProcessAt(yield));
                Assert.True(double.IsFinite(value), $"{label}, q={yield}");
                return value;
            }

            double zero = ValueAt(r);
            foreach (double epsilon in new[] { 1.0e-4, 1.0e-6 })
            {
                double left = ValueAt(r - epsilon);
                double right = ValueAt(r + epsilon);

                // Both prices use the existing nonsingular formula. Individual differences are O(epsilon); their mean
                // is O(epsilon^2).
                Assert.True(Math.Abs(left - zero) < (100.0 * epsilon) + 1.0e-8, $"{label}, epsilon={epsilon}");
                Assert.True(Math.Abs(right - zero) < (100.0 * epsilon) + 1.0e-8, $"{label}, epsilon={epsilon}");
                Assert.True(
                    Math.Abs((0.5 * (left + right)) - zero) < (200.0 * epsilon * epsilon) + 1.0e-8, $"{label}, epsilon={epsilon}");
            }

            // Exercise both sides of the implementation's dimensionless cutoff.
            const double carryCutoff = 0.5 * sigma * sigma * 1.0e-7;
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                double inside = ValueAt(r + (sign * 0.99 * carryCutoff));
                double outside = ValueAt(r + (sign * 1.01 * carryCutoff));
                Assert.True(Math.Abs(outside - inside) < 1.0e-6, $"{label}, sign={sign}");
            }
        }

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (int boundary in new[] { 90, 180, 270, 360 })
            {
                foreach (double strike in new[] { 90.0, 100.0, 110.0 })
                {
                    var option = new ContinuousPartialFixedLookbackOption(today + boundary, new PlainVanillaPayoff(type, strike), exercise);
                    CheckContinuity(
                        p => AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, context, p).NPV!.Value,
                        $"fixed {type}, boundary={boundary}, strike={strike}");
                }

                foreach (double minmax in new[] { 100.0, type == OptionType.Call ? 90.0 : 110.0 })
                {
                    foreach (double lambda in new[] { 1.0, type == OptionType.Call ? 1.1 : 0.9 })
                    {
                        var option = new ContinuousPartialFloatingLookbackOption(
                            minmax, lambda, today + boundary, new FloatingTypePayoff(type), exercise);
                        CheckContinuity(
                            p => AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, context, p).NPV!.Value,
                            $"floating {type}, boundary={boundary}, minmax={minmax}, lambda={lambda}");
                    }
                }
            }
        }
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
