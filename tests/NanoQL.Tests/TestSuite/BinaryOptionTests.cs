// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/binaryoption.cpp @ v1.44
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
public class BinaryOptionTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestCashOrNothingHaugValues()
    {
        // Testing cash-or-nothing barrier options against Haug's values...

        BinaryOptionData[] values =
        [
            /* The data below are from
              "Option pricing formulas 2nd Ed.", E.G. Haug, McGraw-Hill 2007 pag. 180 - cases 13,14,17,18,21,22,25,26
              Note:
                q is the dividend rate, while the book gives b, the cost of carry (q=r-b)
            */
            //    barrierType, barrier,  cash,         type, strike,   spot,    q,    r,   t,  vol,   value, tol
            new(BarrierType.DownIn, 100.00, 15.00, OptionType.Call, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 4.9289, 1e-4),
            new(BarrierType.DownIn, 100.00, 15.00, OptionType.Call, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 6.2150, 1e-4),

            // following value is wrong in book.
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Call, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 5.8926, 1e-4),
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Call, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 7.4519, 1e-4),

            // 17,18
            new(BarrierType.DownIn, 100.00, 15.00, OptionType.Put, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 4.4314, 1e-4),
            new(BarrierType.DownIn, 100.00, 15.00, OptionType.Put, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 3.1454, 1e-4),
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Put, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 5.3297, 1e-4),
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Put, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 3.7704, 1e-4),

            // 21,22
            new(BarrierType.DownOut, 100.00, 15.00, OptionType.Call, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 4.8758, 1e-4),
            new(BarrierType.DownOut, 100.00, 15.00, OptionType.Call, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 4.9081, 1e-4),
            new(BarrierType.UpOut, 100.00, 15.00, OptionType.Call, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
            new(BarrierType.UpOut, 100.00, 15.00, OptionType.Call, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 0.0407, 1e-4),

            // 25,26
            new(BarrierType.DownOut, 100.00, 15.00, OptionType.Put, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 0.0323, 1e-4),
            new(BarrierType.DownOut, 100.00, 15.00, OptionType.Put, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
            new(BarrierType.UpOut, 100.00, 15.00, OptionType.Put, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 3.0461, 1e-4),
            new(BarrierType.UpOut, 100.00, 15.00, OptionType.Put, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 3.0054, 1e-4),

            // other values calculated with book vba
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Call, 102.00, 95.00, -0.14, 0.10, 0.5, 0.20, 8.6806, 1e-4),
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Call, 102.00, 95.00, 0.03, 0.10, 0.5, 0.20, 5.3112, 1e-4),

            // degenerate conditions (barrier touched)
            new(BarrierType.DownIn, 100.00, 15.00, OptionType.Call, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 7.4926, 1e-4),
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Call, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 11.1231, 1e-4),

            // 17,18
            new(BarrierType.DownIn, 100.00, 15.00, OptionType.Put, 102.00, 98.00, 0.00, 0.10, 0.5, 0.20, 7.1344, 1e-4),
            new(BarrierType.UpIn, 100.00, 15.00, OptionType.Put, 102.00, 101.00, 0.00, 0.10, 0.5, 0.20, 5.9299, 1e-4),

            // 21,22
            new(BarrierType.DownOut, 100.00, 15.00, OptionType.Call, 98.00, 99.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
            new(BarrierType.UpOut, 100.00, 15.00, OptionType.Call, 98.00, 101.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),

            // 25,26
            new(BarrierType.DownOut, 100.00, 15.00, OptionType.Put, 98.00, 99.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
            new(BarrierType.UpOut, 100.00, 15.00, OptionType.Put, 98.00, 101.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
        ];

        Check(values, value => new CashOrNothingPayoff(value.Type, value.Strike, value.Cash));
    }

    [Fact]
    public void TestAssetOrNothingHaugValues()
    {
        // Testing asset-or-nothing barrier options against Haug's values...

        BinaryOptionData[] values =
        [
            /* The data below are from
              "Option pricing formulas 2nd Ed.", E.G. Haug, McGraw-Hill 2007 pag. 180 - cases 15,16,19,20,23,24,27,28
              Note:
                q is the dividend rate, while the book gives b, the cost of carry (q=r-b)
            */
            //    barrierType, barrier,  cash,         type, strike,   spot,    q,    r,   t,  vol,   value, tol
            new(BarrierType.DownIn, 100.00, 0.00, OptionType.Call, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 37.2782, 1e-4),
            new(BarrierType.DownIn, 100.00, 0.00, OptionType.Call, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 45.8530, 1e-4),
            new(BarrierType.UpIn, 100.00, 0.00, OptionType.Call, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 44.5294, 1e-4),
            new(BarrierType.UpIn, 100.00, 0.00, OptionType.Call, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 54.9262, 1e-4),

            // 19,20
            new(BarrierType.DownIn, 100.00, 0.00, OptionType.Put, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 27.5644, 1e-4),
            new(BarrierType.DownIn, 100.00, 0.00, OptionType.Put, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 18.9896, 1e-4),

            // following value is wrong in book.
            new(BarrierType.UpIn, 100.00, 0.00, OptionType.Put, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 33.1723, 1e-4),
            new(BarrierType.UpIn, 100.00, 0.00, OptionType.Put, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 22.7755, 1e-4),

            // 23,24
            new(BarrierType.DownOut, 100.00, 0.00, OptionType.Call, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 39.9391, 1e-4),
            new(BarrierType.DownOut, 100.00, 0.00, OptionType.Call, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 40.1574, 1e-4),
            new(BarrierType.UpOut, 100.00, 0.00, OptionType.Call, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
            new(BarrierType.UpOut, 100.00, 0.00, OptionType.Call, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 0.2676, 1e-4),

            // 27,28
            new(BarrierType.DownOut, 100.00, 0.00, OptionType.Put, 102.00, 105.00, 0.00, 0.10, 0.5, 0.20, 0.2183, 1e-4),
            new(BarrierType.DownOut, 100.00, 0.00, OptionType.Put, 98.00, 105.00, 0.00, 0.10, 0.5, 0.20, 0.0000, 1e-4),
            new(BarrierType.UpOut, 100.00, 0.00, OptionType.Put, 102.00, 95.00, 0.00, 0.10, 0.5, 0.20, 17.2983, 1e-4),
            new(BarrierType.UpOut, 100.00, 0.00, OptionType.Put, 98.00, 95.00, 0.00, 0.10, 0.5, 0.20, 17.0306, 1e-4),
        ];

        Check(values, value => new AssetOrNothingPayoff(value.Type, value.Strike));
    }

    [Fact]
    public void TestCashOrNothingLowVolatility()
    {
        // Testing cash-or-nothing barrier options at low volatility...

        BinaryOptionData[] values =
        [
            //    barrierType, barrier,  cash,         type, strike,   spot,    q,     r,   t,   vol,              value, tol
            new(BarrierType.DownOut, 90.00, 10.00, OptionType.Call, 95.00, 100.00, 0.03, -0.02, 1.0, 0.003, 6.797152649161316, 1e-8),
            new(BarrierType.UpOut, 110.00, 10.00, OptionType.Put, 105.00, 100.00, 0.00, 0.03, 1.0, 0.001, 9.704455335485082, 1e-8),
            new(BarrierType.DownOut, 90.00, 10.00, OptionType.Call, 100.00, 100.00, 0.00, -0.02, 1.0, 0.001, 0.0, 1e-8),
        ];

        Check(values, value => new CashOrNothingPayoff(value.Type, value.Strike, value.Cash));
    }

    // The cases' common loop: an American exercise paying at expiry, a process at the case's quotes.
    private static void Check(BinaryOptionData[] values, Func<BinaryOptionData, StrikedTypePayoff> payoffOf)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (BinaryOptionData value in values)
        {
            StrikedTypePayoff payoff = payoffOf(value);

            Date exDate = today + TimeToDays(value.T);
            Exercise amExercise = new AmericanExercise(today, exDate, true);

            var stochProcess = new BlackScholesMertonProcess(
                value.S, FlatRate(today, value.Q, dc), FlatRate(today, value.R, dc), FlatVol(today, value.V, dc));

            var opt = new BarrierOption(value.BarrierType, value.Barrier, 0, payoff, amExercise);

            double calculated = AnalyticBinaryBarrierEngine.Calculate(opt, RunnerContext, stochProcess).NPV!.Value;
            double error = Math.Abs(calculated - value.Result);
            Assert.True(
                error <= value.Tol,
                $"{payoff.OptionType} option with {BarrierTypeFunctions.ToString(value.BarrierType)} barrier type: "
                + $"barrier {value.Barrier}, spot {value.S}, strike {payoff.Strike}, dividend yield {value.Q}, "
                + $"risk-free rate {value.R}, reference date {today}, maturity {exDate}, volatility {value.V}: "
                + $"expected value {value.Result}, calculated value {calculated}, error {error}, tolerance {value.Tol}");
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

    private sealed record BinaryOptionData(
        BarrierType BarrierType,
        double Barrier,
        double Cash, // cash payoff for cash-or-nothing
        OptionType Type,
        double Strike,
        double S, // spot
        double Q, // dividend
        double R, // risk-free rate
        double T, // time to maturity
        double V, // volatility
        double Result, // expected result
        double Tol); // tolerance
}
