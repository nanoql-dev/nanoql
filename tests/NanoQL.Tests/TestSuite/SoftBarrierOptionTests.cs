// SPDX-FileCopyrightText: Copyright (C) 2025 William Day
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/softbarrieroption.cpp @ v1.44
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
public class SoftBarrierOptionTests
{
    [Fact]
    public void TestSoftBarrierHaug()
    {
        // Testing soft barrier option pricing against textbook values...

        SoftBarrierOptionData[] values =
        [
            /* The data below is from "The complete guide to option pricing formulas 2nd Ed",E.G. Haug p.166
              Note: In the book, b represents the cost of carry (r-q)
            */

            // barrierType,       optionType,   S,   X,  U,  L,    q,   r,   T,  v, result, tol
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 95, 0.05, 0.1, 0.5, 0.1, 3.8075, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 90, 0.05, 0.1, 0.5, 0.1, 4.0175, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 85, 0.05, 0.1, 0.5, 0.1, 4.0529, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 80, 0.05, 0.1, 0.5, 0.1, 4.0648, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 75, 0.05, 0.1, 0.5, 0.1, 4.0708, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 70, 0.05, 0.1, 0.5, 0.1, 4.0744, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 65, 0.05, 0.1, 0.5, 0.1, 4.0768, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 60, 0.05, 0.1, 0.5, 0.1, 4.0785, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 55, 0.05, 0.1, 0.5, 0.1, 4.0798, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 50, 0.05, 0.1, 0.5, 0.1, 4.0808, 1e-4),

            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 95, 0.05, 0.1, 0.5, 0.2, 4.5263, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 90, 0.05, 0.1, 0.5, 0.2, 5.5615, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 85, 0.05, 0.1, 0.5, 0.2, 6.0394, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 80, 0.05, 0.1, 0.5, 0.2, 6.2594, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 75, 0.05, 0.1, 0.5, 0.2, 6.3740, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 70, 0.05, 0.1, 0.5, 0.2, 6.4429, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 65, 0.05, 0.1, 0.5, 0.2, 6.4889, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 60, 0.05, 0.1, 0.5, 0.2, 6.5217, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 55, 0.05, 0.1, 0.5, 0.2, 6.5463, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 50, 0.05, 0.1, 0.5, 0.2, 6.5654, 1e-4),

            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 95, 0.05, 0.1, 0.5, 0.3, 4.7297, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 90, 0.05, 0.1, 0.5, 0.3, 6.2595, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 85, 0.05, 0.1, 0.5, 0.3, 7.2496, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 80, 0.05, 0.1, 0.5, 0.3, 7.8567, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 75, 0.05, 0.1, 0.5, 0.3, 8.2253, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 70, 0.05, 0.1, 0.5, 0.3, 8.4578, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 65, 0.05, 0.1, 0.5, 0.3, 8.6142, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 60, 0.05, 0.1, 0.5, 0.3, 8.7260, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 55, 0.05, 0.1, 0.5, 0.3, 8.8099, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 50, 0.05, 0.1, 0.5, 0.3, 8.8751, 1e-4),

            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 95, 0.05, 0.1, 1.0, 0.1, 5.4187, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 90, 0.05, 0.1, 1.0, 0.1, 6.0758, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 85, 0.05, 0.1, 1.0, 0.1, 6.2641, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 80, 0.05, 0.1, 1.0, 0.1, 6.3336, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 75, 0.05, 0.1, 1.0, 0.1, 6.3685, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 70, 0.05, 0.1, 1.0, 0.1, 6.3894, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 65, 0.05, 0.1, 1.0, 0.1, 6.4034, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 60, 0.05, 0.1, 1.0, 0.1, 6.4133, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 55, 0.05, 0.1, 1.0, 0.1, 6.4208, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 50, 0.05, 0.1, 1.0, 0.1, 6.4266, 1e-4),

            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 95, 0.05, 0.1, 1.0, 0.2, 5.3614, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 90, 0.05, 0.1, 1.0, 0.2, 6.9776, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 85, 0.05, 0.1, 1.0, 0.2, 7.9662, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 80, 0.05, 0.1, 1.0, 0.2, 8.5432, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 75, 0.05, 0.1, 1.0, 0.2, 8.8822, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 70, 0.05, 0.1, 1.0, 0.2, 9.0931, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 65, 0.05, 0.1, 1.0, 0.2, 9.2343, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 60, 0.05, 0.1, 1.0, 0.2, 9.3353, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 55, 0.05, 0.1, 1.0, 0.2, 9.4110, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 50, 0.05, 0.1, 1.0, 0.2, 9.4698, 1e-4),

            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 95, 0.05, 0.1, 1.0, 0.3, 5.2300, 1e-4),
            // new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 90, 0.05, 0.1, 1.0, 0.3, 7.2046, 1e-4), // this test case has an error of c0.0003 -> not sure why, but most likely due to tight barriers
            // and high volatility leading to small numerical differences
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 85, 0.05, 0.1, 1.0, 0.3, 8.7092, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 80, 0.05, 0.1, 1.0, 0.3, 9.8118, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 75, 0.05, 0.1, 1.0, 0.3, 10.5964, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 70, 0.05, 0.1, 1.0, 0.3, 11.1476, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 65, 0.05, 0.1, 1.0, 0.3, 11.5384, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 60, 0.05, 0.1, 1.0, 0.3, 11.8228, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 55, 0.05, 0.1, 1.0, 0.3, 12.0369, 1e-4),
            new(BarrierType.DownOut, OptionType.Call, 100, 100, 95, 50, 0.05, 0.1, 1.0, 0.3, 12.2036, 1e-4),
        ];

        DayCounter dc = new Actual360();
        var today = new Date(8, August, 2025);
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (SoftBarrierOptionData value in values)
        {
            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            var process = new GeneralizedBlackScholesProcess(
                value.S, FlatRate(today, value.Q, dc), FlatRate(today, value.R, dc), FlatVol(today, value.V, dc));

            var option = new SoftBarrierOption(value.BarrierType, value.L, value.U, payoff, exercise);

            // Value check
            double calculated = AnalyticSoftBarrierEngine.Calculate(option, context, process).NPV!.Value;
            double error = Math.Abs(calculated - value.Result);
            Assert.True(
                error <= value.Tol,
                $"{value.BarrierType} European {value.Type} option: underlying value {value.S}, strike {value.Strike}, "
                + $"upper barrier {value.U}, lower barrier {value.L}, dividend yield {value.Q}, risk-free rate {value.R}, "
                + $"maturity {value.T}, volatility {value.V}: expected Soft barrier option value {value.Result}, "
                + $"calculated {calculated}, error {error}, tolerance {value.Tol}");
        }
    }

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private sealed record SoftBarrierOptionData(
        BarrierType BarrierType, // e.g DownOut/UpIn/etc.
        OptionType Type, // Call/Put
        double S, // Spot price
        double Strike, // X
        double U, // U
        double L, // L
        double Q, // Dividend Yield
        double R, // Risk free rate
        double T, // Time to maturity
        double V, // Volatility
        double Result, // Expected NPV
        double Tol); // Tolerance
}
