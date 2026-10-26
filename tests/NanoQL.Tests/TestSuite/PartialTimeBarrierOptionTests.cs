// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/partialtimebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's options hold their engines and its quotes notify them; here each pricing passes the engine's arguments,
// and a quote set anew is a process built at the new value.
[Trait("Oracle", "QuantLib")]
public class PartialTimeBarrierOptionTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestAnalyticEngine()
    {
        // Testing analytic engine for partial-time barrier option...

        Date today = RunnerContext.EvaluationDate;

        OptionType type = OptionType.Call;
        DayCounter dc = new Actual360();
        Date maturity = today + 360;
        var exercise = new EuropeanExercise(maturity);
        double barrier = 100.0;
        double rebate = 0.0;

        TestCase[] cases =
        [
            new(95.0, 90.0, 1, 0.0393),
            new(95.0, 110.0, 1, 0.0000),
            new(105.0, 90.0, 1, 9.8751),
            new(105.0, 110.0, 1, 6.2303),

            new(95.0, 90.0, 90, 6.2747),
            new(95.0, 110.0, 90, 3.7352),
            new(105.0, 90.0, 90, 15.6324),
            new(105.0, 110.0, 90, 9.6812),

            new(95.0, 90.0, 180, 10.3345),
            new(95.0, 110.0, 180, 5.8712),
            new(105.0, 90.0, 180, 19.2896),
            new(105.0, 110.0, 180, 11.6055),

            new(95.0, 90.0, 270, 13.4342),
            new(95.0, 110.0, 270, 7.1270),
            new(105.0, 90.0, 270, 22.0753),
            new(105.0, 110.0, 270, 12.7342),

            new(95.0, 90.0, 359, 16.8576),
            new(95.0, 110.0, 359, 7.5763),
            new(105.0, 90.0, 359, 25.1488),
            new(105.0, 110.0, 359, 13.1376),
        ];

        foreach (TestCase i in cases)
        {
            Date coverEventDate = today + i.Days;
            var payoff = new PlainVanillaPayoff(type, i.Strike);
            var option = new PartialTimeBarrierOption(
                BarrierType.DownOut, PartialBarrierRange.EndB1, barrier, rebate, coverEventDate, payoff, exercise);

            var process = new BlackScholesMertonProcess(
                i.Underlying, FlatRate(today, 0.0, dc), FlatRate(today, 0.1, dc), FlatVol(today, 0.25, dc));
            double calculated = AnalyticPartialTimeBarrierOptionEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            double expected = i.Result;
            double error = Math.Abs(calculated - expected);
            double tolerance = 1e-4;
            Assert.True(error <= tolerance, Failure(BarrierType.DownOut, barrier, payoff, i, expected, calculated, error, tolerance));
        }
    }

    [Fact]
    public void TestAnalyticEnginePutOption()
    {
        // Testing analytic engine for partial-time put barrier option...

        Date today = RunnerContext.EvaluationDate;

        OptionType type = OptionType.Put;
        DayCounter dc = new Actual360();
        Date maturity = today + 360;
        var exercise = new EuropeanExercise(maturity);
        double barrier = 100.0;
        double rebate = 0.0;

        TestCase[] cases =
        [
            new(95.0, 90.0, 1, 1.5551),
            new(95.0, 95.0, 1, 2.0589),
            new(90.0, 95.0, 1, 4.4512),
            new(99.0, 90.0, 1, 0.3404),

            new(95.0, 90.0, 90, 2.4181),
            new(95.0, 95.0, 90, 3.2257),
            new(90.0, 95.0, 90, 5.0624),
            new(99.0, 90.0, 90, 1.5992),

            new(95.0, 90.0, 180, 3.0021),
            new(95.0, 95.0, 180, 4.0617),
            new(90.0, 95.0, 180, 5.7960),
            new(99.0, 90.0, 180, 2.1903),

            new(95.0, 90.0, 270, 3.4194),
            new(95.0, 95.0, 270, 4.7362),
            new(90.0, 95.0, 270, 6.4370),
            new(99.0, 90.0, 270, 2.6025),

            new(95.0, 90.0, 359, 3.5965),
            new(95.0, 95.0, 359, 5.1865),
            new(90.0, 95.0, 359, 6.8782),
            new(99.0, 90.0, 359, 2.7759),
        ];

        foreach (TestCase i in cases)
        {
            Date coverEventDate = today + i.Days;
            var payoff = new PlainVanillaPayoff(type, i.Strike);
            var option = new PartialTimeBarrierOption(
                BarrierType.UpOut, PartialBarrierRange.EndB1, barrier, rebate, coverEventDate, payoff, exercise);

            var process = new BlackScholesMertonProcess(
                i.Underlying, FlatRate(today, 0.0, dc), FlatRate(today, 0.1, dc), FlatVol(today, 0.25, dc));
            double calculated = AnalyticPartialTimeBarrierOptionEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            double expected = i.Result;
            double error = Math.Abs(calculated - expected);
            double tolerance = 1e-4;
            Assert.True(error <= tolerance, Failure(BarrierType.UpOut, barrier, payoff, i, expected, calculated, error, tolerance));
        }
    }

    [Fact]
    public void TestPutCallSymmetry()
    {
        // Testing put-call symmetry for the partial-time barrier option...

        Date today = RunnerContext.EvaluationDate;

        PutCallSymmetryTestCase[] cases =
        [
            new(105.2631, 95.2380, BarrierType.DownOut, 95.0, 105.0, 1, BarrierType.UpOut),
            new(105.2631, 95.2380, BarrierType.DownOut, 95.0, 105.0, 90, BarrierType.UpOut),
            new(105.2631, 95.2380, BarrierType.DownOut, 95.0, 105.0, 180, BarrierType.UpOut),
            new(105.2631, 95.2380, BarrierType.DownOut, 95.0, 105.0, 270, BarrierType.UpOut),
            new(105.2631, 95.2380, BarrierType.DownOut, 95.0, 105.0, 359, BarrierType.UpOut),

            new(110.0, 120.0, BarrierType.UpOut, 90.9090, 83.3333, 1, BarrierType.DownOut),
            new(110.0, 120.0, BarrierType.UpOut, 90.9090, 83.3333, 90, BarrierType.DownOut),
            new(110.0, 120.0, BarrierType.UpOut, 90.9090, 83.3333, 180, BarrierType.DownOut),
            new(110.0, 120.0, BarrierType.UpOut, 90.9090, 83.3333, 270, BarrierType.DownOut),
            new(110.0, 120.0, BarrierType.UpOut, 90.9090, 83.3333, 359, BarrierType.DownOut),
        ];

        DayCounter dc = new Actual360();
        Date maturity = today + 360;
        var exercise = new EuropeanExercise(maturity);
        double r = 0.01;
        double rebate = 0.0;
        double spotPrice = 100.0;

        YieldTermStructure dividendTSCall = FlatRate(today, 0.0, dc);
        YieldTermStructure riskFreeTSCall = FlatRate(today, r, dc);
        YieldTermStructure dividendTSPut = FlatRate(today, r, dc);
        YieldTermStructure riskFreeTSPut = FlatRate(today, 0.0, dc);
        BlackVolTermStructure blackVolTS = FlatVol(today, 0.25, dc);

        var callProcess = new BlackScholesMertonProcess(spotPrice, dividendTSCall, riskFreeTSCall, blackVolTS);
        var putProcess = new BlackScholesMertonProcess(spotPrice, dividendTSPut, riskFreeTSPut, blackVolTS);

        foreach (PutCallSymmetryTestCase i in cases)
        {
            Date coverEventDate = today + i.Days;
            var putPayoff = new PlainVanillaPayoff(OptionType.Put, i.PutStrike);
            var callPayoff = new PlainVanillaPayoff(OptionType.Call, i.CallStrike);
            var putOption = new PartialTimeBarrierOption(
                i.PutType, PartialBarrierRange.EndB1, i.PutBarrier, rebate, coverEventDate, putPayoff, exercise);
            var callOption = new PartialTimeBarrierOption(
                i.CallType, PartialBarrierRange.EndB1, i.CallBarrier, rebate, coverEventDate, callPayoff, exercise);

            double putValue = AnalyticPartialTimeBarrierOptionEngine.Calculate(putOption, RunnerContext, putProcess).NPV!.Value;
            double callValue = AnalyticPartialTimeBarrierOptionEngine.Calculate(callOption, RunnerContext, callProcess).NPV!.Value;
            double callAmount = i.PutStrike / spotPrice;
            double error = Math.Abs(putValue - (callAmount * callValue));
            double tolerance = 1e-4;
            Assert.True(
                error <= tolerance,
                $"Failed to reproduce the put-call symmetry for the partial-time barrier options: error {error}");
        }
    }

    private static string Failure(
        BarrierType barrierType,
        double barrier,
        StrikedTypePayoff payoff,
        TestCase i,
        double expected,
        double calculated,
        double error,
        double tolerance) =>
        $"{BarrierTypeFunctions.ToString(barrierType)} European {payoff.OptionType} option: underlying value "
        + $"{i.Underlying}, strike {payoff.Strike}, barrier {barrier}, cover event in {i.Days} days: expected value "
        + $"{expected}, calculated value {calculated}, error {error}, tolerance {tolerance}";

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    private sealed record TestCase(double Underlying, double Strike, int Days, double Result);

    private sealed record PutCallSymmetryTestCase(
        double CallStrike,
        double CallBarrier,
        BarrierType CallType,
        double PutStrike,
        double PutBarrier,
        int Days,
        BarrierType PutType);
}
