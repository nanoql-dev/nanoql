// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/barrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
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
// and a quote set anew is a curve or process built at the new value.
[Trait("Oracle", "QuantLib")]
public class BarrierOptionTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestParity()
    {
        // Testing that knock-in plus knock-out barrier options replicate a European option...

        Date today = RunnerContext.EvaluationDate;

        DayCounter dc = new Actual360();

        double spot = 100.0;
        YieldTermStructure rTS = FlatRate(today, 0.01, dc);
        BlackVolTermStructure volTS = FlatVol(today, 0.20, dc);

        BlackScholesProcess stochProcess = BlackScholesProcess.Create(spot, rTS, volTS, RunnerContext);

        Date exerciseDate = today + (6 * TimeUnit.Months);

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);

        Exercise exercise = new EuropeanExercise(exerciseDate);

        var knockIn = new BarrierOption(BarrierType.DownIn, 90.0, 0.0, payoff, exercise);
        var knockOut = new BarrierOption(BarrierType.DownOut, 90.0, 0.0, payoff, exercise);
        var european = new EuropeanOption(payoff, exercise);

        double knockInNpv = AnalyticBarrierEngine.Calculate(knockIn, RunnerContext, stochProcess).NPV!.Value;
        double knockOutNpv = AnalyticBarrierEngine.Calculate(knockOut, RunnerContext, stochProcess).NPV!.Value;
        double replicated = knockInNpv + knockOutNpv;
        double expected = AnalyticEuropeanEngine.Calculate(european, RunnerContext, stochProcess).NPV!.Value;
        double error = Math.Abs(replicated - expected);
        Assert.True(
            error <= 1e-7,
            $"Failed to replicate European option: knock-in {knockInNpv}, knock-out {knockOutNpv}, "
            + $"replicated {replicated}, expected {expected}, error {error:E3}");

        // try again with different day counters
        stochProcess = BlackScholesProcess.Create(
            spot, rTS, FlatVol(today, 0.20, new Business252(new TARGET())), RunnerContext);

        knockInNpv = AnalyticBarrierEngine.Calculate(knockIn, RunnerContext, stochProcess).NPV!.Value;
        knockOutNpv = AnalyticBarrierEngine.Calculate(knockOut, RunnerContext, stochProcess).NPV!.Value;
        replicated = knockInNpv + knockOutNpv;
        expected = AnalyticEuropeanEngine.Calculate(european, RunnerContext, stochProcess).NPV!.Value;
        error = Math.Abs(replicated - expected);
        Assert.True(
            error <= 1e-7,
            $"Failed to replicate European option: knock-in {knockInNpv}, knock-out {knockOutNpv}, "
            + $"replicated {replicated}, expected {expected}, error {error:E3}");
    }

    [Fact]
    public void TestPutCallSymmetry()
    {
        // Testing put-call symmetry for barrier options...

        Date today = RunnerContext.EvaluationDate;

        (double CallStrike, double CallBarrier, BarrierType CallType, double PutStrike, double PutBarrier,
            BarrierType PutType)[] cases =
        [
            (90, 95, BarrierType.DownOut, 111.11111, 105.26315, BarrierType.UpOut),
            (95, 95, BarrierType.DownOut, 105.26315, 105.26315, BarrierType.UpOut),
            (100, 95, BarrierType.DownOut, 100.0, 105.26315, BarrierType.UpOut),
            (105, 95, BarrierType.DownOut, 95.23809, 105.26315, BarrierType.UpOut),
            (110, 95, BarrierType.DownOut, 90.90909, 105.26315, BarrierType.UpOut),

            (90.0, 120.0, BarrierType.UpOut, 111.11111, 83.33333, BarrierType.DownOut),
            (95.0, 120.0, BarrierType.UpOut, 105.26315, 83.33333, BarrierType.DownOut),
            (100.0, 120.0, BarrierType.UpOut, 100.0, 83.33333, BarrierType.DownOut),
            (105.0, 120.0, BarrierType.UpOut, 95.23809, 83.33333, BarrierType.DownOut),
            (110.0, 120.0, BarrierType.UpOut, 90.90909, 83.33333, BarrierType.DownOut),
        ];

        DayCounter dc = new Actual360();
        Date maturity = today + 360;
        Exercise exercise = new EuropeanExercise(maturity);
        double r = 0.01;
        double rebate = 0.0;
        double spotPrice = 100;

        BlackVolTermStructure blackVolTS = FlatVol(today, 0.25, dc);

        var processCall = new BlackScholesMertonProcess(
            spotPrice, FlatRate(today, 0.0, dc), FlatRate(today, r, dc), blackVolTS);
        var processPut = new BlackScholesMertonProcess(
            spotPrice, FlatRate(today, r, dc), FlatRate(today, 0.0, dc), blackVolTS);

        foreach (var i in cases)
        {
            StrikedTypePayoff putPayoff = new PlainVanillaPayoff(OptionType.Put, i.PutStrike);
            StrikedTypePayoff callPayoff = new PlainVanillaPayoff(OptionType.Call, i.CallStrike);
            var putOption = new BarrierOption(i.PutType, i.PutBarrier, rebate, putPayoff, exercise);
            var callOption = new BarrierOption(i.CallType, i.CallBarrier, rebate, callPayoff, exercise);

            double putValue = AnalyticBarrierEngine.Calculate(putOption, RunnerContext, processPut).NPV!.Value;
            double callValue = AnalyticBarrierEngine.Calculate(callOption, RunnerContext, processCall).NPV!.Value;
            double callAmount = i.PutStrike / spotPrice;
            double error = Math.Abs(putValue - (callAmount * callValue));
            double tolerance = 1e-4;
            Assert.True(
                error <= tolerance,
                $"Failed to reproduce the put-call symmetry for the partial-time barrier options: error {error}");
        }
    }

    [Fact]
    public void TestHaugValues()
    {
        // Testing barrier options against Haug's values...

        ExerciseType european = ExerciseType.European;
        ExerciseType american = ExerciseType.American;
        NewBarrierOptionData[] values =
        [
            /* The data below are from
              "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 pag. 72
            */
            // barrierType, barrier, rebate, type, exercise, strk, s, q, r, t, v, result, tol
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 9.0246,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 6.7924,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 4.8759,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 2.6789,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 2.3580,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 2.3453,
                1.0e-4),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 7.7627,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 4.0109,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 2.0576,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 13.8333,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 7.8494,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 3.9795,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 14.1112,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 8.4482,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 4.5910,
                1.0e-4),

            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 8.8334,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 7.0285,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 5.4137,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 3.0000,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 2.6341,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 2.4389,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 2.4315,
                1.0e-4),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 9.0093,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 5.1370,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 2.8517,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 14.8816,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 9.2045,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 5.3043,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 15.2098,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 9.7278,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 5.8350,
                1.0e-4),

            // barrierType, barrier, rebate, type, exercise, strk, s, q, r, t, v, result, tol
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 2.2798,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 2.2947,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 2.6252,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 3.7760,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 5.4932,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 7.5187,
                1.0e-4),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 2.9586,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 6.5677,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 11.9752,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 2.2845,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 5.9085,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 11.6465,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 1.4653,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.3721,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 7.0846,
                1.0e-4),

            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 2.4170,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 2.4258,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 2.6246,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 3.0000,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 4.2293,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 5.8032,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 7.5649,
                1.0e-4),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 3.8769,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 7.7989,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 13.3078,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 3.3328,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 7.2636,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 12.9713,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, european, 90, 100.0, 0.04, 0.08, 0.50, 0.30, 2.0658,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, european, 100, 100.0, 0.04, 0.08, 0.50, 0.30, 4.4226,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, european, 110, 100.0, 0.04, 0.08, 0.50, 0.30, 8.3686,
                1.0e-4),

            // Options with american exercise: values computed with 400 steps of Haug's VBA code
            // (handles only out options)
            // barrierType, barrier, rebate, type, exercise, strk, s, q, r, t, v, result, tol
            new(BarrierType.DownOut, 95.0, 0.0, OptionType.Call, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 10.4655,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 0.0, OptionType.Call, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 4.5159,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 0.0, OptionType.Call, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 2.5971,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 0.0, OptionType.Call, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 11.8076,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 0.0, OptionType.Call, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.3993,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 2.3457,
                1.0e-4),

            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 2.2795,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 0.0, OptionType.Put, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.3512,
                1.0e-4),
            new(BarrierType.DownOut, 95.0, 0.0, OptionType.Put, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 11.5773,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 3.0000,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 0.0, OptionType.Put, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 1.4763,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 0.0, OptionType.Put, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 3.3001,
                1.0e-4),
            new(BarrierType.UpOut, 105.0, 0.0, OptionType.Put, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 10.0000,
                1.0e-4),

            // some american in-options - results (roughly) verified with other numerical methods
            // barrierType, barrier, rebate, type, exercise, strk, s, q, r, t, v, result, tol
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 7.7615,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, american, 100, 100.0, 0.04, 0.08, 0.50, 0.25, 4.0118,
                1.0e-4),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 2.0544,
                1.0e-4),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 13.8308,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, american, 90, 100.0, 0.04, 0.08, 0.50, 0.25, 14.1150,
                1.0e-4),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, american, 110, 100.0, 0.04, 0.08, 0.50, 0.25, 4.5900,
                1.0e-4),

            /*
                Data from "Going to Extreme: Correcting Simulation Bias in Exotic
                Option Valuation"
                D.R. Beaglehole, P.H. Dybvig and G. Zhou
                Financial Analysts Journal; Jan / Feb 1997; 53, 1
            */
            // barrierType, barrier, rebate, type, strike, s, q, r, t, v, result, tol
            // { Barrier::DownOut, 45.0, 0.0, Option::Put, 50, 50.0,-0.05, 0.10, 0.25, 0.50, 4.032, 1.0e-3 },
            // { Barrier::DownOut, 45.0, 0.0, Option::Put, 50, 50.0,-0.05, 0.10, 1.00, 0.50, 5.477, 1.0e-3 }
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        void RunTest(NewBarrierOptionData value, bool useZeroSpot, bool useTriggeredBarrier)
        {
            Date exDate = today + TimeToDays(value.T);

            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            var stochProcess = new BlackScholesMertonProcess(
                useZeroSpot ? 0.0 : value.S,
                FlatRate(today, value.Q, dc),
                FlatRate(today, value.R, dc),
                FlatVol(today, value.V, dc));

            Exercise exercise = value.ExType == ExerciseType.European
                ? new EuropeanExercise(exDate)
                : new AmericanExercise(exDate);

            double barrier = useTriggeredBarrier
                ? (value.BarrierType is BarrierType.DownIn or BarrierType.DownOut ? value.S * 1.01 : value.S * 0.99)
                : value.Barrier;

            var barrierOption = new BarrierOption(value.BarrierType, barrier, value.Rebate, payoff, exercise);

            // AnalyticBarrierEngine and FdBlackScholesBarrierEngine support only european exercise type

            bool priced = !useZeroSpot && !useTriggeredBarrier && value.ExType == ExerciseType.European;
            if (priced)
            {
                double calculated =
                    AnalyticBarrierEngine.Calculate(barrierOption, RunnerContext, stochProcess).NPV!.Value;
                double expected = value.Result;
                double error = Math.Abs(calculated - expected);
                Assert.True(error <= value.Tol, Failure("value", value, today, expected, calculated, error, value.Tol));
            }
            else
            {
                AssertThrown(() => AnalyticBarrierEngine.Calculate(barrierOption, RunnerContext, stochProcess));
            }

            if (priced)
            {
                double calculated = FdBlackScholesBarrierEngine.Calculate(
                    barrierOption, RunnerContext, stochProcess, tGrid: 200, xGrid: 400).NPV!.Value;
                double expected = value.Result;
                double error = Math.Abs(calculated - expected);
                Assert.True(error <= 5.0e-3, Failure("fd value", value, today, expected, calculated, error, value.Tol));
            }
            else
            {
                AssertThrown(() => FdBlackScholesBarrierEngine.Calculate(
                    barrierOption, RunnerContext, stochProcess, tGrid: 200, xGrid: 400));
            }

            if (!useZeroSpot && !useTriggeredBarrier)
            {
                double calculated = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
                    barrierOption, RunnerContext, stochProcess, 400).NPV!.Value;
                double expected = value.Result;
                double error = Math.Abs(calculated - expected);
                double tol = 1.1e-2;
                Assert.True(
                    error <= tol, Failure("Binomial (Boyle-lau) value", value, today, expected, calculated, error, tol));
            }
            else
            {
                AssertThrown(() => BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
                    barrierOption, RunnerContext, stochProcess, 400));
            }

            // Note: here, to test Derman convergence, we force maxTimeSteps to
            // timeSteps, effectively disabling Boyle-Lau barrier adjustment.
            // Production code should always enable Boyle-Lau. In most cases it
            // gives very good convergence with only a modest timeStep increment.
            if (!useZeroSpot && !useTriggeredBarrier)
            {
                double calculated = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniBarrierOption>(
                    barrierOption, RunnerContext, stochProcess, 400).NPV!.Value;
                double expected = value.Result;
                double error = Math.Abs(calculated - expected);
                double tol = 4e-2;
                Assert.True(
                    error <= tol, Failure("Binomial (Derman) value", value, today, expected, calculated, error, tol));
            }
            else
            {
                AssertThrown(() => BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniBarrierOption>(
                    barrierOption, RunnerContext, stochProcess, 400));
            }
        }

        foreach (NewBarrierOptionData value in values)
        {
            RunTest(value, false, false);
            RunTest(value, true, false);
            RunTest(value, false, true);
        }
    }

    [Fact]
    public void TestBabsiriValues()
    {
        // Testing barrier options against Babsiri's values...

        /*
            Data from
            "Simulating Path-Dependent Options: A New Approach"
              - M. El Babsiri and G. Noel
                Journal of Derivatives; Winter 1998; 6, 2
        */
        BarrierOptionData[] values =
        [
            new(BarrierType.DownIn, 0.10, 100, 90, 0.07187, 0.0),
            new(BarrierType.DownIn, 0.15, 100, 90, 0.60638, 0.0),
            new(BarrierType.DownIn, 0.20, 100, 90, 1.64005, 0.0),
            new(BarrierType.DownIn, 0.25, 100, 90, 2.98495, 0.0),
            new(BarrierType.DownIn, 0.30, 100, 90, 4.50952, 0.0),
            new(BarrierType.UpIn, 0.10, 100, 110, 4.79148, 0.0),
            new(BarrierType.UpIn, 0.15, 100, 110, 7.08268, 0.0),
            new(BarrierType.UpIn, 0.20, 100, 110, 9.11008, 0.0),
            new(BarrierType.UpIn, 0.25, 100, 110, 11.06148, 0.0),
            new(BarrierType.UpIn, 0.30, 100, 110, 12.98351, 0.0),
        ];

        double underlyingPrice = 100.0;
        double rebate = 0.0;
        double r = 0.05;
        double q = 0.02;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        YieldTermStructure qTS = FlatRate(today, q, dc);
        YieldTermStructure rTS = FlatRate(today, r, dc);

        Date exDate = today + 360;
        Exercise exercise = new EuropeanExercise(exDate);

        foreach (BarrierOptionData value in values)
        {
            StrikedTypePayoff callPayoff = new PlainVanillaPayoff(OptionType.Call, value.Strike);

            var stochProcess = new BlackScholesMertonProcess(
                underlyingPrice, qTS, rTS, FlatVol(today, value.Volatility, dc));

            // analytic
            var barrierCallOption = new BarrierOption(value.Type, value.Barrier, rebate, callPayoff, exercise);
            double calculated =
                AnalyticBarrierEngine.Calculate(barrierCallOption, RunnerContext, stochProcess).NPV!.Value;
            double expected = value.CallValue;
            double error = Math.Abs(calculated - expected);
            double maxErrorAllowed = 1.0e-5;
            Assert.True(
                error <= maxErrorAllowed,
                $"{BarrierTypeFunctions.ToString(value.Type)} barrier {value.Barrier}, volatility {value.Volatility}: "
                + $"expected {expected}, calculated {calculated}, error {error}");

            double maxMcRelativeErrorAllowed = 2.0e-2;

            Func<BarrierOption, EvaluationContext, OneAssetOptionResults> mcEngine =
                new MakeMCBarrierEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 5)
                    .WithStepsPerYear(1)
                    .WithBrownianBridge()
                    .WithSamples(131071) // 2^17-1
                    .WithMaxSamples(1048575) // 2^20-1
                    .ToEngine();

            calculated = mcEngine(barrierCallOption, RunnerContext).NPV!.Value;
            error = Math.Abs(calculated - expected) / expected;
            Assert.True(
                error <= maxMcRelativeErrorAllowed,
                $"{BarrierTypeFunctions.ToString(value.Type)} barrier {value.Barrier}, volatility {value.Volatility}: "
                + $"expected {expected}, calculated {calculated}, relative error {error}");
        }
    }

    [Fact]
    public void TestBeagleholeValues()
    {
        // Testing barrier options against Beaglehole's values...

        /*
            Data from
            "Going to Extreme: Correcting Simulation Bias in Exotic
             Option Valuation"
              - D.R. Beaglehole, P.H. Dybvig and G. Zhou
                Financial Analysts Journal; Jan / Feb 1997; 53, 1
        */
        BarrierOptionData[] values =
        [
            new(BarrierType.DownOut, 0.50, 50, 45, 5.477, 0.0),
        ];

        double underlyingPrice = 50.0;
        double rebate = 0.0;
        double r = Math.Log(1.1);
        double q = 0.00;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        YieldTermStructure qTS = FlatRate(today, q, dc);
        YieldTermStructure rTS = FlatRate(today, r, dc);

        Date exDate = today + 360;
        Exercise exercise = new EuropeanExercise(exDate);

        foreach (BarrierOptionData value in values)
        {
            StrikedTypePayoff callPayoff = new PlainVanillaPayoff(OptionType.Call, value.Strike);

            var stochProcess = new BlackScholesMertonProcess(
                underlyingPrice, qTS, rTS, FlatVol(today, value.Volatility, dc));

            var barrierCallOption = new BarrierOption(value.Type, value.Barrier, rebate, callPayoff, exercise);
            double calculated =
                AnalyticBarrierEngine.Calculate(barrierCallOption, RunnerContext, stochProcess).NPV!.Value;
            double expected = value.CallValue;
            double maxErrorAllowed = 1.0e-3;
            double error = Math.Abs(calculated - expected);
            Assert.True(
                error <= maxErrorAllowed,
                $"{BarrierTypeFunctions.ToString(value.Type)} barrier {value.Barrier}: expected {expected}, "
                + $"calculated {calculated}, error {error}");

            double maxMcRelativeErrorAllowed = 0.01;
            Func<BarrierOption, EvaluationContext, OneAssetOptionResults> mcEngine =
                new MakeMCBarrierEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 10)
                    .WithStepsPerYear(1)
                    .WithBrownianBridge()
                    .WithSamples(131071) // 2^17-1
                    .WithMaxSamples(1048575) // 2^20-1
                    .ToEngine();

            calculated = mcEngine(barrierCallOption, RunnerContext).NPV!.Value;
            error = Math.Abs(calculated - expected) / expected;
            Assert.True(
                error <= maxMcRelativeErrorAllowed,
                $"{BarrierTypeFunctions.ToString(value.Type)} barrier {value.Barrier}: expected {expected}, "
                + $"calculated {calculated}, relative error {error}");
        }
    }

    [Fact]
    public void TestLocalVolAndHestonComparison()
    {
        // Testing local volatility and Heston FD engines for barrier options...

        var settlementDate = new Date(5, July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        Calendar calendar = new TARGET();

        Integer[] t = [13, 41, 75, 165, 256, 345, 524, 703];
        Rate[] r = [0.0357, 0.0349, 0.0341, 0.0355, 0.0359, 0.0368, 0.0386, 0.0401];

        List<Rate> rates = [0.0357];
        List<Date> dates = [settlementDate];
        for (Size i = 0; i < 8; ++i)
        {
            dates.Add(settlementDate + t[i]);
            rates.Add(r[i]);
        }

        YieldTermStructure rTS = new ZeroCurve(dates, rates, dayCounter);
        YieldTermStructure qTS = FlatRate(settlementDate, 0.0, dayCounter);

        Real s0 = 4500.00;

        Real[] strikes =
        [
            100, 500, 2000, 3400, 3600, 3800, 4000, 4200, 4400, 4500,
            4600, 4800, 5000, 5200, 5400, 5600, 7500, 10000, 20000, 30000,
        ];

        Volatility[] v =
        [
            1.015873, 1.015873, 1.015873, 0.89729, 0.796493, 0.730914, 0.631335, 0.568895,
            0.711309, 0.711309, 0.711309, 0.641309, 0.635593, 0.583653, 0.508045, 0.463182,
            0.516034, 0.500534, 0.500534, 0.500534, 0.448706, 0.416661, 0.375470, 0.353442,
            0.516034, 0.482263, 0.447713, 0.387703, 0.355064, 0.337438, 0.316966, 0.306859,
            0.497587, 0.464373, 0.430764, 0.374052, 0.344336, 0.328607, 0.310619, 0.301865,
            0.479511, 0.446815, 0.414194, 0.361010, 0.334204, 0.320301, 0.304664, 0.297180,
            0.461866, 0.429645, 0.398092, 0.348638, 0.324680, 0.312512, 0.299082, 0.292785,
            0.444801, 0.413014, 0.382634, 0.337026, 0.315788, 0.305239, 0.293855, 0.288660,
            0.428604, 0.397219, 0.368109, 0.326282, 0.307555, 0.298483, 0.288972, 0.284791,
            0.420971, 0.389782, 0.361317, 0.321274, 0.303697, 0.295302, 0.286655, 0.282948,
            0.413749, 0.382754, 0.354917, 0.316532, 0.300016, 0.292251, 0.284420, 0.281164,
            0.400889, 0.370272, 0.343525, 0.307904, 0.293204, 0.286549, 0.280189, 0.277767,
            0.390685, 0.360399, 0.334344, 0.300507, 0.287149, 0.281380, 0.276271, 0.274588,
            0.383477, 0.353434, 0.327580, 0.294408, 0.281867, 0.276746, 0.272655, 0.271617,
            0.379106, 0.349214, 0.323160, 0.289618, 0.277362, 0.272641, 0.269332, 0.268846,
            0.377073, 0.347258, 0.320776, 0.286077, 0.273617, 0.269057, 0.266293, 0.266265,
            0.399925, 0.369232, 0.338895, 0.289042, 0.265509, 0.255589, 0.249308, 0.249665,
            0.423432, 0.406891, 0.373720, 0.314667, 0.281009, 0.263281, 0.246451, 0.242166,
            0.453704, 0.453704, 0.453704, 0.381255, 0.334578, 0.305527, 0.268909, 0.251367,
            0.517748, 0.517748, 0.517748, 0.416577, 0.364770, 0.331595, 0.287423, 0.264285,
        ];

        var blackVolMatrix = new Matrix(strikes.Length, dates.Count - 1);
        for (Size i = 0; i < strikes.Length; ++i)
        {
            for (Size j = 1; j < dates.Count; ++j)
            {
                blackVolMatrix[i, j - 1] = v[(i * (dates.Count - 1)) + j - 1];
            }
        }

        BlackVarianceSurface volTS = new BlackVarianceSurface(
                settlementDate, calendar, dates.Skip(1), strikes, blackVolMatrix, dayCounter)
            .SetInterpolation<Bicubic>();
        GeneralizedBlackScholesProcess localVolProcess = new BlackScholesMertonProcess(s0, qTS, rTS, volTS);

        Real v0 = 0.195662;
        Real kappa = 5.6628;
        Real theta = 0.0745911;
        Real sigma = 1.1619;
        Real rho = -0.511493;

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var hestonModel = new HestonModel(hestonProcess);

        Real strike = s0;
        Real barrier = 3000;
        Real rebate = 100;
        Date exDate = settlementDate + new Period(20, TimeUnit.Months);

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, strike);

        Exercise exercise = new EuropeanExercise(exDate);

        var barrierOption = new BarrierOption(BarrierType.DownOut, barrier, rebate, payoff, exercise);

        Real expectedHestonNPV = 111.5;
        Real calculatedHestonNPV =
            FdHestonBarrierEngine.Calculate(barrierOption, context, hestonModel, 100, 400, 50).NPV!.Value;

        Real expectedLocalVolNPV = 132.8;
        Real calculatedLocalVolNPV = FdBlackScholesBarrierEngine.Calculate(
            barrierOption, context, localVolProcess, [], 100, 400, 0, FdmSchemeDesc.Douglas(), true, 0.35).NPV!.Value;

        Real tol = 0.01;

        Assert.True(
            Math.Abs(expectedHestonNPV - calculatedHestonNPV) <= tol * expectedHestonNPV,
            $"Failed to reproduce Heston barrier price for strike {strike}, barrier {barrier}, maturity {exDate}: "
            + $"calculated {calculatedHestonNPV}, expected {expectedHestonNPV}");
        Assert.True(
            Math.Abs(expectedLocalVolNPV - calculatedLocalVolNPV) <= tol * expectedLocalVolNPV,
            $"Failed to reproduce Heston barrier price for strike {strike}, barrier {barrier}, maturity {exDate}: "
            + $"calculated {calculatedLocalVolNPV}, expected {expectedLocalVolNPV}");
    }

    [Fact]
    public void TestDividendBarrierOption()
    {
        // Testing barrier option pricing with discrete dividends...

        DayCounter dc = new Actual365Fixed();

        var today = new Date(11, February, 2018);
        Date maturity = today + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = today };

        double spot = 100.0;
        double strike = 105.0;
        double rebate = 5.0;

        double[] barriers = [80.0, 120.0, 80.0, 120.0];
        BarrierType[] barrierTypes = [BarrierType.DownOut, BarrierType.UpOut, BarrierType.DownIn, BarrierType.UpIn];

        double r = 0.05;
        double q = 0.0;
        double v = 0.02;

        YieldTermStructure qTS = FlatRate(today, q, dc);
        YieldTermStructure rTS = FlatRate(today, r, dc);
        BlackVolTermStructure volTS = FlatVol(today, v, dc);

        var bsProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        double divAmount = 30;
        Date divDate = today + new Period(6, TimeUnit.Months);
        ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([divDate], [divAmount]);

        FdmSchemeDesc[] schemes =
        [
            FdmSchemeDesc.Douglas(), FdmSchemeDesc.CrankNicolson(), FdmSchemeDesc.TrBDF2(), FdmSchemeDesc.CraigSneyd(),
            FdmSchemeDesc.Hundsdorfer(), FdmSchemeDesc.MethodOfLines(),
        ];

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, spot, v * v, 1.0, v * v, 0.005, 0.0));

        // the six schemes' engines, then the Heston engine
        Func<BarrierOption, double>[] engines =
        [
            .. schemes.Select<FdmSchemeDesc, Func<BarrierOption, double>>(scheme => o => FdBlackScholesBarrierEngine.Calculate(
                o, context, bsProcess, dividends, 100, 100, 0, scheme).NPV!.Value),
            o => FdHestonBarrierEngine.Calculate(o, context, hestonModel, dividends, 50, 101, 3).NPV!.Value,
        ];

        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        var exercise = new EuropeanExercise(maturity);

        double[] expected =
        [
            rTS.Discount(divDate) * rebate,
            payoff.Evaluate((spot - (divAmount * rTS.Discount(divDate))) / rTS.Discount(maturity))
                * rTS.Discount(maturity),
            29.154,
            4.765,
        ];

        double relTol = 2e-4;
        for (int i = 0; i < barriers.Length; ++i)
        {
            for (int j = 0; j < engines.Length; ++j)
            {
                double barrier = barriers[i];
                BarrierType barrierType = barrierTypes[i];

                var barrierOption = new BarrierOption(barrierType, barrier, rebate, payoff, exercise);

                double calculated = engines[j](barrierOption);
                double diff = Math.Abs(calculated - expected[i]);
                Assert.True(
                    diff <= relTol * expected[i],
                    $"Failed to reproduce barrier price with discrete dividends: engine {j}, strike {strike}, "
                    + $"barrier {barrier}, maturity {maturity}, calculated {calculated}, expected {expected[i]}, "
                    + $"difference {diff:E}, tolerance {relTol * expected[i]:E}");
            }
        }
    }

    [Fact]
    public void TestDividendBarrierOptionWithDividendsPastMaturity()
    {
        // Testing barrier option pricing with discrete dividends past maturity...

        DayCounter dc = new Actual365Fixed();

        var today = new Date(11, February, 2018);
        Date maturity = today + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = today };

        double spot = 100.0;
        double strike = 105.0;
        double rebate = 5.0;

        double[] barriers = [90.0, 110.0];
        BarrierType[] barrierTypes = [BarrierType.DownOut, BarrierType.UpOut];

        double r = 0.05;
        double q = 0.0;
        double v = 0.02;

        var bsProcess = new BlackScholesMertonProcess(
            spot, FlatRate(today, q, dc), FlatRate(today, r, dc), FlatVol(today, v, dc));

        double divAmount = 30;
        Date divDate = today + new Period(18, TimeUnit.Months);
        ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([divDate], [divAmount]);

        var hestonModel = new HestonModel(new HestonProcess(
            FlatRate(today, r, dc), FlatRate(today, q, dc), spot, v * v, 1.0, v * v, 0.005, 0.0));

        Func<BarrierOption, ImmutableArray<Dividend>, double>[] engines =
        [
            (o, d) => FdBlackScholesBarrierEngine.Calculate(o, context, bsProcess, d, 100, 100, 0, FdmSchemeDesc.Douglas()).NPV!.Value,
            (o, d) => FdHestonBarrierEngine.Calculate(o, context, hestonModel, d, 50, 101, 3).NPV!.Value,
        ];

        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        var exercise = new EuropeanExercise(maturity);

        for (int i = 0; i < barriers.Length; ++i)
        {
            for (int j = 0; j < engines.Length; ++j)
            {
                double barrier = barriers[i];
                BarrierType barrierType = barrierTypes[i];

                var barrierOption = new BarrierOption(barrierType, barrier, rebate, payoff, exercise);

                double withoutDividends = engines[j](barrierOption, []);

                double withDividends = engines[j](barrierOption, dividends);

                double diff = Math.Abs(withDividends - withoutDividends);
                double tolerance = 1e-12;
                Assert.True(
                    diff <= tolerance,
                    $"Dividends past maturity affected option price: engine {j}, strike {strike}, barrier {barrier}, "
                    + $"maturity {maturity}, without dividend {withoutDividends}, with dividend {withDividends}, "
                    + $"difference {diff:E}");
            }
        }
    }

    [Fact]
    public void TestImpliedVolatility()
    {
        // Testing implied volatility for barrier options...

        DayCounter dc = new Actual365Fixed();

        var today = new Date(11, February, 2018);
        Date maturity = today + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = today };

        double spot = 100.0;

        double r = 0.05;
        double q = 0.0;

        YieldTermStructure qTS = FlatRate(today, q, dc);
        YieldTermStructure rTS = FlatRate(today, r, dc);
        BlackVolTermStructure dummyVolTS = FlatVol(today, 0.0, dc);

        var bsProcess = new BlackScholesMertonProcess(spot, qTS, rTS, dummyVolTS);

        double divAmount = 10;
        Date divDate = today + new Period(6, TimeUnit.Months);
        ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([divDate], [divAmount]);

        double strike = 105.0;
        double rebate = 5.0;

        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        var exercise = new EuropeanExercise(maturity);

        double[] barriers = [90.0, 110.0, 90.0, 110.0];
        BarrierType[] barrierTypes = [BarrierType.DownOut, BarrierType.UpOut, BarrierType.DownIn, BarrierType.UpIn];
        double[] targetWithoutDividends = [1.0, 1.0, 5.0, 5.0];
        double[] targetWithDividends = [8.0, 12.0, 9.0, 8.0];
        double tolerance = 1e-5;

        for (int i = 0; i < barriers.Length; ++i)
        {
            double barrier = barriers[i];
            BarrierType barrierType = barrierTypes[i];

            var barrierOption = new BarrierOption(barrierType, barrier, rebate, payoff, exercise);
            double impliedVol = barrierOption.ImpliedVolatility(targetWithoutDividends[i], bsProcess, context, 1e-6);

            var process = new BlackScholesMertonProcess(spot, qTS, rTS, FlatVol(today, impliedVol, dc));
            double npv = AnalyticBarrierEngine.Calculate(barrierOption, context, process).NPV!.Value;

            double diff = Math.Abs(npv - targetWithoutDividends[i]);
            Assert.True(
                diff <= tolerance,
                $"Failed to reproduce target option price: strike {strike}, barrier {barrier}, maturity {maturity}, "
                + $"target value {targetWithoutDividends[i]}, calculated {npv}, difference {diff:E}");

            impliedVol = barrierOption.ImpliedVolatility(targetWithDividends[i], bsProcess, dividends, context, 1e-6);

            process = new BlackScholesMertonProcess(spot, qTS, rTS, FlatVol(today, impliedVol, dc));
            npv = FdBlackScholesBarrierEngine.Calculate(barrierOption, context, process, dividends).NPV!.Value;

            diff = Math.Abs(npv - targetWithDividends[i]);
            Assert.True(
                diff <= tolerance,
                $"Failed to reproduce target option price: strike {strike}, barrier {barrier}, maturity {maturity}, "
                + $"target value {targetWithDividends[i]}, calculated {npv}, difference {diff:E}");
        }
    }

    [Fact]
    public void TestLowVolatility()
    {
        // Testing barrier options with low volatility value...

        DayCounter dc = new Actual365Fixed();

        var today = new Date(11, February, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturity = today + new Period(1, TimeUnit.Years);

        double spot = 100.0;
        double vol = 1e-7;

        BlackVolTermStructure volTS = FlatVol(today, vol, dc);

        void Check(
            double strike,
            OptionType optionType,
            double barrier,
            BarrierType barrierType,
            double rebate,
            double r,
            double q,
            double expected)
        {
            var process = new BlackScholesMertonProcess(spot, FlatRate(today, q, dc), FlatRate(today, r, dc), volTS);
            var payoff = new PlainVanillaPayoff(optionType, strike);
            var exercise = new EuropeanExercise(maturity);
            var option = new BarrierOption(barrierType, barrier, rebate, payoff, exercise);
            double value = AnalyticBarrierEngine.Calculate(option, context, process).NPV!.Value;
            double diff = Math.Abs(value - expected);
            Assert.False(
                double.IsNaN(value) || diff > 0.5,
                $"Failed to match expected price: strike {strike}, option type {optionType}, barrier {barrier}, "
                + $"barrier type {barrierType}, rebate {rebate}, risk-free rate {r}, dividend yield {q}, "
                + $"expected {expected}, calculated {value}");
        }

        // strike, type, barrier, barrier type, rebate, r, q, expected

        Check(105.0, OptionType.Put, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 3.0); // fwd = 102, put, ITM, k < H
        Check(109.0, OptionType.Put, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 7.0); // fwd = 102, put, ITM, k > H
        Check(100.0, OptionType.Put, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 0.0); // fwd = 102, put, OTM
        Check(99.0, OptionType.Put, 101.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 4.0); // fwd = 102, knocked out, k < H
        Check(105.0, OptionType.Put, 101.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 4.0); // fwd = 102, knocked out, k > H
        Check(105.0, OptionType.Call, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 0.0); // fwd = 102, call, OTM, k < H
        Check(109.0, OptionType.Call, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 0.0); // fwd = 102, call, OTM, k > H
        Check(100.0, OptionType.Call, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 2.0); // fwd = 102, call, ITM
        Check(105.0, OptionType.Call, 101.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 4.0); // fwd = 102, knocked out, k < H
        Check(99.0, OptionType.Call, 101.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 4.0); // fwd = 102, knocked out, k > H

        Check(105.0, OptionType.Put, 107.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 4.0); // fwd = 103, not triggered, k < H
        Check(109.0, OptionType.Put, 107.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 4.0); // fwd = 103, not triggered, k > H
        Check(105.0, OptionType.Put, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 2.0); // fwd = 103, put, ITM
        Check(100.0, OptionType.Put, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 0.0); // fwd = 103, put, OTM, k < H
        Check(102.0, OptionType.Put, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 0.0); // fwd = 103, put, OTM, k > H
        Check(105.0, OptionType.Call, 107.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 4.0); // fwd = 103, not triggered, k < H
        Check(109.0, OptionType.Call, 107.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 4.0); // fwd = 103, not triggered, k > H
        Check(105.0, OptionType.Call, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 0.0); // fwd = 103, call, OTM
        Check(100.0, OptionType.Call, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 3.0); // fwd = 103, call, ITM, k < H
        Check(102.0, OptionType.Call, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 1.0); // fwd = 103, call, ITM, k > H

        Check(91.0, OptionType.Put, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 0.0); // fwd = 98, put, OTM, k < H
        Check(95.0, OptionType.Put, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 0.0); // fwd = 98, put, OTM, k > H
        Check(100.0, OptionType.Put, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 2.0); // fwd = 98, put, ITM
        Check(97.0, OptionType.Put, 99.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 4.0); // fwd = 98, knocked out, k < H
        Check(101.0, OptionType.Put, 99.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 4.0); // fwd = 98, knocked out, k > H
        Check(91.0, OptionType.Call, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 7.0); // fwd = 98, call, ITM
        Check(95.0, OptionType.Call, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 3.0); // fwd = 98, call, ITM
        Check(100.0, OptionType.Call, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 0.0); // fwd = 98, call, OTM
        Check(95.0, OptionType.Call, 99.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 4.0); // fwd = 98, knocked out, k < H
        Check(101.0, OptionType.Call, 99.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 4.0); // fwd = 98, knocked out, k > H

        Check(91.0, OptionType.Put, 93.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 4.0); // fwd = 97, not triggered, k < H
        Check(95.0, OptionType.Put, 93.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 4.0); // fwd = 97, not triggered, k > H
        Check(100.0, OptionType.Put, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 3.0); // fwd = 97, put, ITM
        Check(95.0, OptionType.Put, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 0.0); // fwd = 97, put, OTM, k < H
        Check(95.0, OptionType.Put, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 0.0); // fwd = 97, put, OTM, k > H
        Check(91.0, OptionType.Call, 93.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 4.0); // fwd = 97, not triggered, k < H
        Check(95.0, OptionType.Call, 93.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 4.0); // fwd = 97, not triggered, k > H
        Check(98.0, OptionType.Call, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 0.0); // fwd = 97, call, OTM, k < H
        Check(100.0, OptionType.Call, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 0.0); // fwd = 97, call, OTM, k > H
        Check(95.0, OptionType.Call, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 2.0); // fwd = 97, call, ITM
    }

    [Fact]
    public void TestPerturbativeOrder0()
    {
        // Testing perturbative barrier engine, order 0...
        Assert.True(Math.Abs(PerturbativeBarrierOptionValue(0) - 0.897365) <= 1.0e-6);
    }

    [Fact]
    public void TestPerturbativeOrder1()
    {
        // Testing perturbative barrier engine, order 1...
        Assert.True(Math.Abs(PerturbativeBarrierOptionValue(1) - 0.894374) <= 1.0e-6);
    }

    // QuantLib's master's cached value (#2935), its own check of the trivariate normal's fix; QuantLib 1.44 cached its
    // defective code's 0.8943769 (Divergences/PerturbativeBarrierEngineTests.cs)
    [Fact]
    public void TestPerturbativeOrder2Fast()
    {
        // Testing perturbative barrier engine, fast order 2...
        Assert.True(Math.Abs(PerturbativeBarrierOptionValue(2, 100, 10) - 0.8943754) <= 1.0e-6);
    }

    // As TestPerturbativeOrder2Fast: #2935's cached value
    [Fact]
    public void TestPerturbativeOrder2FullResolution()
    {
        // Takes about 10-20 seconds on Apple Silicon (2026).
        // Testing perturbative barrier engine, full-resolution order 2...
        Assert.True(Math.Abs(PerturbativeBarrierOptionValue(2) - 0.8943754) <= 1.0e-6);
    }

    [Fact]
    public void TestVannaVolgaSimpleBarrierValues()
    {
        // Testing barrier FX options against Vanna/Volga values...

        BarrierFxOptionData[] values =
        [
            // barrierType, barrier, rebate, type, strike, s, q, r, t, vol25Put, volAtm, vol25Call, vol, result, tol
            new(BarrierType.UpOut, 1.5, 0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.11638, 0.148127, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.10088, 0.075943, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08925, 0.0274771, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08463, 0.00573, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08412, 0.00012, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.11638, 0.00697606, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.10088, 0.020078, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08925, 0.0489395, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08463, 0.0969877, 1.0e-4),
            new(BarrierType.UpOut, 1.5, 0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08412, 0.157, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.11638, 0.0322202, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.10088, 0.0241491, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08925, 0.0164275, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08463, 0.01, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08412, 0.00489, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.11638, 0.000560713, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.10088, 0.000546804, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08925, 0.000130649, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08463, 0.000300828, 1.0e-4),
            new(BarrierType.UpIn, 1.5, 0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08412, 0.00135, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.11638, 0.17746, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.10088, 0.0994142, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08925, 0.0439, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08463, 0.01574, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08412, 0.00501, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.11638, 0.00612, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.10088, 0.00426, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08925, 0.00257, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08463, 0.00122, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08412, 0.00045, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.11638, 0.00022, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.10088, 0.00284, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08925, 0.02032, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08463, 0.058235, 1.0e-4),
            new(BarrierType.DownOut, 1.1, 0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08412, 0.109432, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.11638, 0, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.10088, 0, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08925, 0, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08463, 0.00017, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08412, 0.00083, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.11638, 0.00289, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.10088, 0.00067784, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08925, 0, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08463, 0, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08412, 0, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.11638, 0.17423, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.10088, 0.09584, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08925, 0.04133, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08463, 0.01452, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087,
                0.08925, 0.08463, 0.08412, 0.00456, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.11638, 0.00732, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.10088, 0.01778, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08925, 0.02875, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08463, 0.0390535, 1.0e-4),
            new(BarrierType.DownIn, 1.1, 0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08412, 0.0489236, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.13321, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.11638, 0.00753, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.22687, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.10088, 0.02062, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.31179, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08925, 0.04907, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.38843, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08463, 0.09711, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.46047, 1.30265, 0.0003541, 0.0033871, 1, 0.10087, 0.08925,
                0.08463, 0.08412, 0.15752, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.20493, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.105577, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.0358872, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.00634958, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.0108218, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.0313339, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Put, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.0751237, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Put, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.153407, 1.0e-4),
            new(BarrierType.UpOut, 1.6, 0, OptionType.Put, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.253767, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.05402, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.0410069, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.0279562, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.0173055, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.00764, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.000962737, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.00102637, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Put, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.000419834, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Put, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.00159277, 1.0e-4),
            new(BarrierType.UpIn, 1.6, 0, OptionType.Put, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.00473629, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.255098, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.145701, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.06384, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.02366, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.00764, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.12511, 0.00592, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.1089, 0.00421, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09444, 0.00256, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09197, 0.0012, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09261, 0.0004, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.00280549, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Put, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.0279945, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Put, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.0896352, 1.0e-4),
            new(BarrierType.DownOut, 1, 0, OptionType.Put, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.175182, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.12511, 0.00000, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.1089, 0.00000, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09444, 0.00000, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09197, 0.0002, 1.0e-4),
            new(BarrierType.DownOut, 1.3, 0, OptionType.Put, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09261, 0.00096, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.00384783, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.000883232, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.00000, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.00000, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.12511, 0.25302, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.1089, 0.14238, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09444, 0.06128, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09197, 0.02245, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Call, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891,
                0.09525, 0.09197, 0.09261, 0.00725, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.01178, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.0295548, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Put, 1.32238, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09444, 0.047549, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Put, 1.44298, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09197, 0.0653642, 1.0e-4),
            new(BarrierType.DownIn, 1, 0, OptionType.Put, 1.56345, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.09261, 0.0833221, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.06145, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.12511, 0.01178, 1.0e-4),
            new(BarrierType.DownIn, 1.3, 0, OptionType.Put, 1.19545, 1.30265, 0.0009418, 0.0039788, 2, 0.10891, 0.09525,
                0.09197, 0.1089, 0.03236, 1.0e-4),
        ];

        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, March, 2013);
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (BarrierFxOptionData value in values)
        {
            YieldTermStructure qTS = FlatRate(today, value.Q, dc);
            YieldTermStructure rTS = FlatRate(today, value.R, dc);

            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            Date exDate = today + TimeToDays(value.T, 365);
            Exercise exercise = new EuropeanExercise(exDate);

            var volAtmQuote = new DeltaVolQuote(
                value.VolAtm, DeltaVolQuoteDeltaType.Fwd, value.T, DeltaVolQuoteAtmType.AtmDeltaNeutral);
            var vol25PutQuote = new DeltaVolQuote(-0.25, value.Vol25Put, value.T, DeltaVolQuoteDeltaType.Fwd);
            var vol25CallQuote = new DeltaVolQuote(0.25, value.Vol25Call, value.T, DeltaVolQuoteDeltaType.Fwd);

            var barrierOption = new BarrierOption(value.BarrierType, value.Barrier, value.Rebate, payoff, exercise);

            double bsVanillaPrice = BlackFormulaFunctions.BlackFormula(
                value.Type,
                value.Strike,
                value.S * qTS.Discount(value.T) / rTS.Discount(value.T),
                value.V * Math.Sqrt(value.T),
                rTS.Discount(value.T));
            double calculated = VannaVolgaBarrierEngine.Calculate(
                barrierOption,
                context,
                volAtmQuote,
                vol25PutQuote,
                vol25CallQuote,
                value.S,
                rTS,
                qTS,
                true,
                bsVanillaPrice).NPV!.Value;
            double expected = value.Result;
            double error = Math.Abs(calculated - expected);
            Assert.True(
                error <= value.Tol,
                $"{BarrierTypeFunctions.ToString(value.BarrierType)} {value.Type} FX option: barrier {value.Barrier}, "
                + $"strike {value.Strike}, spot {value.S}, expected {expected}, calculated {calculated}, "
                + $"error {error}, tolerance {value.Tol}");
        }
    }

    // QuantLib's perturbativeBarrierOptionValue: an up-and-out put struck at the barrier on a variance curve.
    private static double PerturbativeBarrierOptionValue(int order, int npoint = 1000, int npoint2 = 100)
    {
        double s = 100.0;
        double rebate = 0.0;
        double r = 0.03;
        double q = 0.02;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        YieldTermStructure qTS = FlatRate(today, q, dc);
        YieldTermStructure rTS = FlatRate(today, r, dc);

        Date[] dates = [today + 90, today + 180];
        double[] vols = [0.105, 0.11];

        var volTS = new BlackVarianceCurve(today, dates, vols, dc);

        var stochProcess = new BlackScholesMertonProcess(s, qTS, rTS, volTS);

        double strike = 101.0;
        double barrier = 101.0;
        Date exDate = today + 180;

        var exercise = new EuropeanExercise(exDate);
        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);

        var option = new BarrierOption(BarrierType.UpOut, barrier, rebate, payoff, exercise);

        const bool zeroGamma = false;
        return PerturbativeBarrierOptionEngine.Calculate(
            option, RunnerContext, stochProcess, order, zeroGamma, npoint, npoint2).NPV!.Value;
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

    // QL_ASSERT_EXCEPTION_THROWN: the statement throws (QuantLib catches std::exception; nanoql's refusals are
    // ArgumentException and NanoQLException).
    private static void AssertThrown(Action statement)
    {
        Exception? e = Record.Exception(statement);
        Assert.True(
            e is ArgumentException or NanoQLException, $"exception expected, got {e?.GetType().Name ?? "none"}");
    }

    private static string Failure(
        string greekName, NewBarrierOptionData value, Date today, double expected, double calculated, double error,
        double tolerance) =>
        $"{BarrierTypeFunctions.ToString(value.BarrierType)} {value.ExType} {value.Type} option: underlying value "
        + $"{value.S}, strike {value.Strike}, barrier {value.Barrier}, rebate {value.Rebate}, dividend yield "
        + $"{value.Q}, risk-free rate {value.R}, reference date {today}, volatility {value.V}: expected {greekName} "
        + $"{expected}, calculated {greekName} {calculated}, error {error}, tolerance {tolerance}";

    private sealed record BarrierOptionData(
        BarrierType Type, double Volatility, double Strike, double Barrier, double CallValue, double PutValue);

    private sealed record NewBarrierOptionData(
        BarrierType BarrierType,
        double Barrier,
        double Rebate,
        OptionType Type,
        ExerciseType ExType,
        double Strike,
        double S,
        double Q,
        double R,
        double T,
        double V,
        double Result,
        double Tol);

    private sealed record BarrierFxOptionData(
        BarrierType BarrierType,
        double Barrier,
        double Rebate,
        OptionType Type,
        double Strike,
        double S,
        double Q,
        double R,
        double T,
        double Vol25Put,
        double VolAtm,
        double Vol25Call,
        double V,
        double Result,
        double Tol);
}
