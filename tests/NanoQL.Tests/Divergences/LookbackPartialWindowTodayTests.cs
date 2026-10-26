// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The partial lookbacks' windows opening or closing today or before it (the S-bar code review's B2-2, BR5 (xiii)'s
/// class for the partial-time barrier, fixed under R8). QuantLib's partial formulas take f₁ = 0/0 for a window date
/// today at the money and √t₁ of a negative time before it: on spot 100, r 5%, q 1%, σ 20% over a year, its partial
/// fixed call struck at 100 opening today and its partial floating call closing today are NaN, and a window dated
/// yesterday throws the bivariate normal's "rho must be >= -1.0 (NaN not allowed)"; its Monte Carlo engine prices a
/// window opened before today as opening today and folds today's spot into a window that has closed (the S-bar run's
/// <c>c2_window_today.py</c>; the reviewer's <c>q12_ql.py</c>). nanoql takes each limit: a fixed window opening today
/// is the full fixed lookback with the spot as its maximum; a floating window closing today the European at λ times the
/// minimum (or maximum) with the spot, and one closed before today the European at λ times the extremum as given; a
/// fixed window opened before today is refused, the option holding no extremum observed since. Checks: QuantLib's full
/// fixed engine and its analytic European engine, and the partial engine a day out. "Before today" is before the
/// evaluation date and "today" runs from it to the clock's origin, inclusive (the review's B2-3): on a market settling
/// two days after the evaluation date, a window dated on it or a day after takes today's limit, where reading a clock's
/// time refused the fixed window and dropped the spot from the floating one, 24% low; the checks the full fixed engine
/// and the European engine on that market, the Monte Carlo's full fixed lookback on the same paths to the bit.
/// </summary>
public class LookbackPartialWindowTodayTests
{
    private const string Opened = "lookback period started before the evaluation date: no extremum observed since its start is given";

    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.01, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    // every structure settling two days after the evaluation date, each clock's origin there
    private static readonly GeneralizedBlackScholesProcess Later = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today + 2, 0.01, A365),
        new FlatForward(Today + 2, 0.05, A365),
        new BlackConstantVol(Today + 2, new NullCalendar(), 0.20, A365));

    [Theory]
    [InlineData(100.0, 18.527987747322328, 18.518757931050956)]
    [InlineData(105.0, 14.186766229135369, 14.186766226768356)]
    public void AFixedWindowOpeningTodayIsTheFullLookback(double strike, double quantLibFull, double quantLibTomorrow)
    {
        var payoff = new PlainVanillaPayoff(OptionType.Call, strike);
        double today = AnalyticContinuousPartialFixedLookbackEngine.Calculate(
            new ContinuousPartialFixedLookbackOption(Today, payoff, AYear), Context, Process).NPV!.Value;
        Assert.Equal(quantLibFull, today, 1e-12);
        double tomorrow = AnalyticContinuousPartialFixedLookbackEngine.Calculate(
            new ContinuousPartialFixedLookbackOption(Today + 1, payoff, AYear), Context, Process).NPV!.Value;
        Assert.Equal(quantLibTomorrow, tomorrow, 1e-12);
    }

    [Theory]
    [InlineData(100.0, 9.826297782739136, 10.251004131239412)]
    [InlineData(95.0, 12.630520058070891, 12.630520148455668)]
    public void AFloatingWindowClosingTodayIsTheEuropean(double minmax, double quantLibEuropean, double quantLibTomorrow)
    {
        var payoff = new FloatingTypePayoff(OptionType.Call);
        double today = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(
            new ContinuousPartialFloatingLookbackOption(minmax, 1.0, Today, payoff, AYear), Context, Process).NPV!.Value;
        Assert.Equal(quantLibEuropean, today, 1e-12);
        double tomorrow = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(
            new ContinuousPartialFloatingLookbackOption(minmax, 1.0, Today + 1, payoff, AYear), Context, Process).NPV!.Value;
        Assert.Equal(quantLibTomorrow, tomorrow, 1e-12);
    }

    [Fact]
    public void AFixedWindowOpenedBeforeTodayIsRefused()
    {
        var option = new ContinuousPartialFixedLookbackOption(Today - 30, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        Assert.Equal(
            Opened, Assert.Throws<NanoQLException>(() => AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, Context, Process)).Message);
        Assert.Equal(
            Opened,
            Assert.Throws<NanoQLException>(() => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
                option, Context, Process, 4, null, false, true, 1000, null, null, 42)).Message);
    }

    [Theory]
    [InlineData(OptionType.Call, 105.0, 7.491693155007908)]
    [InlineData(OptionType.Put, 110.0, 11.232739865837237)]
    public void AFloatingWindowClosedBeforeTodayIsTheEuropeanAtItsExtremum(OptionType type, double minmax, double quantLibEuropean)
    {
        var option = new ContinuousPartialFloatingLookbackOption(minmax, 1.0, Today - 30, new FloatingTypePayoff(type), AYear);
        Assert.Equal(quantLibEuropean, AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, Context, Process).NPV!.Value, 1e-12);
        OneAssetOptionResults mc = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, 4, null, false, true, 100000, null, null, 42);
        Assert.True(Math.Abs(mc.NPV!.Value - quantLibEuropean) <= 3.0 * mc.ErrorEstimate!.Value, $"{mc.NPV} against {quantLibEuropean}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OnALaterMarketAFixedWindowOpeningBeforeTheClocksOriginIsTheFullLookback(int days)
    {
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var option = new ContinuousPartialFixedLookbackOption(Today + days, payoff, AYear);
        var full = new ContinuousFixedLookbackOption(100.0, payoff, AYear);
        double expected = AnalyticContinuousFixedLookbackEngine.Calculate(full, Context, Later).NPV!.Value;
        Assert.Equal(18.4715, expected, 1e-4);
        Assert.Equal(expected, AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, Context, Later).NPV!.Value);
        Assert.Equal(
            MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(full, Context, Later, 4, null, false, true, 1000, null, null, 42).NPV,
            MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(option, Context, Later, 4, null, false, true, 1000, null, null, 42).NPV);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OnALaterMarketAFloatingWindowClosingBeforeTheClocksOriginIsTheEuropeanWithTheSpot(int days)
    {
        var option = new ContinuousPartialFloatingLookbackOption(105.0, 1.0, Today + days, new FloatingTypePayoff(OptionType.Call), AYear);
        var european = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        double expected = AnalyticEuropeanEngine.Calculate(european, Context, Later).NPV!.Value;
        Assert.Equal(9.7949, expected, 1e-4);
        Assert.Equal(expected, AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, Context, Later).NPV!.Value);
        OneAssetOptionResults mc = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Later, 4, null, false, true, 100000, null, null, 42);
        Assert.True(Math.Abs(mc.NPV!.Value - expected) <= 3.0 * mc.ErrorEstimate!.Value, $"{mc.NPV} against {expected}");
    }

    [Fact]
    public void OnALaterMarketAWindowDatedBeforeTheEvaluationDateIsPast()
    {
        var fixedOption = new ContinuousPartialFixedLookbackOption(Today - 1, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        Assert.Equal(
            Opened,
            Assert.Throws<NanoQLException>(() => AnalyticContinuousPartialFixedLookbackEngine.Calculate(fixedOption, Context, Later)).Message);
        Assert.Equal(
            Opened,
            Assert.Throws<NanoQLException>(() => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
                fixedOption, Context, Later, 4, null, false, true, 1000, null, null, 42)).Message);

        var floatingOption = new ContinuousPartialFloatingLookbackOption(105.0, 1.0, Today - 1, new FloatingTypePayoff(OptionType.Call), AYear);
        double european = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 105.0), AYear), Context, Later).NPV!.Value;
        Assert.Equal(european, AnalyticContinuousPartialFloatingLookbackEngine.Calculate(floatingOption, Context, Later).NPV!.Value);
        OneAssetOptionResults mc = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            floatingOption, Context, Later, 4, null, false, true, 100000, null, null, 42);
        Assert.True(Math.Abs(mc.NPV!.Value - european) <= 3.0 * mc.ErrorEstimate!.Value, $"{mc.NPV} against {european}");
    }
}
