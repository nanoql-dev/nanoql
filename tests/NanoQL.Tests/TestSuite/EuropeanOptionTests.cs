// SPDX-FileCopyrightText: Copyright (C) 2003, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/europeanoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testValues, testFdEngines, testLocalVolatility, testPDESchemes, testFdEngineWithNonConstantParameters and testDouglasVsCrankNicolson, which wait for this slot's finite-difference engine; testJRBinomialEngines, testCRRBinomialEngines, testEQPBinomialEngines, testTGEOBinomialEngines, testTIANBinomialEngines, testLRBinomialEngines, testJOSHIBinomialEngines, testFarOutOfTheMoneyBinomialEngines, testLargeDriftBinomialEngines and testBinomialGreeksWithCoincidentNodes, which wait for this slot's binomial engines; testIntegralEngines, which waits for B4.5's IntegralEngine; testMcEngines and testQmcEngines, which wait for B4.4's MCEuropeanEngine; testFFTEngines, which waits for B7.6's FFTVanillaEngine; testFdEnginesWithNonStrikedPayoff, which waits for B4.3's FdHestonVanillaEngine and FdCIRVanillaEngine

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
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
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);
}
