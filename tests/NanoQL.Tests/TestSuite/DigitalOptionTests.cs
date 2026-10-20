// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/digitaloption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCashAtHitOrNothingAmericanValues, testAssetAtHitOrNothingAmericanValues, testCashAtExpiryOrNothingAmericanValues, testAssetAtExpiryOrNothingAmericanValues, testCashAtHitOrNothingAmericanGreeks and testCashAtHitOrNothingAmericanLowVolatility, which wait for B4.5's AnalyticDigitalAmericanEngine; testMCCashAtHit, which waits for B4.4's MCDigitalEngine

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class DigitalOptionTests
{
    // The test runner's evaluation date.
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    [Fact]
    public void TestCashOrNothingEuropeanValues()
    {
        // Testing European cash-or-nothing digital option...

        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 88
        //        type, strike,  spot,    q,    r,    t,  vol,  value, tol
        CheckValue(new CashOrNothingPayoff(OptionType.Put, 80.00, 10.0), 100.0, 0.06, 0.06, 0.75, 0.35, 2.6710, 1e-4);
    }

    [Fact]
    public void TestAssetOrNothingEuropeanValues()
    {
        // Testing European asset-or-nothing digital option...

        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 90
        //        type, strike, spot,    q,    r,    t,  vol,   value, tol
        CheckValue(new AssetOrNothingPayoff(OptionType.Put, 65.00), 70.0, 0.05, 0.07, 0.50, 0.27, 20.2069, 1e-4);
    }

    [Fact]
    public void TestGapEuropeanValues()
    {
        // Testing European gap digital option...

        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 88
        //        type, strike, spot,    q,    r,    t,  vol,   value, tol
        CheckValue(new GapPayoff(OptionType.Call, 50.00, 57.00), 50.0, 0.00, 0.09, 0.50, 0.20, -0.0053, 1e-4);
    }

    // The body each European case runs on its one value.
    private static void CheckValue(
        StrikedTypePayoff payoff, Real s, Rate q, Rate r, double t, Volatility v, Real result, Real tol)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Date exDate = today + (Integer)Math.Round(t * 360, MidpointRounding.AwayFromZero);
        Exercise exercise = new EuropeanExercise(exDate);

        var stochProcess = new BlackScholesMertonProcess(
            s, new FlatForward(today, q, dc), new FlatForward(today, r, dc), new BlackConstantVol(today, new NullCalendar(), v, dc));

        var opt = new VanillaOption(payoff, exercise);

        Real calculated = AnalyticEuropeanEngine.Calculate(opt, RunnerContext, stochProcess).NPV!.Value;
        Real error = Math.Abs(calculated - result);
        if (error > tol)
        {
            Assert.Fail(
                $"European {payoff.OptionType} option with {payoff.Name} payoff: expected value {result}, calculated "
                + $"{calculated}, error {error}, tolerance {tol}");
        }
    }
}
