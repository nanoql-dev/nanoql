// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/dividendoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCashDividendEuropeanEngine, testCashDividendEuropeanEngineWithManyDividends, testCashDividendEuropeanEngineWithSingleDividends and testZeroStrikeCallWithCashDividends, which wait for B4.5's CashDividendEuropeanEngine

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class DividendOptionTests
{
    // Adapted: the quotes changed in place become processes built for each value, on curves dated by the context.
    [Fact]
    public void TestEuropeanValues()
    {
        // Testing dividend European option values with no dividends...

        Real tolerance = 1.0e-5;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.00, 0.10, 0.30];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] lengths = [1, 2];
        Volatility[] vols = [0.05, 0.20, 0.70];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    Date exDate = today + (length * TimeUnit.Years);
                    Exercise exercise = new EuropeanExercise(exDate);

                    List<Date> dividendDates = [];
                    List<Real> dividends = [];
                    for (Date d = today + (3 * TimeUnit.Months); d < exercise.LastDate(); d += 6 * TimeUnit.Months)
                    {
                        dividendDates.Add(d);
                        dividends.Add(0.0);
                    }

                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);

                    var refOption = new VanillaOption(payoff, exercise);
                    var option1 = new VanillaOption(payoff, exercise);
                    ImmutableArray<Dividend> dividends1 = DividendFunctions.DividendVector(dividendDates, dividends);
                    var option2 = new VanillaOption(payoff, exercise);
                    ImmutableArray<Dividend> dividends2 = DividendFunctions.DividendVector([exDate + (6 * TimeUnit.Months)], [1.0]);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    GeneralizedBlackScholesProcess stochProcess = Process(u, q, r, v, dc, context);

                                    Real expected = AnalyticEuropeanEngine.Calculate(refOption, context, stochProcess).NPV!.Value;
                                    Real calculated1 = AnalyticDividendEuropeanEngine.Calculate(
                                        option1, context, stochProcess, dividends1).NPV!.Value;
                                    Real calculated2 = AnalyticDividendEuropeanEngine.Calculate(
                                        option2, context, stochProcess, dividends2).NPV!.Value;
                                    Real error1 = Math.Abs(calculated1 - expected);
                                    Real error2 = Math.Abs(calculated2 - expected);
                                    if (error1 > tolerance)
                                    {
                                        Report("value start limit", payoff, u, q, r, v, expected, calculated1, error1, tolerance);
                                    }

                                    if (error2 > tolerance)
                                    {
                                        Report("value start limit", payoff, u, q, r, v, expected, calculated2, error2, tolerance);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Reference pg. 253 - Hull - Options, Futures, and Other Derivatives 5th ed
    // Exercise 12.8
    [Fact]
    public void TestEuropeanKnownValue()
    {
        // Testing dividend European option against known value...

        Real tolerance = 1.0e-2;
        Real expected = 3.67;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        Date exDate = today + (180 * TimeUnit.Days);
        Exercise exercise = new EuropeanExercise(exDate);

        Date[] dividendDates = [today + (2 * 30 * TimeUnit.Days), today + (5 * 30 * TimeUnit.Days)];
        Real[] dividends = [0.50, 0.50];

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 40.0);

        var option = new VanillaOption(payoff, exercise);

        Real u = 40.0;
        Rate q = 0.0, r = 0.09;
        Volatility v = 0.30;
        GeneralizedBlackScholesProcess stochProcess = Process(u, q, r, v, dc, context);

        Real calculated = AnalyticDividendEuropeanEngine.Calculate(
            option, context, stochProcess, DividendFunctions.DividendVector(dividendDates, dividends)).NPV!.Value;
        Real error = Math.Abs(calculated - expected);
        if (error > tolerance)
        {
            Report("value start limit", payoff, u, q, r, v, expected, calculated, error, tolerance);
        }
    }

    // Adapted: the quotes changed in place become processes built for each value, on curves dated by the context.
    [Fact]
    public void TestEuropeanStartLimit()
    {
        // Testing dividend European option with a dividend on today's date...

        Real tolerance = 1.0e-5;
        Real dividendValue = 10.0;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.00, 0.10, 0.30];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] lengths = [1, 2];
        Volatility[] vols = [0.05, 0.20, 0.70];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    Date exDate = today + (length * TimeUnit.Years);
                    Exercise exercise = new EuropeanExercise(exDate);

                    Date[] dividendDates = [today];
                    Real[] dividends = [dividendValue];

                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);

                    var refOption = new VanillaOption(payoff, exercise);
                    var option = new VanillaOption(payoff, exercise);
                    ImmutableArray<Dividend> schedule = DividendFunctions.DividendVector(dividendDates, dividends);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    Real calculated = AnalyticDividendEuropeanEngine.Calculate(
                                        option, context, Process(u, q, r, v, dc, context), schedule).NPV!.Value;
                                    Real expected = AnalyticEuropeanEngine.Calculate(
                                        refOption, context, Process(u - dividendValue, q, r, v, dc, context)).NPV!.Value;
                                    Real error = Math.Abs(calculated - expected);
                                    if (error > tolerance)
                                    {
                                        Report("value", payoff, u, q, r, v, expected, calculated, error, tolerance);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: the quotes changed in place (spot, rates, volatility and the evaluation date) become processes built for
    // each value, on curves dated by the context.
    [Fact]
    public void TestEuropeanGreeks()
    {
        // Testing dividend European option greeks...

        Dictionary<string, Real> calculated = [], expected = [], tolerance = [];
        tolerance["delta"] = 1.0e-5;
        tolerance["gamma"] = 1.0e-5;
        tolerance["theta"] = 1.0e-5;
        tolerance["rho"] = 1.0e-5;
        tolerance["vega"] = 1.0e-5;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.00, 0.10, 0.30];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] lengths = [1, 2];
        Volatility[] vols = [0.05, 0.20, 0.40];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    Date exDate = today + (length * TimeUnit.Years);
                    Exercise exercise = new EuropeanExercise(exDate);

                    List<Date> dividendDates = [];
                    List<Real> dividends = [];
                    for (Date d = today + (3 * TimeUnit.Months); d < exercise.LastDate(); d += 6 * TimeUnit.Months)
                    {
                        dividendDates.Add(d);
                        dividends.Add(5.0);
                    }

                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                    ImmutableArray<Dividend> schedule = DividendFunctions.DividendVector(dividendDates, dividends);
                    var option = new VanillaOption(payoff, exercise);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    OneAssetOptionResults results = Price(u, q, r, v, context);
                                    Real value = results.NPV!.Value;
                                    calculated["delta"] = results.Delta!.Value;
                                    calculated["gamma"] = results.Gamma!.Value;
                                    calculated["theta"] = results.Theta!.Value;
                                    calculated["rho"] = results.Rho!.Value;
                                    calculated["vega"] = results.Vega!.Value;

                                    if (value > u * 1.0e-5)
                                    {
                                        // perturb spot and get delta and gamma
                                        Real du = u * 1.0e-4;
                                        OneAssetOptionResults p = Price(u + du, q, r, v, context);
                                        OneAssetOptionResults mm = Price(u - du, q, r, v, context);
                                        expected["delta"] = (p.NPV!.Value - mm.NPV!.Value) / (2 * du);
                                        expected["gamma"] = (p.Delta!.Value - mm.Delta!.Value) / (2 * du);

                                        // perturb risk-free rate and get rho
                                        Spread dr = r * 1.0e-4;
                                        Real valueP = Price(u, q, r + dr, v, context).NPV!.Value;
                                        Real valueM = Price(u, q, r - dr, v, context).NPV!.Value;
                                        expected["rho"] = (valueP - valueM) / (2 * dr);

                                        // perturb volatility and get vega
                                        Spread dv = v * 1.0e-4;
                                        valueP = Price(u, q, r, v + dv, context).NPV!.Value;
                                        valueM = Price(u, q, r, v - dv, context).NPV!.Value;
                                        expected["vega"] = (valueP - valueM) / (2 * dv);

                                        // perturb date and get theta
                                        double dT = dc.YearFraction(today - 1, today + 1);
                                        valueM = Price(u, q, r, v, context with { EvaluationDate = today - 1 }).NPV!.Value;
                                        valueP = Price(u, q, r, v, context with { EvaluationDate = today + 1 }).NPV!.Value;
                                        expected["theta"] = (valueP - valueM) / dT;

                                        // compare
                                        foreach ((string greek, Real calcl) in calculated)
                                        {
                                            Real expct = expected[greek], tol = tolerance[greek];
                                            Real error = RelativeError(expct, calcl, u);
                                            if (error > tol)
                                            {
                                                Report(greek, payoff, u, q, r, v, expct, calcl, error, tol);
                                            }
                                        }
                                    }

                                    OneAssetOptionResults Price(Real s, Rate qq, Rate rr, Volatility vv, EvaluationContext ctx) =>
                                        AnalyticDividendEuropeanEngine.Calculate(option, ctx, Process(s, qq, rr, vv, dc, ctx), schedule);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: the quotes changed in place become processes built for each value, on curves dated by the context.
    [Fact]
    public void TestFdEuropeanValues()
    {
        // Testing finite-difference dividend European option values...

        Real tolerance = 1.0e-2;
        Size gridPoints = 400;
        Size timeSteps = 40;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.00, 0.10, 0.30];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] lengths = [1, 2];
        Volatility[] vols = [0.05, 0.20, 0.40];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int length in lengths)
                {
                    Date exDate = today + (length * TimeUnit.Years);
                    Exercise exercise = new EuropeanExercise(exDate);

                    List<Date> dividendDates = [];
                    List<Real> dividends = [];
                    for (Date d = today + (3 * TimeUnit.Months); d < exercise.LastDate(); d += 6 * TimeUnit.Months)
                    {
                        dividendDates.Add(d);
                        dividends.Add(5.0);
                    }

                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                    ImmutableArray<Dividend> schedule = DividendFunctions.DividendVector(dividendDates, dividends);
                    var option = new VanillaOption(payoff, exercise);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    GeneralizedBlackScholesProcess stochProcess = Process(u, q, r, v, dc, context);
                                    Real calculated = new MakeFdBlackScholesVanillaEngine(stochProcess)
                                        .WithTGrid(timeSteps)
                                        .WithXGrid(gridPoints)
                                        .WithCashDividends(dividendDates, dividends)
                                        .WithCashDividendModel(FdBlackScholesVanillaEngineCashDividendModel.Escrowed)
                                        .ToEngine()(option, context).NPV!.Value;
                                    if (calculated > u * 1.0e-5)
                                    {
                                        Real expected = AnalyticDividendEuropeanEngine.Calculate(
                                            option, context, stochProcess, schedule).NPV!.Value;
                                        Real error = Math.Abs(calculated - expected);
                                        if (error > tolerance)
                                        {
                                            Report("value", payoff, u, q, r, v, expected, calculated, error, tolerance);
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

    [Fact]
    public void TestFdEuropeanGreeks()
    {
        // Testing finite-differences dividend European option greeks...

        Date today = Date.TodaysDate();
        Integer[] lengths = [1, 2];

        foreach (int length in lengths)
        {
            Date exDate = today + (length * TimeUnit.Years);
            Exercise exercise = new EuropeanExercise(exDate);
            TestFdGreeks(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Spot);
            TestFdGreeks(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Escrowed);
        }
    }

    [Fact]
    public void TestFdAmericanGreeks()
    {
        // Testing finite-differences dividend American option greeks...

        Date today = Date.TodaysDate();
        Integer[] lengths = [1, 2];

        foreach (int length in lengths)
        {
            Date exDate = today + (length * TimeUnit.Years);
            Exercise exercise = new AmericanExercise(today, exDate);
            TestFdGreeks(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Spot);
        }
    }

    [Fact]
    public void TestFdEuropeanDegenerate()
    {
        // Testing degenerate finite-differences dividend European option...

        var today = new Date(27, Month.February, 2005);
        var exDate = new Date(13, Month.April, 2005);

        Exercise exercise = new EuropeanExercise(exDate);

        TestFdDegenerate(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Spot);
        TestFdDegenerate(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Escrowed);
    }

    [Fact]
    public void TestFdAmericanDegenerate()
    {
        // Testing degenerate finite-differences dividend American option...

        var today = new Date(27, Month.February, 2005);
        var exDate = new Date(13, Month.April, 2005);

        Exercise exercise = new AmericanExercise(today, exDate);

        TestFdDegenerate(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Spot);
        TestFdDegenerate(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Escrowed);
    }

    [Fact]
    public void TestFdEuropeanWithDividendToday()
    {
        // Testing finite-differences dividend European option with dividend on today's date...

        var today = new Date(27, Month.February, 2005);
        var exDate = new Date(13, Month.April, 2005);

        Exercise exercise = new EuropeanExercise(exDate);

        TestFdDividendAtTZero(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Spot);
        TestFdDividendAtTZero(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Escrowed);
    }

    [Fact]
    public void TestFdAmericanWithDividendToday()
    {
        // Testing finite-differences dividend American option with dividend on today's date...

        var today = new Date(27, Month.February, 2005);
        var exDate = new Date(13, Month.April, 2005);

        Exercise exercise = new AmericanExercise(today, exDate);

        TestFdDividendAtTZero(today, exercise, FdBlackScholesVanillaEngineCashDividendModel.Spot);
    }

    [Fact]
    public void TestEscrowedDividendModel()
    {
        // Testing finite-difference European engine with the escrowed dividend model...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(12, Month.October, 2019);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.063, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.094, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.3, dc);

        Date maturity = today + new Period(1, TimeUnit.Years);

        GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        var payoff = new PlainVanillaPayoff(OptionType.Put, spot);

        Exercise exercise = new EuropeanExercise(maturity);

        Date[] dividendDates = [today + new Period(3, TimeUnit.Months), today + new Period(9, TimeUnit.Months)];
        Real[] dividendAmounts = [8.3, 6.8];

        var refOption = new VanillaOption(payoff, exercise);

        OneAssetOptionResults analytic = AnalyticDividendEuropeanEngine.Calculate(
            refOption, context, process, DividendFunctions.DividendVector(dividendDates, dividendAmounts));

        Real analyticNPV = analytic.NPV!.Value;
        Real analyticDelta = analytic.Delta!.Value;

        var option = new VanillaOption(payoff, exercise);
        OneAssetOptionResults pde = new MakeFdBlackScholesVanillaEngine(process)
            .WithTGrid(50)
            .WithXGrid(200)
            .WithDampingSteps(1)
            .WithCashDividends(dividendDates, dividendAmounts)
            .WithCashDividendModel(FdBlackScholesVanillaEngineCashDividendModel.Escrowed)
            .ToEngine()(option, context);

        Real pdeNPV = pde.NPV!.Value;
        Real pdeDelta = pde.Delta!.Value;

        Real tol = 0.0025;

        if (Math.Abs(pdeNPV - analyticNPV) > tol)
        {
            Assert.Fail(
                "Failed to reproduce European option values with the escrowed dividend model and the "
                + $"FdBlackScholesVanillaEngine engine\n    calculated: {pdeNPV}\n    expected:   {analyticNPV}"
                + $"\n    difference: {Math.Abs(pdeNPV - analyticNPV)}\n    tolerance:  {tol}");
        }

        if (Math.Abs(pdeDelta - analyticDelta) > tol)
        {
            Assert.Fail(
                "Failed to reproduce European option deltas with the escrowed dividend model and the "
                + $"FdBlackScholesVanillaEngine engine\n    calculated: {pdeNPV}\n    expected:   {analyticNPV}"
                + $"\n    difference: {Math.Abs(pdeNPV - analyticNPV)}\n    tolerance:  {tol}");
        }
    }

    // Adapted: the volatility quote changed in place becomes a process built for each value.
    [Fact]
    public void TestAmericanOptionsWithEscrowedDividends()
    {
        // Testing American option with escrowed dividend model...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(26, Month.October, 2025);
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturityDate = today + new Period(18, TimeUnit.Months);
        double maturityTime = dc.YearFraction(today, maturityDate);

        Real spot = 100.0;

        YieldTermStructure qTS = new FlatForward(today, 0.05, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.15, dc);

        Volatility v = 0.3;

        GeneralizedBlackScholesProcess Process(Volatility vol) =>
            new BlackScholesMertonProcess(100, qTS, rTS, FlatVol(vol, dc, context));

        Real dividend = 5.0;

        foreach (OptionType optionType in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (Date dividendDate in new[]
                     {
                         today, new Date(1, Month.January, 2026), new Date(1, Month.January, 2027), maturityDate,
                     })
            {
                Func<VanillaOption, EvaluationContext, OneAssetOptionResults> Builder(
                    Volatility vol, FdBlackScholesVanillaEngineCashDividendModel model) =>
                    new MakeFdBlackScholesVanillaEngine(Process(vol))
                        .WithTGrid(100)
                        .WithXGrid(400)
                        .WithCashDividends([dividendDate], [dividend])
                        .WithCashDividendModel(model)
                        .ToEngine();

                var option = new VanillaOption(
                    new PlainVanillaPayoff(optionType, 95),
                    new AmericanExercise(maturityDate));

                Real escrowedNPV =
                    Builder(v, FdBlackScholesVanillaEngineCashDividendModel.Escrowed)(option, context).NPV!.Value;

                Real s = (spot - dividend) / spot;
                Volatility spotVol = Math.Sqrt(
                    ((v * v * dc.YearFraction(today, dividendDate) * (s * s))
                     + (v * v * dc.YearFraction(dividendDate, maturityDate))) / maturityTime);
                Real spotNPV =
                    Builder(spotVol, FdBlackScholesVanillaEngineCashDividendModel.Spot)(option, context).NPV!.Value;

                Real tol = 0.1;
                Real diff = Math.Abs(spotNPV - escrowedNPV);
                if (diff > tol)
                {
                    Assert.Fail(
                        "Failed to compare American option prices with cash- and escrowed dividend model"
                        + $"\n    Option Type       : {optionType}\n    dividend date     : {dividendDate}"
                        + $"\n    escrowed div price: {escrowedNPV}\n    cash div price    : {spotNPV}"
                        + $"\n    difference        : {diff}\n    tolerance         : {tol}");
                }
            }
        }
    }

    // Adapted: the quotes changed in place become processes built for each value, on curves dated by the context.
    private static void TestFdGreeks(Date today, Exercise exercise, FdBlackScholesVanillaEngineCashDividendModel model)
    {
        Dictionary<string, Real> calculated = [], expected = [], tolerance = [];
        tolerance["delta"] = 5.0e-3;
        tolerance["gamma"] = 7.0e-3;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.00, 0.10, 0.20];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Volatility[] vols = [0.05, 0.20, 0.50];

        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                List<Date> dividendDates = [];
                List<Real> dividends = [];
                for (Date d = today + (3 * TimeUnit.Months); d < exercise.LastDate(); d += 6 * TimeUnit.Months)
                {
                    dividendDates.Add(d);
                    dividends.Add(5.0);
                }

                StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);

                var option = new VanillaOption(payoff, exercise);

                foreach (Real u in underlyings)
                {
                    foreach (Real m in qRates)
                    {
                        foreach (Real n in rRates)
                        {
                            foreach (Real v in vols)
                            {
                                Rate q = m, r = n;

                                OneAssetOptionResults results = Price(u);
                                Real value = results.NPV!.Value;
                                calculated["delta"] = results.Delta!.Value;
                                calculated["gamma"] = results.Gamma!.Value;

                                if (value > u * 1.0e-5)
                                {
                                    // perturb spot and get delta and gamma
                                    Real du = u * 1.0e-4;
                                    OneAssetOptionResults p = Price(u + du);
                                    OneAssetOptionResults mm = Price(u - du);
                                    expected["delta"] = (p.NPV!.Value - mm.NPV!.Value) / (2 * du);
                                    expected["gamma"] = (p.Delta!.Value - mm.Delta!.Value) / (2 * du);

                                    // compare
                                    foreach ((string greek, Real calcl) in calculated)
                                    {
                                        Real expct = expected[greek], tol = tolerance[greek];
                                        Real error = RelativeError(expct, calcl, u);
                                        if (error > tol)
                                        {
                                            Report(greek, payoff, u, q, r, v, expct, calcl, error, tol);
                                        }
                                    }
                                }

                                OneAssetOptionResults Price(Real s) =>
                                    new MakeFdBlackScholesVanillaEngine(Process(s, q, r, v, dc, context))
                                        .WithCashDividends(dividendDates, dividends)
                                        .WithCashDividendModel(model)
                                        .ToEngine()(option, context);
                            }
                        }
                    }
                }
            }
        }
    }

    private static void TestFdDegenerate(Date today, Exercise exercise, FdBlackScholesVanillaEngineCashDividendModel model)
    {
        DayCounter dc = new Actual360();
        var context = new EvaluationContext { EvaluationDate = today };
        GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
            54.625,
            FlatForward.Create(0, new NullCalendar(), 0.0, dc, context),
            FlatForward.Create(0, new NullCalendar(), 0.052706, dc, context),
            FlatVol(0.282922, dc, context));

        Size timeSteps = 100;
        Size gridPoints = 300;

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 55.0);

        Real tolerance = 1.0e-6;

        var option = new VanillaOption(payoff, exercise);
        Real refValue = new MakeFdBlackScholesVanillaEngine(process)
            .WithTGrid(timeSteps)
            .WithXGrid(gridPoints)
            .WithCashDividendModel(model)
            .ToEngine()(option, context).NPV!.Value;

        List<Rate> dividends = [];
        List<Date> dividendDates = [];

        Real value = new MakeFdBlackScholesVanillaEngine(process)
            .WithTGrid(timeSteps)
            .WithXGrid(gridPoints)
            .WithCashDividends(dividendDates, dividends)
            .WithCashDividendModel(model)
            .ToEngine()(option, context).NPV!.Value;

        if (Math.Abs(refValue - value) > tolerance)
        {
            Assert.Fail(
                $"NPV changed by empty dividend set:\n    previous value: {value}\n    current value:  {refValue}\n"
                + $"    change:         {value - refValue}");
        }

        for (Size i = 1; i <= 6; i++)
        {
            dividends.Add(0.0);
            dividendDates.Add(today + i);

            value = new MakeFdBlackScholesVanillaEngine(process)
                .WithTGrid(timeSteps)
                .WithXGrid(gridPoints)
                .WithCashDividends(dividendDates, dividends)
                .WithCashDividendModel(model)
                .ToEngine()(option, context).NPV!.Value;

            if (Math.Abs(refValue - value) > tolerance)
            {
                Assert.Fail(
                    $"NPV changed by null dividend :\n    previous value: {value}\n    current value:  {refValue}\n"
                    + $"    change:         {value - refValue}");
            }
        }
    }

    // Adapted: QuantLib's theta() throws on its null theta, which is a null result here.
    private static void TestFdDividendAtTZero(
        Date today, Exercise exercise, FdBlackScholesVanillaEngineCashDividendModel model)
    {
        DayCounter dc = new Actual360();
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), 0.0, dc, context);
        GeneralizedBlackScholesProcess process =
            new BlackScholesMertonProcess(54.625, rTS, rTS, FlatVol(0.282922, dc, context));

        Size timeSteps = 50;
        Size gridPoints = 400;

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 55.0);

        // today's dividend must by taken into account
        Rate[] dividends = [1.0];
        Date[] dividendDates = [today];

        var option = new VanillaOption(payoff, exercise);
        OneAssetOptionResults results = new MakeFdBlackScholesVanillaEngine(process)
            .WithTGrid(timeSteps)
            .WithXGrid(gridPoints)
            .WithCashDividends(dividendDates, dividends)
            .WithCashDividendModel(model)
            .ToEngine()(option, context);
        Real calculated = results.NPV!.Value;

        switch (model)
        {
            case FdBlackScholesVanillaEngineCashDividendModel.Spot:
                Assert.Null(results.Theta);
                break;
            case FdBlackScholesVanillaEngineCashDividendModel.Escrowed:
                Assert.NotNull(results.Theta);
                break;
            default:
                throw new NanoQLException("unknown dividend model type");
        }

        Exercise europeanExercise = new EuropeanExercise(exercise.LastDate());
        var europeanOption = new VanillaOption(payoff, europeanExercise);

        Real expected = AnalyticDividendEuropeanEngine.Calculate(
            europeanOption, context, process, DividendFunctions.DividendVector(dividendDates, dividends)).NPV!.Value;

        Real tol = 1e-4;

        if (Math.Abs(calculated - expected) > tol)
        {
            Assert.Fail(
                "Can not reproduce reference values from analytic dividend engine :\n"
                + $"    calculated: {calculated}\n    expected  : {expected}\n    diff:       {tol}");
        }
    }

    // The test suite's flatVol(vol, dc): a constant volatility from the evaluation date.
    private static BlackVolTermStructure FlatVol(Volatility vol, DayCounter dc, EvaluationContext context) =>
        BlackConstantVol.Create(0, new NullCalendar(), vol, dc, context);

    // The process on the test suite's flatRate(quote, dc) and flatVol(quote, dc): curves from the evaluation date.
    private static GeneralizedBlackScholesProcess Process(
        Real u, Rate q, Rate r, Volatility v, DayCounter dc, EvaluationContext context) =>
        new BlackScholesMertonProcess(
            u,
            FlatForward.Create(0, new NullCalendar(), q, dc, context),
            FlatForward.Create(0, new NullCalendar(), r, dc, context),
            BlackConstantVol.Create(0, new NullCalendar(), v, dc, context));

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    private static void Report(
        string greekName,
        StrikedTypePayoff payoff,
        Real s,
        Rate q,
        Rate r,
        Volatility v,
        Real expected,
        Real calculated,
        Real error,
        Real tolerance) =>
        Assert.Fail(
            $"European {payoff.OptionType} option with {payoff.Name} payoff:\n    spot value:       {s}\n"
            + $"    strike:           {payoff.Strike}\n    dividend yield:   {q}\n    risk-free rate:   {r}\n"
            + $"    volatility:       {v}\n\n    expected   {greekName}: {expected}\n"
            + $"    calculated {greekName}: {calculated}\n    error:            {error}\n"
            + $"    tolerance:        {tolerance}");
}
