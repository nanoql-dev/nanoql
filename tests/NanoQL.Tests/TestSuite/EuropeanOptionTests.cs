// SPDX-FileCopyrightText: Copyright (C) 2003, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/europeanoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testIntegralEngines, which waits for B4.5's IntegralEngine; testMcEngines and testQmcEngines, which wait for B4.4's MCEuropeanEngine; testFFTEngines, which waits for B7.6's FFTVanillaEngine; testFdEnginesWithNonStrikedPayoff, which waits for B4.3's FdHestonVanillaEngine and FdCIRVanillaEngine

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class EuropeanOptionTests
{
    // The test runner's evaluation date, which the cases that don't set one price under.
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    [Fact]
    public void TestGreekValues()
    {
        // Testing European option greek values...

        /* The data below are from
           "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
           pag 11-16
        */
        (OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Real Result)[] values =
        [
            //        type, strike,   spot,    q,    r,        t,  vol,  value
            // delta
            (OptionType.Call, 100.00, 105.00, 0.10, 0.10, 0.500000, 0.36, 0.5946),
            (OptionType.Put, 100.00, 105.00, 0.10, 0.10, 0.500000, 0.36, -0.3566),

            // elasticity
            (OptionType.Put, 100.00, 105.00, 0.10, 0.10, 0.500000, 0.36, -4.8775),

            // gamma
            (OptionType.Call, 60.00, 55.00, 0.00, 0.10, 0.750000, 0.30, 0.0278),
            (OptionType.Put, 60.00, 55.00, 0.00, 0.10, 0.750000, 0.30, 0.0278),

            // vega
            (OptionType.Call, 60.00, 55.00, 0.00, 0.10, 0.750000, 0.30, 18.9358),
            (OptionType.Put, 60.00, 55.00, 0.00, 0.10, 0.750000, 0.30, 18.9358),

            // theta
            (OptionType.Put, 405.00, 430.00, 0.05, 0.07, 1.0 / 12.0, 0.20, -31.1924),

            // theta per day
            (OptionType.Put, 405.00, 430.00, 0.05, 0.07, 1.0 / 12.0, 0.20, -0.0855),

            // rho
            (OptionType.Call, 75.00, 72.00, 0.00, 0.09, 1.000000, 0.19, 38.7325),

            // dividendRho
            (OptionType.Put, 490.00, 500.00, 0.05, 0.08, 0.250000, 0.15, 42.2254),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Func<OneAssetOptionResults, double?>[] greeks =
        [
            r => r.Delta, r => r.Delta, r => r.Elasticity, r => r.Gamma, r => r.Gamma, r => r.Vega, r => r.Vega,
            r => r.Theta, r => r.ThetaPerDay, r => r.Rho, r => r.DividendRho,
        ];
        string[] names = ["delta", "delta", "elasticity", "gamma", "gamma", "vega", "vega", "theta", "thetaPerDay", "rho", "dividendRho"];
        Real tolerance = 1e-4;

        for (int i = 0; i < values.Length; i++)
        {
            StrikedTypePayoff payoff = new PlainVanillaPayoff(values[i].Type, values[i].Strike);
            Date exDate = today + TimeToDays(values[i].T);
            Exercise exercise = new EuropeanExercise(exDate);
            var stochProcess = new BlackScholesMertonProcess(
                values[i].S,
                FlatRate(today, values[i].Q, dc),
                FlatRate(today, values[i].R, dc),
                FlatVol(today, values[i].V, dc));
            VanillaOption option = new EuropeanOption(payoff, exercise);
            Real calculated = greeks[i](AnalyticEuropeanEngine.Calculate(option, RunnerContext, stochProcess))!.Value;
            Real error = Math.Abs(calculated - values[i].Result);
            if (error > tolerance)
            {
                Assert.Fail(
                    $"{values[i].Type} option: {names[i]} expected {values[i].Result}, calculated {calculated}, "
                    + $"error {error}, tolerance {tolerance}");
            }
        }
    }

    // Adapted: the quotes changed in place (spot, rates, volatility and the evaluation date) become processes and
    // curves built for each value; the curves are dated by the context, as QuantLib's flatRate(quote, dc) is by the
    // evaluation date.
    [Fact]
    public void TestGreeks()
    {
        // Testing analytic European option greeks...

        Dictionary<string, Real> calculated = [], expected = [], tolerance = [];
        tolerance["delta"] = 1.0e-5;
        tolerance["gamma"] = 1.0e-5;
        tolerance["theta"] = 1.0e-5;
        tolerance["rho"] = 1.0e-5;
        tolerance["divRho"] = 1.0e-5;
        tolerance["vega"] = 1.0e-5;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.04, 0.05, 0.06];
        Rate[] rRates = [0.01, 0.05, 0.15];
        double[] residualTimes = [1.0, 2.0];
        Volatility[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        StrikedTypePayoff payoff = null!;

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (double residualTime in residualTimes)
                {
                    Date exDate = today + TimeToDays(residualTime);
                    Exercise exercise = new EuropeanExercise(exDate);
                    for (int kk = 0; kk < 4; kk++)
                    {
                        // option to check
                        if (kk == 0)
                        {
                            payoff = new PlainVanillaPayoff(type, strike);
                        }
                        else if (kk == 1)
                        {
                            payoff = new CashOrNothingPayoff(type, strike, 100.0);
                        }
                        else if (kk == 2)
                        {
                            payoff = new AssetOrNothingPayoff(type, strike);
                        }
                        else if (kk == 3)
                        {
                            payoff = new GapPayoff(type, strike, 100.0);
                        }

                        var option = new EuropeanOption(payoff, exercise);

                        foreach (Real u in underlyings)
                        {
                            foreach (Real m in qRates)
                            {
                                foreach (Real n in rRates)
                                {
                                    foreach (Real v in vols)
                                    {
                                        Rate q = m, r = n;
                                        OneAssetOptionResults results = Price(option, u, q, r, v, dc, context);
                                        Real value = results.NPV!.Value;
                                        calculated["delta"] = results.Delta!.Value;
                                        calculated["gamma"] = results.Gamma!.Value;
                                        calculated["theta"] = results.Theta!.Value;
                                        calculated["rho"] = results.Rho!.Value;
                                        calculated["divRho"] = results.DividendRho!.Value;
                                        calculated["vega"] = results.Vega!.Value;

                                        if (value > u * 1.0e-5)
                                        {
                                            // perturb spot and get delta and gamma
                                            Real du = u * 1.0e-4;
                                            OneAssetOptionResults p = Price(option, u + du, q, r, v, dc, context);
                                            OneAssetOptionResults mm = Price(option, u - du, q, r, v, dc, context);
                                            expected["delta"] = (p.NPV!.Value - mm.NPV!.Value) / (2 * du);
                                            expected["gamma"] = (p.Delta!.Value - mm.Delta!.Value) / (2 * du);

                                            // perturb rates and get rho and dividend rho
                                            Spread dr = r * 1.0e-4;
                                            Real valueP = Price(option, u, q, r + dr, v, dc, context).NPV!.Value;
                                            Real valueM = Price(option, u, q, r - dr, v, dc, context).NPV!.Value;
                                            expected["rho"] = (valueP - valueM) / (2 * dr);

                                            Spread dq = q * 1.0e-4;
                                            valueP = Price(option, u, q + dq, r, v, dc, context).NPV!.Value;
                                            valueM = Price(option, u, q - dq, r, v, dc, context).NPV!.Value;
                                            expected["divRho"] = (valueP - valueM) / (2 * dq);

                                            // perturb volatility and get vega
                                            Volatility dv = v * 1.0e-4;
                                            valueP = Price(option, u, q, r, v + dv, dc, context).NPV!.Value;
                                            valueM = Price(option, u, q, r, v - dv, dc, context).NPV!.Value;
                                            expected["vega"] = (valueP - valueM) / (2 * dv);

                                            // perturb date and get theta
                                            double dT = dc.YearFraction(today - 1, today + 1);
                                            valueM = Price(option, u, q, r, v, dc, context with { EvaluationDate = today - 1 }).NPV!.Value;
                                            valueP = Price(option, u, q, r, v, dc, context with { EvaluationDate = today + 1 }).NPV!.Value;
                                            expected["theta"] = (valueP - valueM) / dT;

                                            // compare
                                            foreach ((string greek, Real calcl) in calculated)
                                            {
                                                Real expct = expected[greek], tol = tolerance[greek];
                                                Real error = RelativeError(expct, calcl, u);
                                                if (error > tol)
                                                {
                                                    Assert.Fail(
                                                        $"{type} option with {payoff.Name} payoff, strike {strike}, "
                                                        + $"spot {u}, q {q}, r {r}, vol {v}: {greek} expected {expct}, "
                                                        + $"calculated {calcl}, error {error}, tolerance {tol}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static OneAssetOptionResults Price(
            EuropeanOption option, Real u, Rate q, Rate r, Volatility v, DayCounter dc, EvaluationContext context)
        {
            var stochProcess = new BlackScholesMertonProcess(
                u, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(v, dc, context));
            return AnalyticEuropeanEngine.Calculate(option, context, stochProcess);
        }
    }

    // Adapted: the quotes changed in place become processes built for each value.
    [Fact]
    public void TestImpliedVol()
    {
        // Testing European option implied volatility...

        int maxEvaluations = 100;
        Real tolerance = 1.0e-6;

        // test options
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [90.0, 99.5, 100.0, 100.5, 110.0];
        Integer[] lengths = [36, 180, 360, 1080];

        // test data
        Real[] underlyings = [90.0, 95.0, 99.9, 100.0, 100.1, 105.0, 110.0];
        Rate[] qRates = [0.01, 0.05, 0.10];
        Rate[] rRates = [0.01, 0.05, 0.10];
        Volatility[] vols = [0.01, 0.20, 0.30, 0.70, 0.90];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    // option to check
                    Date exDate = today + length;
                    Exercise exercise = new EuropeanExercise(exDate);
                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                    VanillaOption option = new EuropeanOption(payoff, exercise);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    CheckImpliedVol(option, today, dc, u, q, r, v, [], tolerance, maxEvaluations);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: the quotes changed in place become processes built for each value.
    [Fact]
    public void TestImpliedVolWithDividends()
    {
        // Testing European option implied volatility with dividends...

        int maxEvaluations = 100;
        Real tolerance = 1.0e-6;

        // test options
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [90.0, 99.5, 100.0, 100.5, 110.0];
        Integer[] lengths = [36, 180, 360, 1080];

        // test data
        Real[] underlyings = [90.0, 95.0, 99.9, 100.0, 100.1, 105.0, 110.0];
        Rate[] qRates = [0.01, 0.05, 0.10];
        Rate[] rRates = [0.01, 0.05, 0.10];
        Volatility[] vols = [0.01, 0.20, 0.30, 0.70, 0.90];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    // option to check
                    Date exDate = today + length;
                    Exercise exercise = new EuropeanExercise(exDate);
                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                    ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([today + (length / 2)], [1.0]);
                    VanillaOption option = new EuropeanOption(payoff, exercise);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    CheckImpliedVol(option, today, dc, u, q, r, v, dividends, tolerance, maxEvaluations);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: QuantLib's check that the implied-volatility calculation notifies no observer of the other option
    // (lines 1103-1110, 1121-1124) has no counterpart, since nanoql has no observers; its two value checks are kept,
    // the volatility's change a process built on the changed volatility.
    [Fact]
    public void TestImpliedVolContainment()
    {
        // Testing self-containment of implied volatility calculation...

        int maxEvaluations = 100;
        Real tolerance = 1.0e-6;

        // test options

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Real spot = 100.0;
        YieldTermStructure qTS = FlatRate(today, 0.05, dc);
        YieldTermStructure rTS = FlatRate(today, 0.03, dc);
        Volatility vol = 0.20;

        Date exerciseDate = today + (1 * TimeUnit.Years);
        Exercise exercise = new EuropeanExercise(exerciseDate);
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);

        var process = new BlackScholesMertonProcess(spot, qTS, rTS, FlatVol(today, vol, dc));

        // link to the same stochastic process, which shouldn't be changed
        // by calling methods of either option
        VanillaOption option1 = new EuropeanOption(payoff, exercise);
        VanillaOption option2 = new EuropeanOption(payoff, exercise);

        // test
        Real refValue = AnalyticEuropeanEngine.Calculate(option2, RunnerContext, process).NPV!.Value;

        option1.ImpliedVolatility(refValue * 1.5, process, RunnerContext, tolerance, maxEvaluations);

        Real value2 = AnalyticEuropeanEngine.Calculate(option2, RunnerContext, process).NPV!.Value;
        if (Math.Abs(value2 - refValue) >= 1.0e-8)
        {
            Assert.Fail(
                "implied volatility calculation changed the value of another instrument: \n"
                + $"previous value: {refValue}\ncurrent value:  {value2}");
        }

        var changed = new BlackScholesMertonProcess(spot, qTS, rTS, FlatVol(today, vol * 1.5, dc));
        if (Math.Abs(AnalyticEuropeanEngine.Calculate(option2, RunnerContext, changed).NPV!.Value - refValue) <= 1.0e-8)
        {
            Assert.Fail("volatility change did not cause the value to change");
        }
    }

    // Adapted: the discount rate's quote changed in place (line 1579) becomes a second discount curve.
    [Fact]
    public void TestAnalyticEngineDiscountCurve()
    {
        // Testing separate discount curve for analytic European engine...

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Real spot = 1000.0;
        YieldTermStructure qTS = FlatRate(today, 0.01, dc);
        YieldTermStructure rTS = FlatRate(today, 0.015, dc);
        BlackVolTermStructure volTS = FlatVol(today, 0.02, dc);
        YieldTermStructure discTS = FlatRate(today, 0.015, dc);

        var stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 1025.0);
        Date exDate = today + new Period(1, TimeUnit.Years);
        Exercise exercise = new EuropeanExercise(exDate);
        var option = new EuropeanOption(payoff, exercise);
        Real npvSingleCurve = AnalyticEuropeanEngine.Calculate(option, RunnerContext, stochProcess).NPV!.Value;
        Real npvMultiCurve = AnalyticEuropeanEngine.Calculate(option, RunnerContext, stochProcess, discTS).NPV!.Value;

        // check that NPV is the same regardless of engine interface
        Assert.Equal(npvSingleCurve, npvMultiCurve);

        // check that NPV changes if discount rate is changed
        npvMultiCurve = AnalyticEuropeanEngine.Calculate(
            option, RunnerContext, stochProcess, FlatRate(today, 0.023, dc)).NPV!.Value;
        Assert.NotEqual(npvSingleCurve, npvMultiCurve);
    }

    // Adapted: the quotes changed in place become a process built for each row.
    [Fact]
    public void TestValues()
    {
        // Testing European option values...

        // The data below are from
        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
        EuropeanOptionData[] values =
        [
            // pag 2-8
            //        type, strike,   spot,    q,    r,    t,  vol,   value,    tol
            new(OptionType.Call, 65.00, 60.00, 0.00, 0.08, 0.25, 0.30, 2.1334, 1.0e-4),
            new(OptionType.Put, 95.00, 100.00, 0.05, 0.10, 0.50, 0.20, 2.4648, 1.0e-4),
            new(OptionType.Put, 19.00, 19.00, 0.10, 0.10, 0.75, 0.28, 1.7011, 1.0e-4),
            new(OptionType.Call, 19.00, 19.00, 0.10, 0.10, 0.75, 0.28, 1.7011, 1.0e-4),
            new(OptionType.Call, 1.60, 1.56, 0.08, 0.06, 0.50, 0.12, 0.0291, 1.0e-4),
            new(OptionType.Put, 70.00, 75.00, 0.05, 0.10, 0.50, 0.35, 4.0870, 1.0e-4),

            // pag 24
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.15, 0.0205, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.15, 1.8734, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.15, 9.9413, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.25, 0.3150, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.25, 3.1217, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.25, 10.3556, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.35, 0.9474, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.35, 4.3693, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.35, 11.1381, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.15, 0.8069, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.15, 4.0232, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.15, 10.5769, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.25, 2.7026, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.25, 6.6997, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.25, 12.7857, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.35, 4.9329, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.35, 9.3679, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.35, 15.3086, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.15, 9.9210, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.15, 1.8734, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.15, 0.0408, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.25, 10.2155, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.25, 3.1217, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.25, 0.4551, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.35, 10.8479, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.35, 4.3693, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.35, 1.2376, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.15, 10.3192, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.15, 4.0232, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.15, 1.0646, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.25, 12.2149, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.25, 6.6997, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.25, 3.2734, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.35, 14.4452, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.35, 9.3679, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.35, 5.7963, 1.0e-4),

            // pag 27
            new(OptionType.Call, 40.00, 42.00, 0.08, 0.04, 0.75, 0.35, 5.0975, 1.0e-4),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (EuropeanOptionData value in values)
        {
            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);
            Date exDate = today + TimeToDays(value.T);
            Exercise exercise = new EuropeanExercise(exDate);

            var stochProcess = new BlackScholesMertonProcess(
                value.S,
                FlatRate(today, value.Q, dc),
                FlatRate(today, value.R, dc),
                FlatVol(today, value.V, dc));

            var option = new EuropeanOption(payoff, exercise);

            Real calculated = AnalyticEuropeanEngine.Calculate(option, context, stochProcess).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            Real tolerance = value.Tol;
            if (error > tolerance)
            {
                Assert.Fail(
                    $"European {value.Type} option: spot {value.S}, strike {value.Strike}: expected value "
                    + $"{value.Result}, calculated {calculated}, error {error}, tolerance {tolerance}");
            }

            calculated = FdBlackScholesVanillaEngine.Calculate(
                option, context, stochProcess, tGrid: 200, xGrid: 400).NPV!.Value;
            error = Math.Abs(calculated - value.Result);
            tolerance = 1.0e-3;
            if (error > tolerance)
            {
                Assert.Fail(
                    $"European {value.Type} option, finite differences: spot {value.S}, strike {value.Strike}: "
                    + $"expected value {value.Result}, calculated {calculated}, error {error}, tolerance {tolerance}");
            }
        }
    }

    [Fact]
    public void TestJRBinomialEngines()
    {
        // Testing JR binomial European engines against analytic results...

        EngineType engine = EngineType.JR;
        Size steps = 251;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 0.002;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestCRRBinomialEngines()
    {
        // Testing CRR binomial European engines against analytic results...

        EngineType engine = EngineType.CRR;
        Size steps = 501;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 0.02;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestEQPBinomialEngines()
    {
        // Testing EQP binomial European engines against analytic results...

        EngineType engine = EngineType.EQP;
        Size steps = 501;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 0.02;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestTGEOBinomialEngines()
    {
        // Testing TGEO binomial European engines against analytic results...

        EngineType engine = EngineType.TGEO;
        Size steps = 251;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 0.002;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestTIANBinomialEngines()
    {
        // Testing TIAN binomial European engines against analytic results...

        EngineType engine = EngineType.TIAN;
        Size steps = 251;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 0.002;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestLRBinomialEngines()
    {
        // Testing LR binomial European engines against analytic results...

        EngineType engine = EngineType.LR;
        Size steps = 251;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 1.0e-6;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestJOSHIBinomialEngines()
    {
        // Testing Joshi binomial European engines against analytic results...

        EngineType engine = EngineType.JOSHI;
        Size steps = 251;
        Size samples = 0; // QuantLib's Null<Size>(), which a binomial engine doesn't read
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 1.0e-7;
        relativeTol["delta"] = 1.0e-3;
        relativeTol["gamma"] = 1.0e-4;
        relativeTol["theta"] = 0.03;
        TestEngineConsistency(engine, steps, samples, relativeTol, true);
    }

    [Fact]
    public void TestFarOutOfTheMoneyBinomialEngines()
    {
        // Testing binomial European engines far out of the money...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(15, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        Rate qRate = 0.01;
        Rate rRate = 0.03;
        Volatility vol = 0.15;

        GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(
            spot, FlatRate(today, qRate, dc), FlatRate(today, rRate, dc), FlatVol(today, vol, dc));

        var payoff = new PlainVanillaPayoff(OptionType.Put, 500.0);
        Exercise exercise = new EuropeanExercise(today + new Period(1, TimeUnit.Days));

        var option = new EuropeanOption(payoff, exercise);
        Size steps = 801;

        Real expected = AnalyticEuropeanEngine.Calculate(option, context, stochProcess).NPV!.Value;

        Real calculated = BinomialVanillaEngine.Calculate<LeisenReimer>(option, context, stochProcess, steps).NPV!.Value;
        Real tolerance = 1.0e-6;
        if (Math.Abs(calculated - expected) > tolerance * expected)
        {
            Assert.Fail(
                $"value: expected {expected}, calculated {calculated}, error {Math.Abs(calculated - expected)}, "
                + $"tolerance {tolerance}");
        }

        // the Joshi up probability is an asymptotic series, and diverges this
        // far from the money
        var e = Assert.Throws<NanoQLException>(
            () => BinomialVanillaEngine.Calculate<Joshi4>(option, context, stochProcess, steps));
        Assert.Contains("invalid up probability", e.Message, StringComparison.Ordinal);

        // the other direction rounds the Leisen-Reimer up probability to one,
        // leaving the tree without a down step
        var deepPayoff = new PlainVanillaPayoff(OptionType.Call, 20.0);
        var deepOption = new EuropeanOption(deepPayoff, exercise);
        e = Assert.Throws<NanoQLException>(
            () => BinomialVanillaEngine.Calculate<LeisenReimer>(deepOption, context, stochProcess, steps));
        Assert.Contains("invalid up probability", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestLargeDriftBinomialEngines()
    {
        // Testing binomial European engines with a large drift per step...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(15, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        Rate qRate = 0.0;
        Rate rRate = 0.10;
        Volatility vol = 0.02;

        GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(
            spot, FlatRate(today, qRate, dc), FlatRate(today, rRate, dc), FlatVol(today, vol, dc));

        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        Exercise exercise = new EuropeanExercise(today + new Period(1, TimeUnit.Years));

        var option = new EuropeanOption(payoff, exercise);

        Real expected = AnalyticEuropeanEngine.Calculate(option, context, stochProcess).NPV!.Value;

        // at ten steps 4*variance is below 3*drift^2, so the equal-probability
        // up step has no real solution
        var e = Assert.Throws<NanoQLException>(
            () => BinomialVanillaEngine.Calculate<AdditiveEQPBinomialTree>(option, context, stochProcess, 10));
        Assert.Contains("more steps are needed", e.Message, StringComparison.Ordinal);

        Real calculated =
            BinomialVanillaEngine.Calculate<AdditiveEQPBinomialTree>(option, context, stochProcess, 501).NPV!.Value;
        Real tolerance = 0.02;
        if (Math.Abs(calculated - expected) > tolerance * expected)
        {
            Assert.Fail(
                $"value: expected {expected}, calculated {calculated}, error {Math.Abs(calculated - expected)}, "
                + $"tolerance {tolerance}");
        }
    }

    [Fact]
    public void TestFdEngines()
    {
        // Testing finite-difference European engines against analytic results...

        EngineType engine = EngineType.FiniteDifferences;
        Size timeSteps = 500;
        Size gridPoints = 500;
        Dictionary<string, Real> relativeTol = [];
        relativeTol["value"] = 1.0e-4;
        relativeTol["delta"] = 1.0e-6;
        relativeTol["gamma"] = 1.0e-6;
        relativeTol["theta"] = 1.0e-3;
        TestEngineConsistency(engine, timeSteps, gridPoints, relativeTol, true);
    }

    [Fact]
    public void TestLocalVolatility()
    {
        // Testing finite-differences with local volatility...

        var settlementDate = new Date(5, Month.July, 2002);
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
        GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(s0, qTS, rTS, volTS);

        (FdmSchemeDesc Desc, string Name)[] schemeDescs =
        [
            (FdmSchemeDesc.Douglas(), "Douglas"),
            (FdmSchemeDesc.CrankNicolson(), "Crank-Nicolson"),
            (FdmSchemeDesc.ModifiedCraigSneyd(), "Mod. Craig-Sneyd"),
        ];

        for (Size i = 2; i < dates.Count; i += 2)
        {
            for (Size j = 3; j < strikes.Length - 5; j += 5)
            {
                Date exDate = dates[i];
                StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, strikes[j]);

                Exercise exercise = new EuropeanExercise(exDate);

                var option = new EuropeanOption(payoff, exercise);
                OneAssetOptionResults analytic = AnalyticEuropeanEngine.Calculate(option, context, process);

                Real tol = 0.001;
                Real expectedNPV = analytic.NPV!.Value;
                Real expectedDelta = analytic.Delta!.Value;
                Real expectedGamma = analytic.Gamma!.Value;

                OneAssetOptionResults fd =
                    FdBlackScholesVanillaEngine.Calculate(option, context, process, tGrid: 200, xGrid: 400);

                Real calculatedNPV = fd.NPV!.Value;
                Real calculatedDelta = fd.Delta!.Value;
                Real calculatedGamma = fd.Gamma!.Value;

                // check implied pricing first
                if (Math.Abs(expectedNPV - calculatedNPV) > tol * expectedNPV)
                {
                    Assert.Fail(
                        $"Failed to reproduce option price for \n    strike:     {payoff.Strike}\n    maturity:   {exDate}"
                        + $"\n    calculated: {calculatedNPV}\n    expected:   {expectedNPV}");
                }

                if (Math.Abs(expectedDelta - calculatedDelta) > tol * expectedDelta)
                {
                    Assert.Fail(
                        $"Failed to reproduce option delta for \n    strike:     {payoff.Strike}\n    maturity:   {exDate}"
                        + $"\n    calculated: {calculatedDelta}\n    expected:   {expectedDelta}");
                }

                if (Math.Abs(expectedGamma - calculatedGamma) > tol * expectedGamma)
                {
                    Assert.Fail(
                        $"Failed to reproduce option gamma for \n    strike:     {payoff.Strike}\n    maturity:   {exDate}"
                        + $"\n    calculated: {calculatedGamma}\n    expected:   {expectedGamma}");
                }

                // check local vol pricing
                // delta/gamma are not the same by definition (model implied greeks)
                foreach ((FdmSchemeDesc desc, string name) in schemeDescs)
                {
                    calculatedNPV = FdBlackScholesVanillaEngine.Calculate(
                        option,
                        context,
                        process,
                        tGrid: 25,
                        xGrid: 100,
                        dampingSteps: 0,
                        schemeDesc: desc,
                        localVol: true,
                        illegalLocalVolOverwrite: 0.35).NPV!.Value;
                    if (Math.Abs(expectedNPV - calculatedNPV) > tol * expectedNPV)
                    {
                        Assert.Fail(
                            $"Failed to reproduce local vol option price for \n    strike:     {payoff.Strike}"
                            + $"\n    maturity:   {exDate}\n    calculated: {calculatedNPV}"
                            + $"\n    expected:   {expectedNPV}\n    scheme:     {name}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestPDESchemes()
    {
        // Testing different PDE schemes to solve Black-Scholes PDEs...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(18, Month.February, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = FlatRate(today, 0.06, dc);
        YieldTermStructure rTS = FlatRate(today, 0.10, dc);
        BlackVolTermStructure volTS = FlatVol(today, 0.35, dc);

        Date maturity = today + new Period(6, TimeUnit.Months);

        var process = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        // Crank-Nicolson and Douglas scheme are the same in one dimension
        (int TGrid, FdmSchemeDesc Desc, string Name)[] engines =
        [
            (15, FdmSchemeDesc.Douglas(), "Douglas"),
            (15, FdmSchemeDesc.CrankNicolson(), "Crank-Nicolson"),
            (500, FdmSchemeDesc.ImplicitEuler(), "Implicit-Euler"),
            (1000, FdmSchemeDesc.ExplicitEuler(), "Explicit-Euler"),
            (1, FdmSchemeDesc.MethodOfLines(), "Method-of-Lines"),
            (10, FdmSchemeDesc.Hundsdorfer(), "Hundsdorfer"),
            (10, FdmSchemeDesc.CraigSneyd(), "Craig-Sneyd"),
            (15, FdmSchemeDesc.ModifiedCraigSneyd(), "Modified Craig-Sneyd"),
            (15, FdmSchemeDesc.TrBDF2(), "TR-BDF2"),
        ];

        var payoff = new PlainVanillaPayoff(OptionType.Put, spot);

        Exercise exercise = new EuropeanExercise(maturity);

        var option = new VanillaOption(payoff, exercise);

        Real expected = AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value;

        Real tol = 0.006;
        foreach ((int tGrid, FdmSchemeDesc desc, string name) in engines)
        {
            Real calculated = FdBlackScholesVanillaEngine.Calculate(
                option, context, process, tGrid: tGrid, xGrid: 100, dampingSteps: 0, schemeDesc: desc).NPV!.Value;

            Real diff = Math.Abs(expected - calculated);

            if (diff > tol)
            {
                Assert.Fail(
                    $"Failed to reproduce European option values with the {name} PDE scheme"
                    + $"\n    calculated: {calculated}\n    expected:   {expected}\n    difference: {diff}"
                    + $"\n    tolerance:  {tol}");
            }
        }
    }

    [Fact]
    public void TestFdEngineWithNonConstantParameters()
    {
        // Testing finite-difference European engine with non-constant parameters...

        Real u = 190.0;
        Volatility v = 0.20;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        BlackVolTermStructure volTS = FlatVol(today, v, dc);

        var dates = new Date[5];
        var rates = new Rate[5];
        dates[0] = today;
        rates[0] = 0.0;
        dates[1] = today + 90;
        rates[1] = 0.001;
        dates[2] = today + 180;
        rates[2] = 0.002;
        dates[3] = today + 270;
        rates[3] = 0.005;
        dates[4] = today + 360;
        rates[4] = 0.01;
        YieldTermStructure rTS = new ForwardCurve(dates, rates, dc);
        Rate r = rTS.ZeroRate(dates[4], dc, Compounding.Continuous).Rate;

        BlackScholesProcess process = BlackScholesProcess.Create(u, rTS, volTS, context);

        Exercise exercise = new EuropeanExercise(today + 360);
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 190.0);

        var option = new EuropeanOption(payoff, exercise);

        Real expected = AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value;

        Size timeSteps = 200;
        Size gridPoints = 201;
        Real calculated = FdBlackScholesVanillaEngine.Calculate(
            option, context, process, tGrid: timeSteps, xGrid: gridPoints).NPV!.Value;

        Real tolerance = 0.01;
        Real error = Math.Abs(expected - calculated);
        if (error > tolerance)
        {
            Assert.Fail(
                $"European {payoff.OptionType} option: spot {u}, r {r}, vol {v}: expected value {expected}, "
                + $"calculated {calculated}, error {error}, tolerance {tolerance}");
        }
    }

    [Fact]
    public void TestDouglasVsCrankNicolson()
    {
        // Testing Douglas vs Crank-Nicolson scheme for finite-difference European PDE engines...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, Month.October, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = FlatRate(today, 0.02, dc);
        YieldTermStructure rTS = FlatRate(today, 0.075, dc);
        BlackVolTermStructure volTS = FlatVol(today, 0.25, dc);

        var process = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Put, spot + 2),
            new EuropeanExercise(today + new Period(6, TimeUnit.Months)));

        Real npv = AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value;
        Real schemeTol = 1e-12;
        Real npvTol = 1e-2;

        for (Real theta = 0.2; theta < 0.81; theta += 0.1)
        {
            Real crankNicolsonNPV = FdBlackScholesVanillaEngine.Calculate(
                option,
                context,
                process,
                tGrid: 500,
                xGrid: 100,
                dampingSteps: 0,
                schemeDesc: new FdmSchemeDesc(FdmSchemeDescFdmSchemeType.CrankNicolsonType, theta, 0.0)).NPV!.Value;

            Real npvDiff = Math.Abs(crankNicolsonNPV - npv);
            if (npvDiff > npvTol)
            {
                Assert.Fail(
                    "Failed to reproduce european option values with the Crank-Nicolson PDE scheme "
                    + $"\n    Analytic NPV:       {npv}\n    Crank-Nicolson NPV: {crankNicolsonNPV}"
                    + $"\n    theta:              {theta}\n    difference:         {npvDiff}"
                    + $"\n    tolerance:          {npvTol}");
            }

            Real douglasNPV = FdBlackScholesVanillaEngine.Calculate(
                option,
                context,
                process,
                tGrid: 500,
                xGrid: 100,
                dampingSteps: 0,
                schemeDesc: new FdmSchemeDesc(FdmSchemeDescFdmSchemeType.DouglasType, theta, 0.0)).NPV!.Value;

            Real schemeDiff = Math.Abs(crankNicolsonNPV - douglasNPV);

            if (schemeDiff > schemeTol)
            {
                Assert.Fail(
                    "Failed to reproduce Douglas scheme option values with the Crank-Nicolson PDE scheme "
                    + $"\n    Dougles NPV:        {douglasNPV}\n    Crank-Nicolson NPV: {crankNicolsonNPV}"
                    + $"\n    difference:         {schemeDiff}\n    tolerance:          {schemeTol}");
            }
        }
    }

    [Fact]
    public void TestBinomialGreeksWithCoincidentNodes()
    {
        // Testing binomial greeks when the tree collapses to one price...

        // At low volatility deep in the money the Leisen-Reimer up probability is
        // 0.99999999999999645, and the up and down factors round to one double.
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        Rate qRate = 0.0;
        Rate rRate = 0.0;
        Volatility vol = 0.01;

        GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(
            spot, FlatRate(today, qRate, dc), FlatRate(today, rRate, dc), FlatVol(today, vol, dc));

        var payoff = new PlainVanillaPayoff(OptionType.Call, 20.0);
        Exercise exercise = new EuropeanExercise(today + new Period(1, TimeUnit.Years));

        var option = new VanillaOption(payoff, exercise);
        OneAssetOptionResults results = BinomialVanillaEngine.Calculate<LeisenReimer>(option, context, stochProcess, 801);

        var reference = new VanillaOption(payoff, exercise);
        OneAssetOptionResults referenceResults = AnalyticEuropeanEngine.Calculate(reference, context, stochProcess);

        CheckClose(results.NPV!.Value, referenceResults.NPV!.Value, 1.0e-8);

        // QuantLib's "delta not provided", "gamma not provided" and "theta not provided": nanoql's results are null
        Assert.Null(results.Delta);
        Assert.Null(results.Gamma);
        Assert.Null(results.Theta);
    }

    // Adapted: the quotes changed in place become a process built for each value; the engines are the analytic and
    // finite-difference branches of QuantLib's makeOption, which the other branches' slots extend (the S-eq plan's
    // SEQD8).
    private static void TestEngineConsistency(
        EngineType engine, Size binomialSteps, Size samples, Dictionary<string, Real> tolerance, bool testGreeks = false)
    {
        Dictionary<string, Real> calculated = [], expected = [];

        // test options
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [75.0, 100.0, 125.0];
        Integer[] lengths = [1];

        // test data
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.00, 0.05];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Volatility[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    Date exDate = today + (length * 360);
                    Exercise exercise = new EuropeanExercise(exDate);
                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                    var option = new EuropeanOption(payoff, exercise);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                                        u, FlatRate(today, q, dc), FlatRate(today, r, dc), FlatVol(today, v, dc));

                                    // reference option
                                    OneAssetOptionResults refResults =
                                        MakeEngine(EngineType.Analytic, process, binomialSteps, samples)(option, context);

                                    // option to check
                                    OneAssetOptionResults results =
                                        MakeEngine(engine, process, binomialSteps, samples)(option, context);

                                    expected.Clear();
                                    calculated.Clear();

                                    expected["value"] = refResults.NPV!.Value;
                                    calculated["value"] = results.NPV!.Value;

                                    if (testGreeks && results.NPV!.Value > u * 1.0e-5)
                                    {
                                        expected["delta"] = refResults.Delta!.Value;
                                        expected["gamma"] = refResults.Gamma!.Value;
                                        expected["theta"] = refResults.Theta!.Value;
                                        calculated["delta"] = results.Delta!.Value;
                                        calculated["gamma"] = results.Gamma!.Value;
                                        calculated["theta"] = results.Theta!.Value;
                                    }

                                    foreach ((string greek, Real calcl) in calculated)
                                    {
                                        Real expct = expected[greek], tol = tolerance[greek];
                                        Real error = RelativeError(expct, calcl, u);
                                        if (error > tol)
                                        {
                                            Assert.Fail(
                                                $"European {type} option: spot {u}, strike {strike}, q {q}, r {r}, "
                                                + $"vol {v}: expected {greek} {expct}, calculated {calcl}, "
                                                + $"error {error}, tolerance {tol}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // QuantLib's makeOption's engine, for the branches this slot ports.
    private static Func<VanillaOption, EvaluationContext, OneAssetOptionResults> MakeEngine(
        EngineType engineType, GeneralizedBlackScholesProcess stochProcess, Size binomialSteps, Size samples) =>
        engineType switch
        {
            EngineType.Analytic => (option, context) => AnalyticEuropeanEngine.Calculate(option, context, stochProcess),
            EngineType.JR => (option, context) => BinomialJRVanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.CRR => (option, context) => BinomialCRRVanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.EQP => (option, context) => BinomialEQPVanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.TGEO => (option, context) =>
                BinomialTrigeorgisVanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.TIAN => (option, context) => BinomialTianVanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.LR => (option, context) => BinomialLRVanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.JOSHI => (option, context) => BinomialJ4VanillaEngine.Calculate(option, context, stochProcess, binomialSteps),
            EngineType.FiniteDifferences => (option, context) =>
                FdBlackScholesVanillaEngine.Calculate(option, context, stochProcess, tGrid: binomialSteps, xGrid: samples),
            _ => throw new InvalidOperationException($"the {engineType} engine waits for its slot"),
        };

    // The body of testImpliedVol and testImpliedVolWithDividends for one set of market data.
    private static void CheckImpliedVol(
        VanillaOption option,
        Date today,
        DayCounter dc,
        Real u,
        Rate q,
        Rate r,
        Volatility v,
        ImmutableArray<Dividend> dividends,
        Real tolerance,
        int maxEvaluations)
    {
        Real value = Npv(v);
        Volatility implVol = 0.0; // just to remove a warning...
        if (value != 0.0)
        {
            // shift guess somehow
            if (Math.Abs(value - Npv(v * 0.5)) <= 1.0e-12)
            {
                // flat price vs vol --- pointless (and
                // numerically unstable) to solve
                return;
            }

            GeneralizedBlackScholesProcess process = Process(v * 0.5);
            try
            {
                implVol = dividends.IsEmpty
                    ? option.ImpliedVolatility(value, process, RunnerContext, tolerance, maxEvaluations)
                    : option.ImpliedVolatility(value, process, dividends, RunnerContext, tolerance, maxEvaluations);
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                Assert.Fail(
                    $"\nimplied vol calculation failed:\n   option:         {((StrikedTypePayoff)option.Payoff!).OptionType}"
                    + $"\n   strike:         {((StrikedTypePayoff)option.Payoff!).Strike}\n   spot value:     {u}"
                    + $"\n   dividend yield: {q}\n   risk-free rate: {r}\n   today:          {today}"
                    + $"\n   maturity:       {option.Exercise.LastDate()}\n   volatility:     {v}"
                    + $"\n   option value:   {value}\n{e.Message}");
            }

            if (Math.Abs(implVol - v) > tolerance)
            {
                // the difference might not matter
                Real value2 = Npv(implVol);
                Real error = RelativeError(value, value2, u);
                if (error > tolerance)
                {
                    Assert.Fail(
                        $"option :\n    spot value:          {u}\n    dividend yield:      {q}\n"
                        + $"    risk-free rate:      {r}\n    original volatility: {v}\n    price:               {value}\n"
                        + $"    implied volatility:  {implVol}\n    corresponding price: {value2}\n"
                        + $"    error:               {error}");
                }
            }
        }

        GeneralizedBlackScholesProcess Process(Volatility vol) =>
            new BlackScholesMertonProcess(u, FlatRate(today, q, dc), FlatRate(today, r, dc), FlatVol(today, vol, dc));

        Real Npv(Volatility vol) => (dividends.IsEmpty
            ? AnalyticEuropeanEngine.Calculate(option, RunnerContext, Process(vol))
            : AnalyticDividendEuropeanEngine.Calculate(option, RunnerContext, Process(vol), dividends)).NPV!.Value;
    }

    // The test suite's flatRate(today, forward, dc): a flat forward from a fixed reference date.
    private static YieldTermStructure FlatRate(Date today, Rate forward, DayCounter dc) => new FlatForward(today, forward, dc);

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's flatVol(today, vol, dc): a constant volatility from a fixed reference date.
    private static BlackVolTermStructure FlatVol(Date today, Volatility vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    // The test suite's flatVol(vol, dc): a constant volatility from the evaluation date.
    private static BlackVolTermStructure FlatVol(Volatility vol, DayCounter dc, EvaluationContext context) =>
        BlackConstantVol.Create(0, new NullCalendar(), vol, dc, context);

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    // BOOST_CHECK_CLOSE: the difference within the tolerance, in percent, of both values.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b);
        Assert.True(
            diff <= Math.Abs(a) * percent / 100.0 && diff <= Math.Abs(b) * percent / 100.0,
            $"difference between {a} and {b} exceeds {percent}%");
    }

    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // QuantLib's EngineType.
    private enum EngineType
    {
        Analytic,
        JR,
        CRR,
        EQP,
        TGEO,
        TIAN,
        LR,
        JOSHI,
        FiniteDifferences,
        Integral,
        PseudoMonteCarlo,
        QuasiMonteCarlo,
        FFT,
    }

    private readonly record struct EuropeanOptionData(
        OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Real Result, Real Tol);
}
