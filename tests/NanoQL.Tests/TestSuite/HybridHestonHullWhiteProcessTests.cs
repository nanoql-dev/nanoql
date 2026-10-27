// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hybridhestonhullwhiteprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class HybridHestonHullWhiteProcessTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // The test suite's hestonModels.
    private static readonly HestonModelData[] HestonModels =
    [
        // ADI finite difference schemes for option pricing in the
        // Heston model with correlation, K.J. in t'Hout and S. Foulon,
        new("'t Hout case 1", 0.04, 1.5, 0.04, 0.3, -0.9, 0.025, 0.0),
        new("'t Hout case 2", 0.12, 3.0, 0.12, 0.04, 0.6, 0.01, 0.04),
        new("'t Hout case 3", 0.0707, 0.6067, 0.0707, 0.2928, -0.7571, 0.03, 0.0),
        new("'t Hout case 4", 0.06, 2.5, 0.06, 0.5, -0.1, 0.0507, 0.0469),

        // Efficient numerical methods for pricing American options under
        // stochastic volatility, Samuli Ikonen and Jari Toivanen,
        new("Ikonen-Toivanen", 0.0625, 5, 0.16, 0.9, 0.1, 0.1, 0.0),

        // Not-so-complex logarithms in the Heston model,
        // Christian Kahl and Peter Jaeckel
        new("Kahl-Jaeckel", 0.16, 1.0, 0.16, 2.0, -0.8, 0.0, 0.0),

        // self defined test cases
        new("Equity case", 0.07, 2.0, 0.04, 0.55, -0.8, 0.03, 0.035),
        new("high correlation", 0.07, 1.0, 0.04, 0.55, 0.995, 0.02, 0.04),
        new("low Vol-Of-Vol", 0.07, 1.0, 0.04, 0.001, -0.75, 0.04, 0.03),
        new("kappaEqSigRho", 0.07, 0.4, 0.04, 0.5, 0.8, 0.03, 0.03),
    ];

    // The test suite's hullWhiteModels.
    private static readonly HullWhiteModelData[] HullWhiteModels =
    [
        new("EUR-2003", 0.00883, 0.00631),
    ];

    // The test suite's schemes.
    private static readonly SchemeData[] Schemes =
    [
        new("HV2", FdmSchemeDesc.Hundsdorfer()),
        new("HV1", FdmSchemeDesc.ModifiedHundsdorfer()),
        new("CS", FdmSchemeDesc.CraigSneyd()),
        new("MCS", FdmSchemeDesc.ModifiedCraigSneyd()),
        new("DS", FdmSchemeDesc.Douglas()),
    ];

    [Fact]
    public void TestBsmHullWhiteEngine()
    {
        // Testing European option pricing for a BSM process with one-factor Hull-White model...
        DayCounter dc = new Actual365Fixed();

        Date today = Date.TodaysDate();
        Date maturity = today + new Period(20, TimeUnit.Years);

        var context = new EvaluationContext { EvaluationDate = today };

        const Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.04, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.0525, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.25, dc);

        var hullWhiteModel = new HullWhite(rTS, 0.00883, 0.00526);

        GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        Exercise exercise = new EuropeanExercise(maturity);

        Real fwd = spot * qTS.Discount(maturity) / rTS.Discount(maturity);
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, fwd);

        var option = new EuropeanOption(payoff, exercise);

        const Real tol = 1e-8;
        Real[] corr = [-0.75, -0.25, 0.0, 0.25, 0.75];
        Volatility[] expectedVol = [0.217064577, 0.243995801, 0.256402830, 0.268236596, 0.290461343];

        for (int i = 0; i < corr.Length; ++i)
        {
            OneAssetOptionResults bsmhw = AnalyticBSMHullWhiteEngine.Calculate(
                option, context, corr[i], stochProcess, hullWhiteModel);
            Real npv = bsmhw.NPV!.Value;

            BlackVolTermStructure compVolTS = new BlackConstantVol(today, new NullCalendar(), expectedVol[i], dc);

            GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(spot, qTS, rTS, compVolTS);

            var comp = new EuropeanOption(payoff, exercise);
            OneAssetOptionResults bs = AnalyticEuropeanEngine.Calculate(comp, context, bsProcess);

            Volatility impliedVol = comp.ImpliedVolatility(npv, bsProcess, context, 1e-10, 100);

            Assert.False(
                Math.Abs(impliedVol - expectedVol[i]) > tol,
                $"Failed to reproduce implied volatility\n    calculated: {impliedVol}\n    expected  : {expectedVol[i]}");
            Assert.False(
                Math.Abs((bs.NPV!.Value - npv) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs(bs.Delta!.Value - bsmhw.Delta!.Value) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs((bs.Gamma!.Value - bsmhw.Gamma!.Value) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs((bs.Theta!.Value - bsmhw.Theta!.Value) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs((bs.Vega!.Value - bsmhw.Vega!.Value) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
        }
    }

    [Fact]
    public void TestCompareBsmHWandHestonHW()
    {
        // Comparing European option pricing for a BSM process with one-factor Hull-White model...
        DayCounter dc = new Actual365Fixed();

        Date today = Date.TodaysDate();

        var context = new EvaluationContext { EvaluationDate = today };

        const Real spot = 100.0;
        var dates = new List<Date>();
        var rates = new List<Rate>();
        var divRates = new List<Rate>();

        for (int i = 0; i <= 40; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
            rates.Add(0.01 + (0.0002 * Math.Exp(Math.Sin(i / 4.0))));
            divRates.Add(0.02 + (0.0001 * Math.Exp(Math.Sin(i / 5.0))));
        }

        YieldTermStructure rTS = new ZeroCurve(dates, rates, dc);
        YieldTermStructure qTS = new ZeroCurve(dates, divRates, dc);

        const Real vol = 0.25;
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var bsmProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        var hestonProcess = new HestonProcess(rTS, qTS, spot, vol * vol, 1.0, vol * vol, 1e-4, 0.0);

        var hestonModel = new HestonModel(hestonProcess);

        var hullWhiteModel = new HullWhite(rTS, 0.01, 0.01);

        const Real tol = 1e-5;
        Real[] strike = [0.25, 0.5, 0.75, 0.8, 0.9, 1.0, 1.1, 1.2, 1.5, 2.0, 4.0];
        int[] maturity = [1, 2, 3, 5, 10, 15, 20, 25, 30];
        OptionType[] types = [OptionType.Put, OptionType.Call];

        foreach (OptionType type in types)
        {
            foreach (Real j in strike)
            {
                foreach (int l in maturity)
                {
                    Date maturityDate = today + new Period(l, TimeUnit.Years);

                    Exercise exercise = new EuropeanExercise(maturityDate);

                    Real fwd = j * spot * qTS.Discount(maturityDate) / rTS.Discount(maturityDate);

                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, fwd);

                    var option = new EuropeanOption(payoff, exercise);

                    Real calculated = AnalyticBSMHullWhiteEngine.Calculate(option, context, 0.0, bsmProcess, hullWhiteModel).NPV!.Value;

                    Real expected = AnalyticHestonHullWhiteEngine.Calculate(option, context, hestonModel, hullWhiteModel, 128).NPV!.Value;

                    Assert.False(
                        Math.Abs(calculated - expected) > calculated * tol && Math.Abs(calculated - expected) > tol,
                        $"Failed to reproduce npvs\n    calculated: {calculated}\n    expected  : {expected}\n    strike    : {j}\n    maturity  : {l}\n    type      : {(type == OptionType.Put ? "Put" : "Call")}");
                }
            }
        }
    }

    [Fact]
    public void TestZeroBondPricing()
    {
        // Testing Monte-Carlo zero bond pricing...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        // construct a strange yield curve to check drifts and discounting
        // of the joint stochastic process

        var dates = new List<Date>();
        var times = new List<double>();
        var rates = new List<Rate>();

        dates.Add(today);
        rates.Add(0.02);
        times.Add(0.0);
        for (int i = 120; i < 240; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Months));
            rates.Add(0.02 + (0.0002 * Math.Exp(Math.Sin(i / 8.0))));
            times.Add(dc.YearFraction(today, dates[^1]));
        }

        Date maturity = dates[^1] + new Period(10, TimeUnit.Years);
        dates.Add(maturity);
        rates.Add(0.04);
        times.Add(dc.YearFraction(today, dates[^1]));

        const Real s0 = 100;

        YieldTermStructure ts = new ZeroCurve(dates, rates, dc);
        YieldTermStructure ds = new FlatForward(today, 0.0, dc);

        var hestonProcess = new HestonProcess(ts, ds, s0, 0.02, 1.0, 0.2, 0.5, -0.8);
        HullWhiteForwardProcess hwProcess = new HullWhiteForwardProcess(ts, 0.05, 0.05)
            .WithForwardMeasureTime(dc.YearFraction(today, maturity));
        var hwModel = new HullWhite(ts, 0.05, 0.05);

        var jointProcess = new HybridHestonHullWhiteProcess(hestonProcess, hwProcess, -0.4);

        var grid = new TimeGrid(times.ToArray().AsSpan(0, times.Count - 1));

        int factors = jointProcess.Factors;
        int steps = grid.Size - 1;

        // QuantLib's rsg_type(factors, steps): the diagonal ordering and seed 0 by default, a seed from the clock
        // (S2R4), which the Sobol sequence reads only past its tabulated dimensions (sobolrsg.cpp's random
        // initialization); these 3 factors over the grid's steps read none, so a fixed seed draws QuantLib's numbers.
        var rsg = new SobolBrownianBridgeRsg(factors, steps, SobolBrownianGeneratorBaseOrdering.Diagonal, 42);
        var generator = new MultiPathGenerator<SobolBrownianBridgeRsg>(jointProcess, grid, rsg, false);

        const int m = 90;
        GeneralStatistics[] zeroStat = [.. Enumerable.Range(0, m).Select(_ => new GeneralStatistics())];
        GeneralStatistics[] optionStat = [.. Enumerable.Range(0, m).Select(_ => new GeneralStatistics())];

        const int nrTrails = 8191;
        const int optionTenor = 24;
        const DiscountFactor strike = 0.5;

        for (int i = 0; i < nrTrails; ++i)
        {
            MultiPath path = generator.Next().Value;

            for (int j = 1; j < m; ++j)
            {
                double t = grid[j];            // zero end and option maturity
                double T = grid[j + optionTenor]; // maturity of zero bond of option

                var states = new Vector(3);
                var optionStates = new Vector(3);
                for (int k = 0; k < jointProcess.Size; ++k)
                {
                    states[k] = path[k][j];
                    optionStates[k] = path[k][j + optionTenor];
                }

                DiscountFactor zeroBond = 1.0 / jointProcess.Numeraire(t, states);
                DiscountFactor zeroOption = zeroBond * Math.Max(0.0, hwModel.DiscountBond(t, T, states[2]) - strike);

                zeroStat[j].Add(zeroBond);
                optionStat[j].Add(zeroOption);
            }
        }

        var errors = new List<string>();
        for (int j = 1; j < m; ++j)
        {
            double t = grid[j];
            Real calculated = zeroStat[j].Mean();
            Real expected = ts.Discount(t);

            if (Math.Abs(calculated - expected) > 0.03)
            {
                errors.Add(
                    $"Failed to reproduce expected zero bond prices\n   t:          {t}\n   calculated: {calculated}\n"
                    + $"   expected:   {expected}");
            }

            double T = grid[j + optionTenor];

            calculated = optionStat[j].Mean();
            expected = hwModel.DiscountBondOption(OptionType.Call, strike, t, T);

            if (Math.Abs(calculated - expected) > 0.0035)
            {
                errors.Add(
                    $"Failed to reproduce expected zero bond option prices\n   t:          {t}\n   T:          {T}\n"
                    + $"   calculated: {calculated}\n   expected:   {expected}");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestMcVanillaPricing()
    {
        // Testing Monte-Carlo vanilla option pricing...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        var context = new EvaluationContext { EvaluationDate = today };

        // construct a strange yield curve to check drifts and discounting
        // of the joint stochastic process

        var dates = new List<Date>();
        var rates = new List<Rate>();
        var divRates = new List<Rate>();

        for (int i = 0; i <= 40; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
            rates.Add(0.03 + (0.0003 * Math.Exp(Math.Sin(i / 4.0))));
            divRates.Add(0.02 + (0.0001 * Math.Exp(Math.Sin(i / 5.0))));
        }

        Date maturity = today + new Period(20, TimeUnit.Years);

        const Real s0 = 100;
        YieldTermStructure rTS = new ZeroCurve(dates, rates, dc);
        YieldTermStructure qTS = new ZeroCurve(dates, divRates, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.25, dc);

        var bsmProcess = new BlackScholesMertonProcess(s0, qTS, rTS, volTS);
        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.0625, 0.5, 0.0625, 1e-5, 0.3);
        HullWhiteForwardProcess hwProcess = new HullWhiteForwardProcess(rTS, 0.01, 0.01)
            .WithForwardMeasureTime(dc.YearFraction(today, maturity));

        const Real tol = 0.05;
        Real[] corr = [-0.9, -0.5, 0.0, 0.5, 0.9];
        Real[] strike = [100];

        foreach (Real i in corr)
        {
            foreach (Real j in strike)
            {
                var jointProcess = new HybridHestonHullWhiteProcess(hestonProcess, hwProcess, i);

                StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, j);
                Exercise exercise = new EuropeanExercise(maturity);

                var optionHestonHW = new VanillaOption(payoff, exercise);
                Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine =
                    new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(jointProcess, 42)
                        .WithSteps(1)
                        .WithAntitheticVariate()
                        .WithControlVariate()
                        .WithAbsoluteTolerance(tol)
                        .ToEngine();

                var hwModel = new HullWhite(rTS, hwProcess.A, hwProcess.Sigma);

                var optionBsmHW = new VanillaOption(payoff, exercise);

                OneAssetOptionResults hestonHW = engine(optionHestonHW, context);
                Real calculated = hestonHW.NPV!.Value;
                Real error = hestonHW.ErrorEstimate!.Value;
                Real expected = AnalyticBSMHullWhiteEngine.Calculate(optionBsmHW, context, i, bsmProcess, hwModel).NPV!.Value;

                Assert.False(
                    (i != 0.0 && Math.Abs(calculated - expected) > 3 * error) || (i == 0.0 && Math.Abs(calculated - expected) > 1e-4),
                    $"Failed to reproduce BSM-HW vanilla prices\n   corr:       {i}\n   strike:     {j}\n   calculated: {calculated}\n   error:      {error}\n   expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestMcPureHestonPricing()
    {
        // Testing Monte-Carlo Heston option pricing...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        var context = new EvaluationContext { EvaluationDate = today };

        // construct a strange yield curve to check drifts and discounting
        // of the joint stochastic process

        var dates = new List<Date>();
        var rates = new List<Rate>();
        var divRates = new List<Rate>();

        for (int i = 0; i <= 100; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Months));
            rates.Add(0.02 + (0.0002 * Math.Exp(Math.Sin(i / 10.0))));
            divRates.Add(0.02 + (0.0001 * Math.Exp(Math.Sin(i / 20.0))));
        }

        Date maturity = today + new Period(2, TimeUnit.Years);

        const Real s0 = 100;
        YieldTermStructure rTS = new ZeroCurve(dates, rates, dc);
        YieldTermStructure qTS = new ZeroCurve(dates, divRates, dc);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.08, 1.5, 0.0625, 0.5, -0.8);
        HullWhiteForwardProcess hwProcess = new HullWhiteForwardProcess(rTS, 0.1, 1e-8)
            .WithForwardMeasureTime(dc.YearFraction(today, maturity + new Period(1, TimeUnit.Years)));

        const Real tol = 0.001;
        Real[] corr = [-0.45, 0.45, 0.25];
        Real[] strike = [100, 75, 50, 150];

        foreach (Real i in corr)
        {
            foreach (Real j in strike)
            {
                var jointProcess = new HybridHestonHullWhiteProcess(
                    hestonProcess, hwProcess, i, HybridHestonHullWhiteProcessDiscretization.Euler);

                StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, j);
                Exercise exercise = new EuropeanExercise(maturity);

                var optionHestonHW = new VanillaOption(payoff, exercise);
                var optionPureHeston = new VanillaOption(payoff, exercise);

                Real expected = AnalyticHestonEngine.Calculate(optionPureHeston, context, new HestonModel(hestonProcess)).NPV!.Value;

                OneAssetOptionResults hestonHW = new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(jointProcess, 42)
                    .WithSteps(2)
                    .WithAntitheticVariate()
                    .WithControlVariate()
                    .WithAbsoluteTolerance(tol)
                    .ToEngine()(optionHestonHW, context);

                Real calculated = hestonHW.NPV!.Value;
                Real error = hestonHW.ErrorEstimate!.Value;

                Assert.False(
                    Math.Abs(calculated - expected) > 3 * error && Math.Abs(calculated - expected) > tol,
                    $"Failed to reproduce pure heston vanilla prices\n   corr:       {i}\n   strike:     {j}\n   calculated: {calculated}\n   error:      {error}\n   expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestAnalyticHestonHullWhitePricing()
    {
        // Testing analytic Heston Hull-White option pricing...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        var context = new EvaluationContext { EvaluationDate = today };

        // construct a strange yield curve to check drifts and discounting
        // of the joint stochastic process

        var dates = new List<Date>();
        var rates = new List<Rate>();
        var divRates = new List<Rate>();

        for (int i = 0; i <= 40; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
            rates.Add(0.03 + (0.0001 * Math.Exp(Math.Sin(i / 4.0))));
            divRates.Add(0.02 + (0.0002 * Math.Exp(Math.Sin(i / 3.0))));
        }

        Date maturity = today + new Period(5, TimeUnit.Years);
        const Real s0 = 100;
        YieldTermStructure rTS = new ZeroCurve(dates, rates, dc);
        YieldTermStructure qTS = new ZeroCurve(dates, divRates, dc);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.08, 1.5, 0.0625, 0.5, -0.8);
        var hestonModel = new HestonModel(hestonProcess);

        HullWhiteForwardProcess hwFwdProcess = new HullWhiteForwardProcess(rTS, 0.01, 0.01)
            .WithForwardMeasureTime(dc.YearFraction(today, maturity));
        var hullWhiteModel = new HullWhite(rTS, hwFwdProcess.A, hwFwdProcess.Sigma);

        const Real tol = 0.002;
        Real[] strike = [80, 120];
        OptionType[] types = [OptionType.Put, OptionType.Call];

        foreach (OptionType type in types)
        {
            foreach (Real j in strike)
            {
                var jointProcess = new HybridHestonHullWhiteProcess(
                    hestonProcess, hwFwdProcess, 0.0, HybridHestonHullWhiteProcessDiscretization.Euler);

                StrikedTypePayoff payoff = new PlainVanillaPayoff(type, j);
                Exercise exercise = new EuropeanExercise(maturity);

                var optionHestonHW = new VanillaOption(payoff, exercise);
                Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine =
                    new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(jointProcess, 42)
                        .WithSteps(1)
                        .WithAntitheticVariate()
                        .WithControlVariate()
                        .WithAbsoluteTolerance(tol)
                        .ToEngine();

                var optionPureHeston = new VanillaOption(payoff, exercise);

                OneAssetOptionResults hestonHW = engine(optionHestonHW, context);
                Real calculated = hestonHW.NPV!.Value;
                Real error = hestonHW.ErrorEstimate!.Value;
                Real expected = AnalyticHestonHullWhiteEngine.Calculate(optionPureHeston, context, hestonModel, hullWhiteModel, 128).NPV!.Value;

                Assert.False(
                    Math.Abs(calculated - expected) > 3 * error && Math.Abs(calculated - expected) > tol,
                    $"Failed to reproduce hw heston vanilla prices\n   strike:     {j}\n   calculated: {calculated}\n   error:      {error}\n   expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestCallableEquityPricing()
    {
        // Testing the pricing of a callable equity product...

        /*
           For the definition of the example product see
           Alexander Giese, On the Pricing of Auto-Callable Equity
           Structures in the Presence of Stochastic Volatility and
           Stochastic Interest Rates .
           http://workshop.mathfinance.de/2006/papers/giese/slides.pdf
        */

        const int maturity = 7;
        DayCounter dc = new Actual365Fixed();
        Date today = Date.TodaysDate();

        var context = new EvaluationContext { EvaluationDate = today };

        const Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.04, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.04, dc);

        var hestonProcess = new HestonProcess(rTS, qTS, spot, 0.0625, 1.0, 0.24 * 0.24, 1e-4, 0.0);
        HullWhiteForwardProcess hwProcess = new HullWhiteForwardProcess(rTS, 0.00883, 0.00526)
            .WithForwardMeasureTime(dc.YearFraction(today, today + new Period(maturity + 1, TimeUnit.Years)));

        var jointProcess = new HybridHestonHullWhiteProcess(hestonProcess, hwProcess, -0.4);

        var schedule = new Schedule(
            today,
            today + new Period(maturity, TimeUnit.Years),
            new Period(1, TimeUnit.Years),
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Following,
            DateGenerationRule.Forward,
            false,
            context);

        double[] times = new double[maturity + 1];
        for (int i = 0; i < times.Length; ++i)
        {
            times[i] = dc.YearFraction(today, schedule[i]);
        }

        for (int i = 0; i <= maturity; ++i)
        {
            times[i] = i;
        }

        var grid = new TimeGrid(times);

        double[] redemption = new double[maturity];
        for (int i = 0; i < maturity; ++i)
        {
            redemption[i] = 1.07 + (0.03 * i);
        }

        const long seed = 42;
        IRsg rsg = PseudoRandom.MakeSequenceGenerator(jointProcess.Factors * (grid.Size - 1), seed);

        var generator = new MultiPathGenerator<IRsg>(jointProcess, grid, rsg, false);
        var stat = new GeneralStatistics();

        Real antitheticPayoff = 0;
        const int nrTrails = 40000;
        for (int i = 0; i < nrTrails; ++i)
        {
            bool antithetic = (i % 2) != 0;

            Sample<MultiPath> path = antithetic ? generator.Antithetic() : generator.Next();

            Real payoff = 0;
            for (int j = 1; j <= maturity; ++j)
            {
                if (path.Value[0][j] > spot)
                {
                    var states = new Vector(3);
                    for (int k = 0; k < 3; ++k)
                    {
                        states[k] = path.Value[k][j];
                    }

                    payoff = redemption[j - 1] / jointProcess.Numeraire(grid[j], states);
                    break;
                }
                else if (j == maturity)
                {
                    var states = new Vector(3);
                    for (int k = 0; k < 3; ++k)
                    {
                        states[k] = path.Value[k][j];
                    }

                    payoff = 1.0 / jointProcess.Numeraire(grid[j], states);
                }
            }

            if (antithetic)
            {
                stat.Add(0.5 * (antitheticPayoff + payoff));
            }
            else
            {
                antitheticPayoff = payoff;
            }
        }

        const Real expected = 0.938;
        Real calculated = stat.Mean();
        Real error = stat.ErrorEstimate();

        Assert.False(
            Math.Abs(expected - calculated) > 3 * error,
            $"Failed to reproduce auto-callable equity structure price\n   calculated: {calculated}\n   error:      {error}\n   expected:   {expected}");
    }

    [Fact]
    public void TestDiscretizationError()
    {
        // Testing the discretization error of the Heston Hull-White process...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        var context = new EvaluationContext { EvaluationDate = today };

        // construct a strange yield curve to check drifts and discounting
        // of the joint stochastic process

        var dates = new List<Date>();
        var rates = new List<Rate>();
        var divRates = new List<Rate>();

        for (int i = 0; i <= 31; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
            rates.Add(0.04 + (0.0001 * Math.Exp(Math.Sin((double)i))));
            divRates.Add(0.04 + (0.0001 * Math.Exp(Math.Sin((double)i))));
        }

        Date maturity = today + new Period(10, TimeUnit.Years);
        const Volatility v = 0.25;

        const Real s0 = 100;
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), v, dc);
        YieldTermStructure rTS = new ZeroCurve(dates, rates, dc);
        YieldTermStructure qTS = new ZeroCurve(dates, divRates, dc);

        var bsmProcess = new BlackScholesMertonProcess(s0, qTS, rTS, volTS);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v * v, 1, v * v, 1e-6, -0.4);

        HullWhiteForwardProcess hwProcess = new HullWhiteForwardProcess(rTS, 0.01, 0.01)
            .WithForwardMeasureTime(20.1472222222222222);

        const Real tol = 0.05;
        Real[] corr = [-0.85, 0.5];
        Real[] strike = [50, 100, 125];

        foreach (Real i in corr)
        {
            foreach (Real j in strike)
            {
                StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, j);
                Exercise exercise = new EuropeanExercise(maturity);

                var optionBsmHW = new VanillaOption(payoff, exercise);
                var hwModel = new HullWhite(rTS, hwProcess.A, hwProcess.Sigma);

                Real expected = AnalyticBSMHullWhiteEngine.Calculate(optionBsmHW, context, i, bsmProcess, hwModel).NPV!.Value;

                var optionHestonHW = new VanillaOption(payoff, exercise);
                var jointProcess = new HybridHestonHullWhiteProcess(hestonProcess, hwProcess, i);
                OneAssetOptionResults hestonHW = new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(jointProcess, 42)
                    .WithSteps(1)
                    .WithAntitheticVariate()
                    .WithAbsoluteTolerance(tol)
                    .ToEngine()(optionHestonHW, context);

                Real calculated = hestonHW.NPV!.Value;
                Real error = hestonHW.ErrorEstimate!.Value;

                Assert.False(
                    Math.Abs(calculated - expected) > 3 * error && Math.Abs(calculated - expected) > 1e-5,
                    $"Failed to reproduce discretization error\n   corr:       {i}\n   strike:     {j}\n   calculated: {calculated}\n   error:      {error}\n   expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestFdmHestonHullWhiteEngine()
    {
        // Testing the FDM Heston Hull-White engine...
        var today = new Date(28, Month.March, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        var exerciseDate = new Date(28, Month.March, 2012);
        DayCounter dc = new Actual365Fixed();

        const Real s0 = 100.0;

        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), 0.05, dc, context);
        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), 0.02, dc, context);

        const Volatility vol = 0.30;
        BlackVolTermStructure volTS = BlackConstantVol.Create(0, new NullCalendar(), vol, dc, context);

        const Real v0 = vol * vol;
        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, 1.0, v0, 0.000001, 0.0);

        var stochProcess = new BlackScholesMertonProcess(s0, qTS, rTS, volTS);

        var hwProcess = new HullWhiteProcess(rTS, 0.00883, 0.01);
        var hwModel = new HullWhite(rTS, hwProcess.A, hwProcess.Sigma);

        Exercise exercise = new EuropeanExercise(exerciseDate);
        Real[] corr = [-0.85, 0.5];
        Real[] strike = [75, 120, 160];

        foreach (Real i in corr)
        {
            foreach (Real j in strike)
            {
                StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, j);
                var option = new VanillaOption(payoff, exercise);

                OneAssetOptionResults fd = FdHestonHullWhiteVanillaEngine.Calculate(
                    option, context, new HestonModel(hestonProcess), hwProcess, i, 50, 200, 10, 15);
                Real calculated = fd.NPV!.Value;
                Real calculatedDelta = fd.Delta!.Value;
                Real calculatedGamma = fd.Gamma!.Value;

                OneAssetOptionResults analytic = AnalyticBSMHullWhiteEngine.Calculate(option, context, i, stochProcess, hwModel);
                Real expected = analytic.NPV!.Value;
                Real expectedDelta = analytic.Delta!.Value;
                Real expectedGamma = analytic.Gamma!.Value;

                const Real npvTol = 0.01;
                Assert.False(
                    Math.Abs(calculated - expected) > npvTol,
                    $"Failed to reproduce analytic npv values\n   corr:       {i}\n   strike:     {j}\n   calculated: {calculated}\n   expected:   {expected}");
                const Real deltaTol = 0.001;
                Assert.False(
                    Math.Abs(calculatedDelta - expectedDelta) > deltaTol,
                    $"Failed to reproduce analytic delta values\n   corr:       {i}\n   strike:     {j}\n   calculated: {calculated}\n   expected:   {expected}");
                const Real gammaTol = 0.001;
                Assert.False(
                    Math.Abs(calculatedGamma - expectedGamma) > gammaTol,
                    $"Failed to reproduce analytic gamma values\n   corr:       {i}\n   strike:     {j}\n   calculated: {calculated}\n   expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestBsmHullWhitePricing()
    {
        // Testing convergence speed of Heston-Hull-White engine...
        var today = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real maturity = 5.0;
        const Real equityIrCorr = -0.4;
        Real[] strikes = [75, 85, 90, 95, 100, 105, 110, 115, 120, 125, 130, 140, 150];
        int[] listOfTimeStepsPerYear = [20];

        var hestonModelData = new HestonModelData("BSM-HW Model", 0.09, 1.0, 0.09, QL_EPSILON, 0.0, 0.04, 0.03);
        HullWhiteModelData hwModelData = HullWhiteModels[0];
        bool[] controlVariate = [true, false];

        HestonProcess hp = MakeHestonProcess(hestonModelData, context);
        var hestonModel = new HestonModel(hp);

        var hwProcess = new HullWhiteProcess(hp.RiskFreeRate, hwModelData.A, hwModelData.Sigma);
        var hullWhiteModel = new HullWhite(hp.RiskFreeRate, hwProcess.A, hwProcess.Sigma);

        var bsmProcess = new BlackScholesMertonProcess(
            hp.S0,
            hp.DividendYield,
            hp.RiskFreeRate,
            new BlackConstantVol(today, new NullCalendar(), Math.Sqrt(hestonModelData.Theta), hp.RiskFreeRate.DayCounter));

        Real[] tolWithCV = [2e-4, 2e-4, 2e-4, 2e-4, 0.01];
        Real[] tolWithOutCV = [5e-3, 5e-3, 5e-3, 5e-3, 0.02];
        for (int l = 0; l < Schemes.Length; ++l)
        {
            SchemeData scheme = Schemes[l];
            foreach (bool i in controlVariate)
            {
                foreach (int u in listOfTimeStepsPerYear)
                {
                    int tSteps = (int)(maturity * u);

                    // QuantLib's engine with enableMultipleStrikesCaching(strikes), its cache filled by the first
                    // strike's option: CalculateStrikes on it (the S-hes plan's HR4 (ii))
                    ImmutableArray<OneAssetOptionResults> fdResults = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
                        MakeVanillaOption(new VanillaOptionData(strikes[0], maturity, OptionType.Call), context),
                        strikes,
                        context,
                        hestonModel,
                        hwProcess,
                        equityIrCorr,
                        tSteps,
                        400,
                        2,
                        10,
                        0,
                        i,
                        scheme.SchemeDesc);

                    Real avgPriceDiff = 0.0;
                    for (int k = 0; k < strikes.Length; ++k)
                    {
                        var optionData = new VanillaOptionData(strikes[k], maturity, OptionType.Call);
                        VanillaOption option = MakeVanillaOption(optionData, context);
                        Real expected = AnalyticBSMHullWhiteEngine.Calculate(option, context, equityIrCorr, bsmProcess, hullWhiteModel).NPV!.Value;

                        Real calculated = fdResults[k].NPV!.Value;
                        avgPriceDiff += Math.Abs(expected - calculated) / strikes.Length;
                    }

                    Assert.False(
                        i && tolWithCV[l] < avgPriceDiff,
                        $"Failed to reproduce BSM-Hull-White prices\n   scheme       : {scheme.Name}\n   model        : {hestonModelData.Name}\n   CV           : on");

                    Assert.False(
                        !i && tolWithOutCV[l] < avgPriceDiff,
                        $"Failed to reproduce BSM-Hull-White prices\n   scheme       : {scheme.Name}\n   model        : {hestonModelData.Name}\n   CV           : off");
                }
            }
        }
    }

    [Fact]
    public void TestSpatialDiscretizatinError()
    {
        // Testing spatial convergence speed of Heston engine...
        var today = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real maturity = 1.0;
        Real[] strikes = [75, 85, 90, 95, 100, 105, 110, 115, 120, 125, 130, 140, 150];
        int[] listOfTimeStepsPerYear = [40];

        Real[] tol = [0.02, 0.02, 0.02, 0.02, 0.05];
        foreach (int u in listOfTimeStepsPerYear)
        {
            for (int i = 0; i < Schemes.Length; ++i)
            {
                foreach (HestonModelData j in HestonModels)
                {
                    Real avgPriceDiff = 0;
                    HestonProcess hestonProcess = MakeHestonProcess(j, context);
                    var hestonModel = new HestonModel(hestonProcess);

                    int tSteps = (int)(maturity * u);

                    // QuantLib's engine with enableMultipleStrikesCaching(strikes), its cache filled by the first
                    // strike's option: CalculateStrikes on it (the S-hes plan's HR4 (ii))
                    ImmutableArray<OneAssetOptionResults> fdResults =
                        FdHestonVanillaEngine.CalculateStrikes(
                            MakeVanillaOption(new VanillaOptionData(strikes[0], maturity, OptionType.Call), context),
                            strikes,
                            context,
                            hestonModel,
                            tSteps,
                            200,
                            40,
                            0,
                            Schemes[i].SchemeDesc);

                    for (int k = 0; k < strikes.Length; ++k)
                    {
                        var optionData = new VanillaOptionData(strikes[k], maturity, OptionType.Call);
                        VanillaOption option = MakeVanillaOption(optionData, context);
                        Real expected = AnalyticHestonEngine.Calculate(option, context, hestonModel, 172).NPV!.Value;

                        Real calculated = fdResults[k].NPV!.Value;

                        avgPriceDiff += Math.Abs(expected - calculated) / strikes.Length;
                    }

                    Assert.False(
                        avgPriceDiff > tol[i],
                        $"\nFailed to reproduce Heston prices\n scheme    : {Schemes[i].Name}\n model     : {j.Name}\n error     : {avgPriceDiff}\n tolerance : {tol[i]}");
                }
            }
        }
    }

    [Fact]
    public void TestHestonHullWhiteCalibration()
    {
        // Testing the Heston Hull-White calibration...

        // Calibration of a hybrid Heston-Hull-White model using
        // the finite difference HestonHullWhite pricing engine
        //
        // Input surface is based on a Heston-Hull-White model with
        // Hull-White: a = 0.00883, \sigma = 0.00631
        // Heston    : \nu = 0.12, \kappa = 2.0,
        //             \theta = 0.09, \sigma = 0.5, \rho=-0.75
        // Equity Short rate correlation: -0.5

        DayCounter dc = new Actual365Fixed();
        Calendar calendar = new TARGET();
        var today = new Date(28, Month.March, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), 0.05, dc, context);

        // assuming, that the Hull-White process is already calibrated
        // on a given set of pure interest rate calibration instruments.
        var hwProcess = new HullWhiteProcess(rTS, 0.00883, 0.00631);
        var hullWhiteModel = new HullWhite(rTS, hwProcess.A, hwProcess.Sigma);

        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), 0.02, dc, context);
        const Real s0 = 100.0;

        // starting point of the pure Heston calibration
        const Real start_v0 = 0.2 * 0.2;
        const Real start_theta = start_v0;
        const Real start_kappa = 0.5;
        const Real start_sigma = 0.25;
        const Real start_rho = -0.5;

        var hestonProcess = new HestonProcess(rTS, qTS, s0, start_v0, start_kappa, start_theta, start_sigma, start_rho);
        var analyticHestonModel = new HestonModel(hestonProcess);
        var fdmHestonModel = new HestonModel(hestonProcess);

        const Real equityShortRateCorr = -0.5;

        Real[] strikes = [50, 75, 90, 100, 110, 125, 150, 200];
        double[] maturities = [1 / 12.0, 3 / 12.0, 0.5, 1.0, 2.0, 3.0, 5.0, 7.5, 10];

        Volatility[] vol =
        [
            0.482627, 0.407617, 0.366682, 0.340110, 0.314266, 0.280241, 0.252471, 0.325552,
            0.464811, 0.393336, 0.354664, 0.329758, 0.305668, 0.273563, 0.244024, 0.244886,
            0.441864, 0.375618, 0.340464, 0.318249, 0.297127, 0.268839, 0.237972, 0.225553,
            0.407506, 0.351125, 0.322571, 0.305173, 0.289034, 0.267361, 0.239315, 0.213761,
            0.366761, 0.326166, 0.306764, 0.295279, 0.284765, 0.270592, 0.250702, 0.222928,
            0.345671, 0.314748, 0.300259, 0.291744, 0.283971, 0.273475, 0.258503, 0.235683,
            0.324512, 0.303631, 0.293981, 0.288338, 0.283193, 0.276248, 0.266271, 0.250506,
            0.311278, 0.296340, 0.289481, 0.285482, 0.281840, 0.276924, 0.269856, 0.258609,
            0.303219, 0.291534, 0.286187, 0.283073, 0.280239, 0.276414, 0.270926, 0.262173,
        ];

        var options = new List<CalibrationHelper>();

        for (int i = 0; i < maturities.Length; ++i)
        {
            var maturity = new Period((int)Math.Round(maturities[i] * 12.0, MidpointRounding.AwayFromZero), TimeUnit.Months);
            Exercise exercise = new EuropeanExercise(today + maturity);

            for (int j = 0; j < strikes.Length; ++j)
            {
                StrikedTypePayoff payoff = new PlainVanillaPayoff(
                    strikes[j] * rTS.Discount(maturities[i]) >= s0 * qTS.Discount(maturities[i])
                        ? OptionType.Call
                        : OptionType.Put,
                    strikes[j]);

                var helper = new HestonModelHelper(
                    maturity, calendar, s0, strikes[j], vol[(i * strikes.Length) + j], rTS, qTS, context,
                    BlackCalibrationHelperCalibrationErrorType.PriceError);
                Real marketValue = helper.MarketValue;

                // Improve the quality of the starting point
                // for the full Heston-Hull-White calibration
                var bsProcess = new GeneralizedBlackScholesProcess(
                    s0, qTS, rTS, BlackConstantVol.Create(0, new NullCalendar(), vol[(i * strikes.Length) + j], dc, context));

                var dummyOption = new VanillaOption(payoff, exercise);

                Volatility vt = ImpliedVolatility(
                    dummyOption, context, bsProcess, equityShortRateCorr, hullWhiteModel, marketValue, 1e-8, 100, 0.0001, 10);

                // QuantLib's v.linkTo(vt): the helper at the new volatility, its market value recomputed
                helper = new HestonModelHelper(
                    maturity, calendar, s0, strikes[j], vt, rTS, qTS, context,
                    BlackCalibrationHelperCalibrationErrorType.PriceError);

                options.Add(helper.SetPricingEngine<HestonModel>(
                    (option, c, m) => AnalyticHestonEngine.Calculate(option, c, m, 164)));
            }
        }

        var corrConstraint = new HestonHullWhiteCorrelationConstraint(equityShortRateCorr);
        var om = new LevenbergMarquardt(1e-6, 1e-8, 1e-8);
        analyticHestonModel = analyticHestonModel.Calibrate(
            options, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-4, 1.0e-8), context, corrConstraint).Model;

        options.Clear();
        fdmHestonModel = fdmHestonModel.SetParams(analyticHestonModel.Params());

        for (int i = 0; i < maturities.Length; ++i)
        {
            int tGrid = (int)Math.Max(5.0, maturities[i] * 5.0);
            Func<VanillaOption, EvaluationContext, HestonModel, InstrumentResults> engine =
                MultipleStrikesEngine(hwProcess, equityShortRateCorr, strikes, tGrid);

            var maturity = new Period((int)Math.Round(maturities[i] * 12.0, MidpointRounding.AwayFromZero), TimeUnit.Months);

            for (int j = 0; j < strikes.Length; ++j)
            {
                // multiple strikes engine works best if the first option
                // per maturity has the average strike (because the first option
                // is priced first during the calibration and the first pricing
                // is used to calculate the prices for all strikes
                int js = (j + ((strikes.Length - 1) / 2)) % strikes.Length;

                var helper = new HestonModelHelper(
                    maturity, calendar, s0, strikes[js], vol[(i * strikes.Length) + js], rTS, qTS, context,
                    BlackCalibrationHelperCalibrationErrorType.PriceError);

                options.Add(helper.SetPricingEngine(engine));
            }
        }

        var vm = new LevenbergMarquardt(1e-6, 1e-2, 1e-2);
        fdmHestonModel = fdmHestonModel.Calibrate(
            options, vm, new EndCriteria(400, 40, 1.0e-8, 1.0e-4, 1.0e-8), context, corrConstraint).Model;

        const Real relTol = 0.01;
        const Real expected_v0 = 0.12;
        const Real expected_kappa = 2.0;
        const Real expected_theta = 0.09;
        const Real expected_sigma = 0.5;
        const Real expected_rho = -0.75;

        Assert.False(
            Math.Abs(fdmHestonModel.V0 - expected_v0) / expected_v0 > relTol,
            $"Failed to reproduce Heston-Hull-White model\n   v0 calculated: {fdmHestonModel.V0}\n   v0 expected  : {expected_v0}\n   relatove tol : {relTol}");
        Assert.False(
            Math.Abs(fdmHestonModel.Theta - expected_theta) / expected_theta > relTol,
            $"Failed to reproduce Heston-Hull-White model\n   theta calculated: {fdmHestonModel.Theta}\n   theta expected  : {expected_theta}\n   relatove tol    : {relTol}");
        Assert.False(
            Math.Abs(fdmHestonModel.Kappa - expected_kappa) / expected_kappa > relTol,
            $"Failed to reproduce Heston-Hull-White model\n   kappa calculated: {fdmHestonModel.Kappa}\n   kappa expected  : {expected_kappa}\n   relatove tol    : {relTol}");
        Assert.False(
            Math.Abs(fdmHestonModel.Sigma - expected_sigma) / expected_sigma > relTol,
            $"Failed to reproduce Heston-Hull-White model\n   sigma calculated: {fdmHestonModel.Sigma}\n   sigma expected  : {expected_sigma}\n   relatove tol    : {relTol}");
        Assert.False(
            Math.Abs(fdmHestonModel.Rho - expected_rho) / expected_rho > relTol,
            $"Failed to reproduce Heston-Hull-White model\n   rho calculated: {fdmHestonModel.Rho}\n   rho expected  : {expected_rho}\n   relatove tol  : {relTol}");
    }

    [Fact]
    public void TestH1HWPricingEngine()
    {
        // Testing the H1-HW approximation engine...

        /*
         * Example taken from Lech Aleksander Grzelak,
         * Equity and Foreign Exchange Hybrid Models for Pricing Long-Maturity
         * Financial Derivatives,
         * http://repository.tudelft.nl/assets/uuid:a8e1a007-bd89-481a-aee3-0e22f15ade6b/PhDThesis_main.pdf
        */

        var today = new Date(15, Month.July, 2012);
        var context = new EvaluationContext { EvaluationDate = today };
        var exerciseDate = new Date(13, Month.July, 2022);
        DayCounter dc = new Actual365Fixed();

        Exercise exercise = new EuropeanExercise(exerciseDate);

        const Real s0 = 100.0;

        const Real r = 0.02;
        const Real q = 0.00;
        const Real v0 = 0.05;
        const Real theta = 0.05;
        const Real kappa_v = 0.3;
        Real[] sigma_v = [0.3, 0.6];
        const Real rho_sv = -0.30;
        const Real rho_sr = 0.6;
        const Real kappa_r = 0.01;
        const Real sigma_r = 0.01;

        YieldTermStructure rTS = new FlatForward(today, r, dc);
        YieldTermStructure qTS = new FlatForward(today, q, dc);

        BlackVolTermStructure flatVolTS = new BlackConstantVol(today, new NullCalendar(), 0.20, dc);
        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, flatVolTS);

        var hwProcess = new HullWhiteProcess(rTS, kappa_r, sigma_r);
        var hullWhiteModel = new HullWhite(rTS, kappa_r, sigma_r);

        const Real tol = 0.0001;
        Real[] strikes = [40, 80, 100, 120, 180];
        Real[][] expected =
        [
            [0.267503, 0.235742, 0.228223, 0.223461, 0.217855],
            [0.263626, 0.211625, 0.199907, 0.193502, 0.190025],
        ];

        for (int j = 0; j < sigma_v.Length; ++j)
        {
            var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa_v, theta, sigma_v[j], rho_sv);
            var hestonModel = new HestonModel(hestonProcess);

            for (int i = 0; i < strikes.Length; ++i)
            {
                StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, strikes[i]);

                var option = new VanillaOption(payoff, exercise);

                Real npv = AnalyticH1HWEngine.Calculate(option, context, hestonModel, hullWhiteModel, rho_sr, 144).NPV!.Value;
                Real impliedH1HW = option.ImpliedVolatility(npv, bsProcess, context);

                Assert.False(
                    Math.Abs(expected[j][i] - impliedH1HW) > tol,
                    $"Failed to reproduce H1HW implied volatility\n   expected       : {expected[j][i]}\n   calculated     : {impliedH1HW}\n   tol            : {tol}\n   strike         : {strikes[i]}\n   sigma          : {sigma_v[j]}");
            }
        }

        _ = hwProcess;
    }

    // QuantLib's engine with enableMultipleStrikesCaching(strikes), shared by one maturity's helpers, as a pricing
    // delegate (the S-hes plan's HR4 (ii)): its cache emptied when the calibration hands it another model, QuantLib's
    // update(); a helper whose option type matches the last solve's reads its strike from that solve, any other solves
    // again with its own option as the reference, as QuantLib's cache lookup does
    // (fdhestonhullwhitevanillaengine.cpp:78-93)
    private static Func<VanillaOption, EvaluationContext, HestonModel, InstrumentResults> MultipleStrikesEngine(
        HullWhiteProcess hwProcess, double corrEquityShortRate, Real[] strikes, int tGrid)
    {
        HestonModel? cachedModel = null;
        OptionType cachedType = default;
        ImmutableArray<OneAssetOptionResults> cached = [];
        return (option, c, m) =>
        {
            var payoff = (PlainVanillaPayoff)option.Payoff!;
            if (!ReferenceEquals(cachedModel, m) || payoff.OptionType != cachedType)
            {
                cached = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
                    option, strikes, c, m, hwProcess, corrEquityShortRate, tGrid, 45, 11, 5, 0, true);
                cachedModel = m;
                cachedType = payoff.OptionType;
            }

            return cached[Array.IndexOf(strikes, payoff.Strike)];
        };
    }

    // QuantLib's detail::ImpliedVolatilityHelper::calculate over the analytic BSM-Hull-White engine, on a clone of the
    // process at each volatility (ImpliedVolatilityHelper::clone): Brent from the bracket's midpoint
    private static double ImpliedVolatility(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double corr,
        HullWhite hullWhiteModel,
        double targetValue,
        double accuracy,
        int maxEvaluations,
        double minVol,
        double maxVol)
    {
        option.Validate();

        var f = new BsmHullWhitePriceError(option, context, process, corr, hullWhiteModel, targetValue);
        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);
        double guess = (minVol + maxVol) / 2.0;
        return solver.Solve(f, accuracy, guess, minVol, maxVol);
    }

    // The test suite's makeHestonProcess.
    private static HestonProcess MakeHestonProcess(HestonModelData parameters, EvaluationContext context)
    {
        Real spot = 100;

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), parameters.R, dayCounter, context);
        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), parameters.Q, dayCounter, context);

        return new HestonProcess(
            rTS, qTS, spot, parameters.V0, parameters.Kappa, parameters.Theta, parameters.Sigma, parameters.Rho);
    }

    // The test suite's makeVanillaOption.
    private static VanillaOption MakeVanillaOption(VanillaOptionData parameters, EvaluationContext context)
    {
        Date maturity = context.EvaluationDate + new Period((int)(parameters.Maturity * 365), TimeUnit.Days);
        var exercise = new EuropeanExercise(maturity);
        var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);

        return new VanillaOption(payoff, exercise);
    }

    // The test suite's HestonHullWhiteCorrelationConstraint.
    private sealed class HestonHullWhiteCorrelationConstraint(Real equityShortRateCorr) : Constraint
    {
        public override bool Test(Vector p)
        {
            Real rho = p[3];

            return (rho * rho) + (equityShortRateCorr * equityShortRateCorr) <= 1.0;
        }
    }

    // ImpliedVolatilityHelper's PriceError: the BSM-Hull-White engine's value at a volatility, less the target.
    private readonly struct BsmHullWhitePriceError(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double corr,
        HullWhite hullWhiteModel,
        double targetValue) : ISolver1DFunction
    {
        public double Evaluate(double x)
        {
            BlackVolTermStructure blackVol = process.BlackVolatility;
            var flat = new BlackConstantVol(blackVol.ReferenceDate, blackVol.Calendar, x, blackVol.DayCounter);
            var clone = new GeneralizedBlackScholesProcess(process.StateVariable, process.DividendYield, process.RiskFreeRate, flat);
            return AnalyticBSMHullWhiteEngine.Calculate(option, context, corr, clone, hullWhiteModel).NPV!.Value - targetValue;
        }
    }

    // The test suite's HestonModelData.
    private readonly record struct HestonModelData(
        string Name, Real V0, Real Kappa, Real Theta, Real Sigma, Real Rho, Real R, Real Q);

    // The test suite's HullWhiteModelData.
    private readonly record struct HullWhiteModelData(string Name, Real A, Real Sigma);

    // The test suite's SchemeData.
    private readonly record struct SchemeData(string Name, FdmSchemeDesc SchemeDesc);

    // The test suite's VanillaOptionData.
    private readonly record struct VanillaOptionData(Real Strike, double Maturity, OptionType OptionType);
}
