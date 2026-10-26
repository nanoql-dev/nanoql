// SPDX-FileCopyrightText: Copyright (C) 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/twoassetbarrieroption.cpp @ v1.44
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
public class TwoAssetBarrierOptionTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestHaugValues()
    {
        // Testing two-asset barrier options against Haug's values...

        OptionData[] values =
        [
            /* The data below are from
              "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
            */
            new(BarrierType.DownOut, OptionType.Call, 95, 90, 100.0, 0.0, 0.2, 100.0, 0.0, 0.2, 0.5, 0.08, 6.6592),
            new(BarrierType.UpOut, OptionType.Call, 105, 90, 100.0, 0.0, 0.2, 100.0, 0.0, 0.2, -0.5, 0.08, 4.6670),
            new(BarrierType.DownOut, OptionType.Put, 95, 90, 100.0, 0.0, 0.2, 100.0, 0.0, 0.2, -0.5, 0.08, 0.6184),
            new(BarrierType.UpOut, OptionType.Put, 105, 100, 100.0, 0.0, 0.2, 100.0, 0.0, 0.2, 0.0, 0.08, 0.8246),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        Date maturity = today + 180;
        Exercise exercise = new EuropeanExercise(maturity);

        foreach (OptionData value in values)
        {
            YieldTermStructure rTS = FlatRate(today, value.R, dc);
            var process1 = new BlackScholesMertonProcess(
                value.S1, FlatRate(today, value.Q1, dc), rTS, FlatVol(today, value.V1, dc));
            var process2 = new BlackScholesMertonProcess(
                value.S2, FlatRate(today, value.Q2, dc), rTS, FlatVol(today, value.V2, dc));

            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            var barrierOption = new TwoAssetBarrierOption(value.BarrierType, value.Barrier, payoff, exercise);

            double calculated = AnalyticTwoAssetBarrierEngine.Calculate(
                barrierOption, RunnerContext, process1, process2, value.Correlation).NPV!.Value;
            double expected = value.Result;
            double error = Math.Abs(calculated - expected);
            double tolerance = 4.0e-3;
            Assert.True(
                error <= tolerance,
                $"failed to reproduce expected price: expected {expected}, calculated {calculated}, tolerance "
                + $"{tolerance}, error {error}");
        }
    }

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    private sealed record OptionData(
        BarrierType BarrierType,
        OptionType Type,
        double Barrier,
        double Strike,
        double S1, // spot
        double Q1, // dividend
        double V1, // volatility
        double S2,
        double Q2,
        double V2,
        double Correlation,
        double R, // risk-free rate
        double Result); // result
}
