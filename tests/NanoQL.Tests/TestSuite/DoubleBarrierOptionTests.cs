// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/doublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testEuropeanHaugValues' Suo-Wang and binomial checks, which wait for S-xeq's SuoWangDoubleBarrierEngine and BinomialDoubleBarrierEngine; testVannaVolgaDoubleBarrierValues, which waits for S-xeq's VannaVolgaDoubleBarrierEngine; testMonteCarloDoubleBarrierWithAnalytical, which waits for S-xeq's MCDoubleBarrierEngine

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's options hold their engines and its quotes notify them; here each pricing passes the engine's arguments,
// and a quote set anew is a process built at the new value.
[Trait("Oracle", "QuantLib")]
public class DoubleBarrierOptionTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestEuropeanHaugValues()
    {
        // Testing double barrier european options against Haug's values...

        NewBarrierOptionData[] values =
        [
            /* The data below are from
              "The complete guide to option pricing formulas 2nd Ed",E.G. Haug, McGraw-Hill, p.156 and following.
              Note:
              The book uses b instead of q (q=r-b)
            */
            // BarrierType, barr.lo, barr.hi, type, strk, s, q, r, t, v, result, tol
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 4.3515, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 6.1644, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 7.0373, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 6.9853, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 7.9336, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 6.5088, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 4.3505, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 5.8500, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 5.7726, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 6.8082, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 6.3383, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 4.3841, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 4.3139, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 4.8293, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 3.7765, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 5.9697, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 4.0004, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 2.2563, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 3.7516, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 2.6387, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 1.4903, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 3.5805, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 1.5098, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 0.5635, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 1.2055, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 0.3098, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 0.0477, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 0.5537, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 0.0441, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 0.0011, 1.0e-4),

            // BarrierType, barr.lo, barr.hi, type, strk, s, q, r, t, v, result, tol
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 1.8825, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 3.7855, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 5.7191, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 2.1374, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 4.7033, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 50.0, 150.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 7.1683, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 1.8825, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 3.7845, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 5.6060, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 2.1374, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 4.6236, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 60.0, 140.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 6.1062, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 1.8825, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 3.7014, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 4.6472, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 2.1325, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 3.8944, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 70.0, 130.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 3.5868, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 1.8600, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 2.6866, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 2.0719, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 1.8883, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 1.7851, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 80.0, 120.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 0.8244, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 0.9473, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 0.3449, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 0.0578, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 0.4555, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 0.0491, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 90.0, 110.0, OptionType.Put, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 0.0013, 1.0e-4),

            // BarrierType, barr.lo, barr.hi, type, strk, s, q, r, t, v, result, tol
            new(DoubleBarrierType.KnockIn, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 0.0000, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 0.0900, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 1.1537, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 0.0292, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 1.6487, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 50.0, 150.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 5.7321, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 0.0010, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 0.4045, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 2.4184, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 0.2062, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 3.2439, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 60.0, 140.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 7.8569, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 0.0376, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 1.4252, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 4.4145, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 1.0447, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 5.5818, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 70.0, 130.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 9.9846, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 0.5999, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 3.6158, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 6.7007, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 3.4340, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 8.0724, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 80.0, 120.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 11.6774, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.15, 3.1460, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.25, 5.9447, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.25, 0.35, 8.1432, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.15, 6.4608, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.25, 9.5382, 1.0e-4),
            new(DoubleBarrierType.KnockIn, 90.0, 110.0, OptionType.Call, 100, 100.0, 0.0, 0.1, 0.50, 0.35, 12.2398, 1.0e-4),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (NewBarrierOptionData value in values)
        {
            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);

            YieldTermStructure qTS = FlatRate(today, value.Q, dc);
            YieldTermStructure rTS = FlatRate(today, value.R, dc);
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            var stochProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, FlatVol(today, value.V, dc));

            var opt = new DoubleBarrierOption(
                value.BarrierType,
                value.BarrierLo,
                value.BarrierHi,
                0, // no rebate
                payoff,
                exercise);

            // Ikeda/Kunitomo engine
            double calculated = AnalyticDoubleBarrierEngine.Calculate(opt, RunnerContext, stochProcess).NPV!.Value;
            double expected = value.Result;
            double error = Math.Abs(calculated - expected);
            Assert.False(error > value.Tol, Failure("Ikeda/Kunitomo value", value, expected, calculated, error, value.Tol));

            // the Suo-Wang and binomial engines' checks wait for S-xeq's engines
            if (value.BarrierType == DoubleBarrierType.KnockOut)
            {
                var model = new HestonModel(
                    new HestonProcess(rTS, qTS, value.S, value.V * value.V, 1.0, value.V * value.V, 0.001, 0.0));
                calculated = FdHestonDoubleBarrierEngine.Calculate(opt, RunnerContext, model, 251, 76, 3).NPV!.Value;
                expected = value.Result;
                error = Math.Abs(calculated - expected);
                double tol = 0.025; // error one order of magnitude lower than plain binomial
                Assert.False(error > tol, Failure("Heston value", value, expected, calculated, error, tol));
            }
        }
    }

    private static string Failure(
        string greekName, NewBarrierOptionData value, double expected, double calculated, double error, double tolerance) =>
        $"{DoubleBarrierTypeFunctions.ToString(value.BarrierType)} European {value.Type} option: underlying value "
        + $"{value.S}, strike {value.Strike}, barrier low {value.BarrierLo}, barrier high {value.BarrierHi}, dividend "
        + $"yield {value.Q}, risk-free rate {value.R}, volatility {value.V}: expected {greekName} {expected}, "
        + $"calculated {greekName} {calculated}, error {error}, tolerance {tolerance}";

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private sealed record NewBarrierOptionData(
        DoubleBarrierType BarrierType,
        double BarrierLo,
        double BarrierHi,
        OptionType Type,
        double Strike,
        double S, // spot
        double Q, // dividend
        double R, // risk-free rate
        double T, // time to maturity
        double V, // volatility
        double Result, // result
        double Tol); // tolerance
}
