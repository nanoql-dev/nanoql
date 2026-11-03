// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/doublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
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

            // Wulin Suo/Yong Wang engine
            calculated = SuoWangDoubleBarrierEngine.Calculate(opt, RunnerContext, stochProcess).NPV!.Value;
            expected = value.Result;
            error = Math.Abs(calculated - expected);
            Assert.False(error > value.Tol, Failure("Wulin/Yong value", value, expected, calculated, error, value.Tol));

            calculated = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
                opt, RunnerContext, stochProcess, 300).NPV!.Value;
            expected = value.Result;
            error = Math.Abs(calculated - expected);
            double binomialTol = 0.28;
            Assert.False(error > binomialTol, Failure("Binomial value", value, expected, calculated, error, binomialTol));

            calculated = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniDoubleBarrierOption>(
                opt, RunnerContext, stochProcess, 300).NPV!.Value;
            expected = value.Result;
            error = Math.Abs(calculated - expected);
            binomialTol = 0.033; // error one order of magnitude lower than plain binomial
            Assert.False(
                error > binomialTol, Failure("Binomial (Derman) value", value, expected, calculated, error, binomialTol));

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

    [Fact]
    public void TestVannaVolgaDoubleBarrierValues()
    {
        // Testing double-barrier FX options against Vanna/Volga values...

        DoubleBarrierFxOptionData[] values =
        [
            // BarrierType, barr.1, barr.2, rebate, type, strike, s, q, r, t, vol25Put, volAtm, vol25Call, vol, result, tol
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.11638, 0.14413, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.10088, 0.07456, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.08925, 0.02710, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.08463, 0.00569, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.08412, 0.00013, 1.0e-4),

            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.11638, 0.00017, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.10088, 0.00353, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.08925, 0.02221, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.08463, 0.06049, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1.0,
                0.10087, 0.08925, 0.08463, 0.08412, 0.11103, 1.0e-4),

            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.12511, 0.19981, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.10890, 0.10389, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.09444, 0.03555, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.09197, 0.00634, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.09261, 0.00000, 1.0e-4),

            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.12511, 0.00000, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.10890, 0.00436, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Put, 1.32238, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.09444, 0.03173, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Put, 1.44298, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.09197, 0.09346, 1.0e-4),
            new(DoubleBarrierType.KnockOut, 1.0, 1.6, 0.0, OptionType.Put, 1.56345, 1.30265, 0.0009418, 0.0039788, 2.0,
                0.10891, 0.09525, 0.09197, 0.09261, 0.17704, 1.0e-4),
        ];

        DayCounter dc = new Actual360();
        var today = new Date(05, March, 2013);
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (DoubleBarrierFxOptionData value in values)
        {
            for (int j = 0; j <= 1; j++)
            {
                var barrierType = (DoubleBarrierType)j;

                // the rows nanoql's clock reads move past QuantLib's tolerance run on the case restated to Actual/365F,
                // the engine's own clock
                bool restated = RestatedValues.TryGetValue(
                    (value.Type, value.Strike, value.T, barrierType), out (double SuoWang, double Analytic) quantLib);
                DayCounter rowDc = restated ? new Actual365Fixed() : dc;
                YieldTermStructure qTS = FlatRate(today, value.Q, rowDc);
                YieldTermStructure rTS = FlatRate(today, value.R, rowDc);

                var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

                Date exDate = today + TimeToDays(value.T, 365);
                var exercise = new EuropeanExercise(exDate);

                var volAtmQuote = new DeltaVolQuote(
                    value.VolAtm, DeltaVolQuoteDeltaType.Fwd, value.T, DeltaVolQuoteAtmType.AtmDeltaNeutral);

                // always delta neutral atm
                var vol25PutQuote = new DeltaVolQuote(-0.25, value.Vol25Put, value.T, DeltaVolQuoteDeltaType.Fwd);
                var vol25CallQuote = new DeltaVolQuote(0.25, value.Vol25Call, value.T, DeltaVolQuoteDeltaType.Fwd);

                var doubleBarrierOption = new DoubleBarrierOption(
                    barrierType, value.Barrier1, value.Barrier2, value.Rebate, payoff, exercise);

                double bsVanillaPrice = BlackFormulaFunctions.BlackFormula(
                    value.Type,
                    value.Strike,
                    value.S * qTS.Discount(value.T) / rTS.Discount(value.T),
                    value.V * Math.Sqrt(value.T),
                    rTS.Discount(value.T));
                OneAssetOptionResults results = VannaVolgaDoubleBarrierEngine.Calculate(
                    doubleBarrierOption,
                    context,
                    SuoWangDoubleBarrierEngine.Calculate,
                    volAtmQuote,
                    vol25PutQuote,
                    vol25CallQuote,
                    value.S,
                    rTS,
                    qTS,
                    true,
                    bsVanillaPrice);

                // Expected result for KO is given in array, for KI is evaluated as vanilla - KO
                double expected = 0;
                if (barrierType == DoubleBarrierType.KnockOut)
                {
                    expected = value.Result;
                }
                else if (barrierType == DoubleBarrierType.KnockIn)
                {
                    expected = bsVanillaPrice - value.Result;
                }

                // on the restated case, QuantLib's engine's own value there plus the domestic discount's own move: the
                // change of the smile's vanilla from QuantLib's foreign discount in both places, scaled by the survival
                // probability, an out option by −λ·Δ and an in option by +λ·Δ under adaptVanDelta
                // (Divergences/VannaVolgaDoubleBarrierTests.cs)
                if (restated)
                {
                    expected = quantLib.SuoWang + DomesticDiscountMove(results, barrierType, value, qTS, rTS);
                }

                double calculated = results.NPV!.Value;
                double error = Math.Abs(calculated - expected);
                Assert.False(error > value.Tol, VannaVolgaFailure(value, barrierType, expected, calculated, error, value.Tol));

                OneAssetOptionResults analyticResults = VannaVolgaDoubleBarrierEngine.Calculate(
                    doubleBarrierOption,
                    context,
                    AnalyticDoubleBarrierEngine.Calculate,
                    volAtmQuote,
                    vol25PutQuote,
                    vol25CallQuote,
                    value.S,
                    rTS,
                    qTS,
                    true,
                    bsVanillaPrice);
                if (restated)
                {
                    expected = quantLib.Analytic + DomesticDiscountMove(analyticResults, barrierType, value, qTS, rTS);
                }

                calculated = analyticResults.NPV!.Value;
                error = Math.Abs(calculated - expected);
                double maxtol = 5.0e-3; // different engines have somewhat different results
                Assert.False(error > maxtol, VannaVolgaFailure(value, barrierType, expected, calculated, error, value.Tol));
            }
        }
    }

    [Fact]
    public void TestMonteCarloDoubleBarrierWithAnalytical()
    {
        // Testing MC double-barrier options against analytical values...

        double tolerance = 0.01; // percentage difference between analytical and monte carlo values to be tolerated

        // set up dates
        var todaysDate = new Date(15, May, 1998);
        var settlementDate = new Date(17, May, 1998);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        // our options
        var type = OptionType.Put;
        double underlying = 36;
        double strike = 40;
        double dividendYield = 0.00;
        double riskFreeRate = 0.06;
        double volatility = 0.20;
        var maturity = new Date(17, May, 1999);
        DayCounter dayCounter = new Actual365Fixed();

        var europeanExercise = new EuropeanExercise(maturity);

        // bootstrap the yield/dividend/vol curves
        var flatTermStructure = new FlatForward(settlementDate, riskFreeRate, dayCounter);
        var flatDividendTS = new FlatForward(settlementDate, dividendYield, dayCounter);
        var flatVolTS = new BlackConstantVol(settlementDate, new TARGET(), volatility, dayCounter);
        var payoff = new PlainVanillaPayoff(type, strike);
        var bsmProcess = new BlackScholesMertonProcess(underlying, flatDividendTS, flatTermStructure, flatVolTS);

        double barrierLow = underlying * 0.9;
        double barrierHigh = underlying * 1.1;

        var knockIndoubleBarrierOption = new DoubleBarrierOption(
            DoubleBarrierType.KnockIn, barrierLow, barrierHigh, 0, payoff, europeanExercise);

        double analytical = AnalyticDoubleBarrierEngine.Calculate(knockIndoubleBarrierOption, context, bsmProcess).NPV!.Value;

        Func<DoubleBarrierOption, EvaluationContext, OneAssetOptionResults> mcdoublebarrierengine =
            new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(bsmProcess, 1)
                .WithSteps(5000)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(0.5)
                .ToEngine();
        double monteCarlo = mcdoublebarrierengine(knockIndoubleBarrierOption, context).NPV!.Value;

        double percentageDiff = Math.Abs(analytical - monteCarlo) / analytical;

        Assert.False(
            percentageDiff > tolerance,
            $"Threshold exceeded: {percentageDiff}, analytical: {analytical}, Monte Carlo: {monteCarlo}");

        var knockOutDoubleBarrierOption = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, barrierLow, barrierHigh, 0, payoff, europeanExercise);

        analytical = AnalyticDoubleBarrierEngine.Calculate(knockOutDoubleBarrierOption, context, bsmProcess).NPV!.Value;

        tolerance = 0.01;

        mcdoublebarrierengine = new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(bsmProcess, 10)
            .WithSteps(5000)
            .WithAntitheticVariate()
            .WithAbsoluteTolerance(tolerance)
            .ToEngine();
        monteCarlo = mcdoublebarrierengine(knockOutDoubleBarrierOption, context).NPV!.Value;

        double diff = Math.Abs(analytical - monteCarlo);

        Assert.False(diff > tolerance, $"Threshold exceeded: {diff}, analytical: {analytical}, Monte Carlo: {monteCarlo}");
    }

    // QuantLib's master's case for #2878, taken with its fix
    [Fact]
    public void TestBinomialExerciseAtMaturity()
    {
        // Testing binomial double-barrier exercise at maturity...

        var today = new Date(21, September, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + 91;
        var process = new BlackScholesMertonProcess(
            100.0, FlatRate(today, 0.0, dc), FlatRate(today, 0.05, dc), FlatVol(today, 0.15, dc));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 85.0);
        var europeanExercise = new EuropeanExercise(maturity);
        var americanExercise = new AmericanExercise(today, maturity);
        int timeSteps = 51;
        double expected = BinomialVanillaEngine.Calculate<CoxRossRubinstein>(
            new VanillaOption(payoff, europeanExercise), context, process, timeSteps).NPV!.Value;
        double tolerance = 1.0e-10;

        // All stock nodes lie between 58 and 171, so neither barrier can knock out.
        var european = new DoubleBarrierOption(DoubleBarrierType.KnockOut, 1.0, 1000.0, 0.0, payoff, europeanExercise);
        var american = new DoubleBarrierOption(DoubleBarrierType.KnockOut, 1.0, 1000.0, 0.0, payoff, americanExercise);
        double europeanValue = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
            european, context, process, timeSteps).NPV!.Value;
        double americanValue = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
            american, context, process, timeSteps).NPV!.Value;
        Assert.True(
            Math.Abs(europeanValue - expected) <= tolerance,
            $"European double-barrier value {europeanValue} differs from vanilla value {expected}");
        Assert.True(
            Math.Abs(americanValue - expected) <= tolerance,
            $"Non-dividend-paying American double-barrier value {americanValue} differs from vanilla value {expected}");
    }

    // QuantLib's engine's values on the rows nanoql's clock reads move, on the case restated to Actual/365F, its
    // Suo-Wang and analytic inner engines' (QuantLib 1.44)
    private static readonly Dictionary<(OptionType Type, double Strike, double T, DoubleBarrierType BarrierType),
        (double SuoWang, double Analytic)> RestatedValues = new()
    {
        [(OptionType.Call, 1.13321, 1.0, DoubleBarrierType.KnockIn)] = (0.035439814070026, 0.035439814035450),
        [(OptionType.Call, 1.13321, 1.0, DoubleBarrierType.KnockOut)] = (0.144907573673456, 0.144907573708033),
        [(OptionType.Call, 1.22687, 1.0, DoubleBarrierType.KnockIn)] = (0.025132141158806, 0.025132141157170),
        [(OptionType.Call, 1.22687, 1.0, DoubleBarrierType.KnockOut)] = (0.074959936999909, 0.074959937001545),
        [(OptionType.Put, 1.38843, 1.0, DoubleBarrierType.KnockIn)] = (0.036585195083795, 0.036585195129096),
        [(OptionType.Put, 1.38843, 1.0, DoubleBarrierType.KnockOut)] = (0.060703298222048, 0.060703298176746),
        [(OptionType.Put, 1.46047, 1.0, DoubleBarrierType.KnockIn)] = (0.046843616828046, 0.046843616852792),
        [(OptionType.Put, 1.46047, 1.0, DoubleBarrierType.KnockOut)] = (0.111511724079918, 0.111511724055171),
        [(OptionType.Call, 1.06145, 2.0, DoubleBarrierType.KnockIn)] = (0.058093455258613, 0.058093455264736),
        [(OptionType.Call, 1.06145, 2.0, DoubleBarrierType.KnockOut)] = (0.200852801984615, 0.200852801978493),
        [(OptionType.Call, 1.19545, 2.0, DoubleBarrierType.KnockIn)] = (0.042171478673604, 0.042171478646976),
        [(OptionType.Call, 1.19545, 2.0, DoubleBarrierType.KnockOut)] = (0.104412638045960, 0.104412638072587),
        [(OptionType.Put, 1.44298, 2.0, DoubleBarrierType.KnockIn)] = (0.061147953381170, 0.061147953396412),
        [(OptionType.Put, 1.44298, 2.0, DoubleBarrierType.KnockOut)] = (0.093851416721637, 0.093851416706395),
        [(OptionType.Put, 1.56345, 2.0, DoubleBarrierType.KnockIn)] = (0.080619822258938, 0.080619822228357),
        [(OptionType.Put, 1.56345, 2.0, DoubleBarrierType.KnockOut)] = (0.177883812638881, 0.177883812669461),
    };

    // The domestic discount's move of a row: the engine's own vanilla on the smile less the one QuantLib's
    // interpolation gives with the foreign discount in both places, times the survival probability, an out option's
    // −λ·Δ and an in option's +λ·Δ under adaptVanDelta
    private static double DomesticDiscountMove(
        OneAssetOptionResults results,
        DoubleBarrierType barrierType,
        DoubleBarrierFxOptionData value,
        YieldTermStructure qTS,
        YieldTermStructure rTS)
    {
        double move = results.Result<double>("lambda")
            * (results.Result<double>("VanillaPrice") - VanillaOnForeignDiscountSmile(value, qTS, rTS));
        return barrierType == DoubleBarrierType.KnockOut ? -move : move;
    }

    // The vanilla QuantLib's engine prices on the smile, its interpolation given the foreign discount in both places
    // (vannavolgadoublebarrierengine.hpp:144), from public pieces: the strikes from the delta calculators, the
    // interpolation and Black's formula, each discount at the quotes' time
    private static double VanillaOnForeignDiscountSmile(
        DoubleBarrierFxOptionData value, YieldTermStructure qTS, YieldTermStructure rTS)
    {
        double t = value.T;
        double sqrtT = Math.Sqrt(t);
        double dDiscount = rTS.Discount(t);
        double fDiscount = qTS.Discount(t);
        double atmStrike = new BlackDeltaCalculator(
            OptionType.Call, DeltaVolQuoteDeltaType.Fwd, value.S, dDiscount, fDiscount, value.VolAtm * sqrtT)
            .AtmStrike(DeltaVolQuoteAtmType.AtmDeltaNeutral);
        double put25Strike = new BlackDeltaCalculator(
            OptionType.Put, DeltaVolQuoteDeltaType.Fwd, value.S, dDiscount, fDiscount, value.Vol25Put * sqrtT)
            .StrikeFromDelta(-0.25);
        double call25Strike = new BlackDeltaCalculator(
            OptionType.Call, DeltaVolQuoteDeltaType.Fwd, value.S, dDiscount, fDiscount, value.Vol25Call * sqrtT)
            .StrikeFromDelta(0.25);
        Interpolation smile = new VannaVolga(value.S, fDiscount, fDiscount, t).Interpolate(
            [put25Strike, atmStrike, call25Strike], [value.Vol25Put, value.VolAtm, value.Vol25Call]);
        double strikeVol = smile.Evaluate(value.Strike, allowExtrapolation: true);
        return BlackFormulaFunctions.BlackFormula(
            value.Type, value.Strike, value.S * fDiscount / dDiscount, strikeVol * sqrtT, dDiscount);
    }

    private static string VannaVolgaFailure(
        DoubleBarrierFxOptionData value,
        DoubleBarrierType barrierType,
        double expected,
        double calculated,
        double error,
        double tolerance) =>
        $"Double Barrier Option {DoubleBarrierTypeFunctions.ToString(barrierType)} European {value.Type} option: "
        + $"underlying value {value.S}, strike {value.Strike}, barrier1 {value.Barrier1}, barrier2 {value.Barrier2}, "
        + $"rebate {value.Rebate}, dividend yield {value.Q}, risk-free rate {value.R}, 25PutVol {value.Vol25Put}, "
        + $"atmVol {value.VolAtm}, 25CallVol {value.Vol25Call}, volatility {value.V}: expected value {expected}, "
        + $"calculated value {calculated}, error {error}, tolerance {tolerance}";

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

    private sealed record DoubleBarrierFxOptionData(
        DoubleBarrierType BarrierType,
        double Barrier1,
        double Barrier2,
        double Rebate,
        OptionType Type,
        double Strike,
        double S, // spot
        double Q, // dividend
        double R, // risk-free rate
        double T, // time to maturity
        double Vol25Put, // 25 delta put vol
        double VolAtm, // atm vol
        double Vol25Call, // 25 delta call vol
        double V, // volatility at strike
        double Result, // result
        double Tol); // tolerance

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
