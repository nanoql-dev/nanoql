// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/twoassetcorrelationoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TwoAssetCorrelationOptionTests
{
    [Fact]
    public void TestAnalyticEngine()
    {
        // Testing analytic engine for two-asset correlation option...

        // the test runner's evaluation date (quantlibglobalfixture.cpp)
        var today = new Date(16, Month.September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();

        OptionType type = OptionType.Call;
        Real strike1 = 50.0;
        Real strike2 = 70.0;
        Date exDate = today + 180;

        var exercise = new EuropeanExercise(exDate);

        var option = new TwoAssetCorrelationOption(type, strike1, strike2, exercise);

        YieldTermStructure dividendTS1 = new FlatForward(today, 0.0, dc);
        YieldTermStructure dividendTS2 = new FlatForward(today, 0.0, dc);
        YieldTermStructure riskFreeTS = new FlatForward(today, 0.1, dc);
        BlackVolTermStructure blackVolTS1 = new BlackConstantVol(today, new NullCalendar(), 0.2, dc);
        BlackVolTermStructure blackVolTS2 = new BlackConstantVol(today, new NullCalendar(), 0.3, dc);

        Real correlation = 0.75;

        var process1 = new BlackScholesMertonProcess(52.0, dividendTS1, riskFreeTS, blackVolTS1);
        var process2 = new BlackScholesMertonProcess(65.0, dividendTS2, riskFreeTS, blackVolTS2);

        Real calculated = AnalyticTwoAssetCorrelationEngine.Calculate(
            option, context, process1, process2, correlation).NPV!.Value;
        Real expected = 4.7073;
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1e-4;
        Assert.True(
            error <= tolerance,
            $"Failed to reproduce holder-extensible option value\n    expected:   {expected}\n"
            + $"    calculated: {calculated}\n    error:      {error}");
    }
}
