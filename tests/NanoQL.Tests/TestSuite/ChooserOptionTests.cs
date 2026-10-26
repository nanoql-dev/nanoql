// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/chooseroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testAnalyticComplexChooserEngine line 138 (Divergences/ComplexChooserEqualTermsTests.cs)

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ChooserOptionTests
{
    [Fact]
    public void TestAnalyticSimpleChooserEngine()
    {
        // Testing analytic simple chooser option...

        /* The data below are from
           "Complete Guide to Option Pricing Formulas", Espen Gaarder Haug
           pages 39-40
        */
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 50.0;
        Rate qRate = 0.0;
        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        Rate rRate = 0.08;
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);
        Volatility vol = 0.25;
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        Real strike = 50.0;

        Date exerciseDate = today + 180;
        var exercise = new EuropeanExercise(exerciseDate);

        Date choosingDate = today + 90;
        var option = new SimpleChooserOption(choosingDate, strike, exercise);

        Real calculated = AnalyticSimpleChooserEngine.Calculate(option, context, stochProcess).NPV!.Value;
        Real expected = 6.1071;
        Real tolerance = 3e-5;
        if (Math.Abs(calculated - expected) > tolerance)
        {
            ReportFailure("value", exercise, spot, qRate, rRate, today, vol, expected, calculated, tolerance);
        }
    }

    [Fact]
    public void TestAnalyticComplexChooserEngine()
    {
        // Testing analytic complex chooser option...

        /* The example below is from
           "Complete Guide to Option Pricing Formulas", Espen Gaarder Haug
        */
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 50.0;
        Rate qRate = 0.05;
        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        Rate rRate = 0.10;
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);
        Volatility vol = 0.35;
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        Real callStrike = 55.0;
        Real putStrike = 48.0;

        // Moved: QuantLib's master after v1.44 measures the maturities from the evaluation date (#2891), where 1.44's
        // case places them 180 and 210 days after the choosing date, which its maturities measured from that date fit
        Date choosingDate = today + 90;
        Date callExerciseDate = today + 180;
        Date putExerciseDate = today + 210;
        var callExercise = new EuropeanExercise(callExerciseDate);
        var putExercise = new EuropeanExercise(putExerciseDate);

        var option = new ComplexChooserOption(choosingDate, callStrike, putStrike, callExercise, putExercise);

        Real calculated = AnalyticComplexChooserEngine.Calculate(option, context, stochProcess).NPV!.Value;
        Real expected = 6.0508;
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1e-4;
        if (error > tolerance)
        {
            Assert.Fail(
                $"Failed to reproduce complex chooser option value\n    expected:   {expected}\n    calculated: {calculated}"
                + $"\n    error:      {error}");
        }
    }

    private static void ReportFailure(
        string greekName,
        Exercise exercise,
        Real s,
        Rate q,
        Rate r,
        Date today,
        Volatility v,
        Real expected,
        Real calculated,
        Real tolerance) =>
        Assert.Fail(
            $"{exercise.Type} Chooser option with     spot value: {s}\n    dividend yield:   {q}\n"
            + $"    risk-free rate:   {r}\n    reference date:   {today}\n    maturity:         {exercise.LastDate()}\n"
            + $"    volatility:       {v}\n\n    expected   {greekName}: {expected}\n"
            + $"    calculated {greekName}: {calculated}\n    error:            {Math.Abs(expected - calculated)}\n"
            + $"    tolerance:        {tolerance}");
}
