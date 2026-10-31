// SPDX-FileCopyrightText: Copyright (C) 2011 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/extensibleoptions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ExtensibleOptionsTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, Month.September, 2015);

    [Fact]
    public void TestAnalyticHolderExtensibleOptionEngine()
    {
        // Testing analytic engine for holder-extensible option...
        OptionType type = OptionType.Call;
        Real strike1 = 100.0;
        Real strike2 = 105.0;
        DayCounter dc = new Actual360();
        Date today = RunnerDate;
        var context = new EvaluationContext { EvaluationDate = today };
        Date exDate1 = today + 180;
        Date exDate2 = today + 270;
        Real premium = 1.0;

        Real spot = 100.0;
        Rate qRate = 0.0;
        Rate rRate = 0.08;
        Volatility vol = 0.25;

        var payoff = new PlainVanillaPayoff(type, strike1);
        var exercise = new EuropeanExercise(exDate1);

        var option = new HolderExtensibleOption(type, premium, exDate2, strike2, payoff, exercise);

        YieldTermStructure dividendTS = new FlatForward(today, qRate, dc);
        YieldTermStructure riskFreeTS = new FlatForward(today, rRate, dc);
        BlackVolTermStructure blackVolTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var process = new BlackScholesMertonProcess(spot, dividendTS, riskFreeTS, blackVolTS);

        Real calculated = AnalyticHolderExtensibleOptionEngine.Calculate(option, context, process).NPV!.Value;
        Real expected = 9.4233;
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1e-4;
        if (error > tolerance)
        {
            Assert.Fail(
                $"Failed to reproduce holder-extensible option value\n    expected:   {expected}\n    calculated: {calculated}"
                + $"\n    error:      {error}");
        }
    }

    [Fact]
    public void TestAnalyticWriterExtensibleOptionEngine()
    {
        // Testing analytic engine for writer-extensible option...

        // What we need for the option (tests):
        OptionType type = OptionType.Call;
        Real strike1 = 90.0;
        Real strike2 = 82.0;
        DayCounter dc = new Actual360();
        Date today = RunnerDate;
        var context = new EvaluationContext { EvaluationDate = today };
        Date exDate1 = today + 180;
        Date exDate2 = today + 270;

        Real spot = 80.0;
        Rate qRate = 0.0;
        YieldTermStructure dividendTS = new FlatForward(today, qRate, dc);
        Rate rRate = 0.10;
        YieldTermStructure riskFreeTS = new FlatForward(today, rRate, dc);
        Volatility vol = 0.30;
        BlackVolTermStructure blackVolTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        // B&S process (needed for the engine):
        var process = new GeneralizedBlackScholesProcess(spot, dividendTS, riskFreeTS, blackVolTS);

        // Create the arguments:
        var payoff1 = new PlainVanillaPayoff(type, strike1);
        var exercise1 = new EuropeanExercise(exDate1);
        var payoff2 = new PlainVanillaPayoff(type, strike2);
        var exercise2 = new EuropeanExercise(exDate2);

        // Create the option by calling the constructor:
        var option = new WriterExtensibleOption(payoff1, exercise1, payoff2, exercise2);

        // Compare the calculated NPV value to the theoretical value:
        Real calculated = AnalyticWriterExtensibleOptionEngine.Calculate(option, context, process).NPV!.Value;
        Real expected = 6.8238;
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1e-4;
        if (error > tolerance)
        {
            Assert.Fail(
                $"Failed to reproduce writer-extensible option value\n    expected:   {expected}\n    calculated: {calculated}"
                + $"\n    error:      {error}");
        }
    }
}
