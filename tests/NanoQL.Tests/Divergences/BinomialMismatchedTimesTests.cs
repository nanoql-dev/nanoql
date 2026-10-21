// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The binomial engine on structures that measure time differently. QuantLib reads the risk-free rate, the dividend
/// yield and the volatility at the maturity date, each on its own day counter, flattens each on that day counter, and
/// runs the tree over the risk-free curve's time to maturity (<c>binomialengine.hpp:84-105</c>), so a dividend curve or
/// a volatility on another day counter or reference date accrues over the wrong time: a one-year put on a dividend
/// curve and a volatility on Act/360 beside a risk-free curve on Act/365 reads 8.6306060 on 801 Cox-Ross-Rubinstein
/// steps, against QuantLib's analytic engine's 8.7101995, which reads every structure by date. nanoql runs the tree on
/// the risk-free curve's day counter from the earliest reference date of the structures, as the finite-difference
/// engine's grid, and flattens such a structure on it, its rate or volatility the one with its discount or variance at
/// the maturity date: QuantLib's own engine on the market restated so, each structure accruing nothing before its own
/// date. A market whose structures share the risk-free curve's day counter and reference date is QuantLib's.
/// </summary>
public class BinomialMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();

    [Fact]
    public void StructuresOnAnotherDayCounterAreReadByDate()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date maturity = Today + new Period(1, TimeUnit.Years);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(maturity));
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.03, A360), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, null, 0.25, A360));

        // QuantLib's own engine on the structures restated on Act/365, with the discount 0.9700412654712278 and the
        // variance 0.06336805555555555 at the maturity date, reads 8.713151996785781; QuantLib's engine on the
        // structures as given, 8.63060600579229
        var restated = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, Math.Log(1.0 / 0.9700412654712278), A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, null, Math.Sqrt(0.06336805555555555), A365));
        Assert.Equal(8.713151996785781, BinomialCRRVanillaEngine.Calculate(put, context, process, 801).NPV!.Value, 1e-10);
        Assert.Equal(
            BinomialCRRVanillaEngine.Calculate(put, context, restated, 801).NPV!.Value,
            BinomialCRRVanillaEngine.Calculate(put, context, process, 801).NPV!.Value,
            1e-12);

        // the analytic engine, 8.71019948797978 on both, within the tree's error
        Assert.Equal(8.71019948797978, AnalyticEuropeanEngine.Calculate(put, context, process).NPV!.Value, 1e-12);
    }

    [Fact]
    public void StructuresOnAnotherReferenceDateAreReadByDate()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date maturity = Today + new Period(1, TimeUnit.Years);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(maturity));
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, context);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.03, A365), settling, new BlackConstantVol(Today, null, 0.25, A365));

        // the risk-free curve settles on 19 October; QuantLib's engine on the structures as given reads
        // 8.592133125498668, its analytic engine 8.654700681780128, and its binomial engine on the market restated from
        // today, the risk-free curve discounting nothing before 19 October, 8.657634745834144: the tree runs from today
        Assert.Equal(Today + 4, settling.ReferenceDate);
        Assert.Equal(8.657634745834144, BinomialCRRVanillaEngine.Calculate(put, context, process, 801).NPV!.Value, 1e-12);
        Assert.Equal(8.654700681780128, AnalyticEuropeanEngine.Calculate(put, context, process).NPV!.Value, 1e-12);
    }

    [Fact]
    public void StructuresDatedAfterTheRiskFreeCurveAccrueNothingBeforeTheirDate()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date later = Today + 4;
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(later, 0.03, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(later, null, 0.25, A365));
        var european = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));
        var american = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today, Today + 365));

        // the dividend curve and the volatility dated four days after the risk-free curve: QuantLib's engine on the
        // market restated from today, both accruing nothing before their date, 8.565155172922998 and 8.822536415521197;
        // on the market as given, 8.63060600579229 and 8.885697455913343; its analytic engine 8.562239528561069, within
        // the tree's error
        Assert.Equal(8.565155172922998, BinomialCRRVanillaEngine.Calculate(european, context, process, 801).NPV!.Value, 1e-12);
        Assert.Equal(8.822536415521197, BinomialCRRVanillaEngine.Calculate(american, context, process, 801).NPV!.Value, 1e-12);
        Assert.Equal(8.562239528561069, AnalyticEuropeanEngine.Calculate(european, context, process).NPV!.Value, 1e-12);
    }

    [Fact]
    public void StructuresSharingTheRiskFreeTimesAreQuantLibs()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date maturity = Today + new Period(1, TimeUnit.Years);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(maturity));
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, null, 0.2, A365));

        // QuantLib's Leisen-Reimer engine at 251 steps reads 6.330074676109362
        Assert.Equal(6.330074676109362, BinomialLRVanillaEngine.Calculate(put, context, process, 251).NPV!.Value, 1e-12);
    }
}
