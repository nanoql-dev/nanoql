// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/forwardoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
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
public class ForwardOptionTests
{
    [Fact]
    public void TestValues()
    {
        // Testing forward option values...

        /* The data below are from
           "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
        */
        ForwardOptionData[] values =
        [
            // type, moneyness, spot,  div, rate,start,   t,  vol, result, tol
            // "Option pricing formulas", pag. 37
            new(OptionType.Call, 1.1, 60.0, 0.04, 0.08, 0.25, 1.0, 0.30, 4.4064, 1.0e-4),

            // "Option pricing formulas", VBA code
            new(OptionType.Put, 1.1, 60.0, 0.04, 0.08, 0.25, 1.0, 0.30, 8.2971, 1.0e-4),
        ];

        CheckValues(values, ForwardVanillaEngine.Calculate);
    }

    [Fact]
    public void TestPerformanceValues()
    {
        // Testing forward performance option values...

        /* The data below are the performance equivalent of the
           forward options tested above and taken from
           "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
        */
        ForwardOptionData[] values =
        [
            // type, moneyness, spot,  div, rate,start, maturity,  vol,                       result, tol
            new(OptionType.Call, 1.1, 60.0, 0.04, 0.08, 0.25, 1.0, 0.30, 4.4064 / 60 * Math.Exp(-0.04 * 0.25), 1.0e-4),
            new(OptionType.Put, 1.1, 60.0, 0.04, 0.08, 0.25, 1.0, 0.30, 8.2971 / 60 * Math.Exp(-0.04 * 0.25), 1.0e-4),
        ];

        CheckValues(values, ForwardPerformanceVanillaEngine.Calculate);
    }

    [Fact]
    public void TestGreeks()
    {
        // Testing forward option greeks...
        TestForwardGreeks(ForwardVanillaEngine.Calculate);
    }

    [Fact]
    public void TestPerformanceGreeks()
    {
        // Testing forward performance option greeks...
        TestForwardGreeks(ForwardPerformanceVanillaEngine.Calculate);
    }

    [Fact]
    public void TestGreeksInitialization()
    {
        // Testing forward option greeks initialization...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        GeneralizedBlackScholesProcess stochProcess = Process(100.0, 0.04, 0.01, 0.11, dc, context);

        // QuantLib's TestBinomialEngine: Cox-Ross-Rubinstein, fixed at 300 steps
        static OneAssetOptionResults TestBinomialEngine(
            VanillaOption o, EvaluationContext c, GeneralizedBlackScholesProcess p) =>
            BinomialCRRVanillaEngine.Calculate(o, c, p, 300);

        Date exDate = today + new Period(1, TimeUnit.Years);
        var exercise = new EuropeanExercise(exDate);
        Date reset = today + new Period(6, TimeUnit.Months);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 0.0);

        var option = new ForwardVanillaOption(0.9, reset, payoff, exercise);
        OneAssetOptionResults forward =
            ForwardVanillaEngine.Calculate(option, context, stochProcess, TestBinomialEngine);

        var ctrloption = new VanillaOption(payoff, exercise);
        OneAssetOptionResults control = TestBinomialEngine(ctrloption, context, stochProcess);

        // if normal option can't calculate a Greek, nor should forward (QuantLib's refusal of a null result is
        // nanoql's null)
        if (control.Delta is null)
        {
            Assert.True(forward.Delta is null, "Forward delta invalid");
        }

        if (control.Rho is null)
        {
            Assert.True(forward.Rho is null, "Forward rho invalid");
        }

        if (control.DividendRho is null)
        {
            Assert.True(forward.DividendRho is null, "Forward dividendRho invalid");
        }

        if (control.Vega is null)
        {
            Assert.True(forward.Vega is null, "Forward vega invalid");
        }
    }

    [Fact]
    public void TestMCPrices()
    {
        // Testing forward option MC prices...
        Real[] tol = [0.002, 0.001, 0.0006, 5e-4, 5e-4];

        int timeSteps = 100;
        int numberOfSamples = 5000;
        long mcSeed = 42;

        Real q = 0.04;
        Real r = 0.01;
        Real sigma = 0.11;
        Real s = 100;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        GeneralizedBlackScholesProcess stochProcess = Process(s, q, r, sigma, dc, context);

        Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> mcEngine =
            new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(stochProcess, mcSeed)
                .WithSteps(timeSteps)
                .WithSamples(numberOfSamples)
                .ToEngine();

        Date exDate = today + new Period(1, TimeUnit.Years);
        var exercise = new EuropeanExercise(exDate);
        Date reset = today + new Period(6, TimeUnit.Months);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 0.0);

        Real[] moneyness = [0.8, 0.9, 1.0, 1.1, 1.2];

        for (int moneynessIndex = 0; moneynessIndex < moneyness.Length; ++moneynessIndex)
        {
            var option = new ForwardVanillaOption(moneyness[moneynessIndex], reset, payoff, exercise);

            Real analyticPrice = ForwardVanillaEngine.Calculate(
                option, context, stochProcess, AnalyticEuropeanEngine.Calculate).NPV!.Value;

            Real mcPrice = mcEngine(option, context).NPV!.Value;

            Real error = RelativeError(analyticPrice, mcPrice, s);
            if (error > tol[moneynessIndex])
            {
                ReportFailure(
                    "testMCPrices", payoff, exercise, s, q, r, today, sigma, moneyness[moneynessIndex], reset, analyticPrice,
                    mcPrice, error, tol[moneynessIndex]);
            }
        }
    }

    [Fact]
    public void TestHestonMCPrices()
    {
        // Testing forward option Heston MC prices...
        OptionType[] optionTypes = [OptionType.Call, OptionType.Put];
        Real[][] mcForwardStartTolerance =
        [
            [7e-4, // Call, moneyness=0.8
                8e-4, // Call, moneyness=0.9
                6e-4, // Call, moneyness=1.0
                5e-4, // Call, moneyness=1.1
                5e-4], // Call, moneyness=1.2
            [6e-4, // Put, moneyness=0.8
                5e-4, // Put, moneyness=0.9
                6e-4, // Put, moneyness=1.0
                0.001, // Put, moneyness=1.1
                0.001], // Put, moneyness=1.2
        ];

        Real[][] tol =
        [
            [9e-4, // Call, moneyness=0.8
                9e-4, // Call, moneyness=0.9
                6e-4, // Call, moneyness=1.0
                5e-4, // Call, moneyness=1.1
                5e-4], // Call, moneyness=1.2
            [6e-4, // Put, moneyness=0.8
                5e-4, // Put, moneyness=0.9
                8e-4, // Put, moneyness=1.0
                0.002, // Put, moneyness=1.1
                0.002], // Put, moneyness=1.2
        ];

        for (int typeIndex = 0; typeIndex < optionTypes.Length; ++typeIndex)
        {
            Real analyticTolerance = 5e-4;

            int timeSteps = 50;
            int numberOfSamples = 4095;
            long mcSeed = 42;

            Real q = 0.04;
            Real r = 0.01;
            Real sigmaBs = 0.245;
            Real s = 100;

            // Test 1: Set up an equivalent flat Heston and compare to analytical BS pricing
            Real v0 = sigmaBs * sigmaBs;
            Real kappa = 1e-8;
            Real theta = sigmaBs * sigmaBs;
            Real sigma = 1e-8;
            Real rho = -0.93;

            DayCounter dc = new Actual360();
            Date today = Date.TodaysDate();
            var context = new EvaluationContext { EvaluationDate = today };

            Date exDate = today + new Period(1, TimeUnit.Years);
            var exercise = new EuropeanExercise(exDate);
            Date reset = today + new Period(6, TimeUnit.Months);
            var payoff = new PlainVanillaPayoff(optionTypes[typeIndex], 0.0);

            YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), q, dc, context);
            YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), r, dc, context);
            GeneralizedBlackScholesProcess bsProcess = Process(s, q, r, sigmaBs, dc, context);

            var hestonProcess = new HestonProcess(rTS, qTS, s, v0, kappa, theta, sigma, rho);

            Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> mcEngine =
                new MakeMCForwardEuropeanHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess, mcSeed)
                    .WithSteps(timeSteps)
                    .WithSamples(numberOfSamples)
                    .ToEngine();

            Real[] moneyness = [0.8, 0.9, 1.0, 1.1, 1.2];

            for (int moneynessIndex = 0; moneynessIndex < moneyness.Length; ++moneynessIndex)
            {
                var option = new ForwardVanillaOption(moneyness[moneynessIndex], reset, payoff, exercise);

                Real analyticPrice = ForwardVanillaEngine.Calculate(
                    option, context, bsProcess, AnalyticEuropeanEngine.Calculate).NPV!.Value;

                Real mcPrice = mcEngine(option, context).NPV!.Value;

                Real mcError = RelativeError(analyticPrice, mcPrice, s);

                if (mcError > mcForwardStartTolerance[typeIndex][moneynessIndex])
                {
                    ReportFailure(
                        "testHestonMCForwardStartPrices", payoff, exercise, s, q, r, today, sigmaBs,
                        moneyness[moneynessIndex], reset, analyticPrice, mcPrice, mcError,
                        mcForwardStartTolerance[typeIndex][moneynessIndex]);
                }
            }

            // Test 2: Using an arbitrary Heston model, check that prices match semi-analytical
            // Heston prices when reset date is t=0
            v0 = sigmaBs * sigmaBs;
            kappa = 1.0;
            theta = 0.08;
            sigma = 0.39;
            rho = -0.93;

            reset = today;

            var hestonProcessSmile = new HestonProcess(rTS, qTS, s, v0, kappa, theta, sigma, rho);

            var hestonModel = new HestonModel(hestonProcessSmile);

            var analyticHestonEngine = new AnalyticHestonEngine(hestonModel, 96);

            Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> mcEngineSmile =
                new MakeMCForwardEuropeanHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcessSmile, mcSeed)
                    .WithSteps(timeSteps)
                    .WithSamples(numberOfSamples)
                    .ToEngine();

            var analyticForwardHestonEngine = new AnalyticHestonForwardEuropeanEngine(hestonProcessSmile);

            for (int moneynessIndex = 0; moneynessIndex < moneyness.Length; ++moneynessIndex)
            {
                Real strike = s * moneyness[moneynessIndex];
                var vanillaPayoff = new PlainVanillaPayoff(optionTypes[typeIndex], strike);

                var vanillaOption = new VanillaOption(vanillaPayoff, exercise);
                var forwardOption = new ForwardVanillaOption(moneyness[moneynessIndex], reset, payoff, exercise);

                Real analyticPrice = analyticHestonEngine.Calculate(vanillaOption, context).NPV!.Value;

                Real mcPrice = mcEngineSmile(forwardOption, context).NPV!.Value;

                Real mcError = RelativeError(analyticPrice, mcPrice, s);
                Real tolerance = tol[typeIndex][moneynessIndex];

                if (mcError > tolerance)
                {
                    ReportFailure(
                        "testHestonMCPrices", vanillaPayoff, exercise, s, q, r, today, sigmaBs, moneyness[moneynessIndex],
                        reset, analyticPrice, mcPrice, mcError, tolerance);
                }

                // T=0, testing the Analytic Pricer's T=0 analytical solution
                Real hestonAnalyticPrice = analyticForwardHestonEngine.Calculate(forwardOption, context).NPV!.Value;

                Real analyticError = RelativeError(analyticPrice, hestonAnalyticPrice, s);
                if (analyticError > analyticTolerance)
                {
                    ReportFailure(
                        "testHestonAnalyticForwardStartPrices", vanillaPayoff, exercise, s, q, r, today, sigmaBs,
                        moneyness[moneynessIndex], reset, analyticPrice, hestonAnalyticPrice, analyticError,
                        analyticTolerance);
                }
            }
        }
    }

    [Fact]
    public void TestHestonAnalyticalVsMCPrices()
    {
        // Testing Heston analytic vs MC prices...
        OptionType[] optionTypes = [OptionType.Call, OptionType.Put];
        Real[][] tol =
        [
            [0.002, // Call, moneyness=0.8, CV:false
                0.002, // Call, moneyness=0.8, CV:true
                0.001, // Call, moneyness=1.0, CV:false
                0.001, // Call, moneyness=1.8, CV:true
                0.001, // Call, moneyness=1.2, CV:false
                0.001], // Call, moneyness=1.2, CV:true
            [0.001, // Put, moneyness=0.8, CV:false
                0.001, // Put, moneyness=0.8, CV:true
                0.003, // Put, moneyness=1.0, CV:false
                0.003, // Put, moneyness=1.0, CV:true
                0.003, // Put, moneyness=1.2, CV:false
                0.003], // Put, moneyness=1.2, CV:true
        ];

        for (int optionTypeIndex = 0; optionTypeIndex < optionTypes.Length; ++optionTypeIndex)
        {
            int timeSteps = 50;
            int numberOfSamples = 5000;
            long mcSeed = 42;

            Real q = 0.03;
            Real r = 0.005;
            Real s = 100;

            Real vol = 0.3;
            Real v0 = vol * vol;
            Real kappa = 11.35;
            Real theta = 0.022;
            Real sigma = 0.618;
            Real rho = -0.5;

            DayCounter dc = new Actual360();
            Date today = Date.TodaysDate();
            var context = new EvaluationContext { EvaluationDate = today };

            Date exDate = today + new Period(1, TimeUnit.Years);
            var exercise = new EuropeanExercise(exDate);
            Date reset = today + new Period(6, TimeUnit.Months);
            var payoff = new PlainVanillaPayoff(optionTypes[optionTypeIndex], 0.0);

            YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), q, dc, context);
            YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), r, dc, context);

            var hestonProcess = new HestonProcess(rTS, qTS, s, v0, kappa, theta, sigma, rho);

            Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> mcEngine =
                new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(hestonProcess, mcSeed)
                    .WithSteps(timeSteps)
                    .WithSamples(numberOfSamples)
                    .ToEngine();

            Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> mcEngineCv =
                new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(hestonProcess, mcSeed)
                    .WithSteps(timeSteps)
                    .WithSamples(numberOfSamples)
                    .WithControlVariate(true)
                    .ToEngine();

            var analyticEngine = new AnalyticHestonForwardEuropeanEngine(hestonProcess);

            Real[] moneyness = [0.8, 1.0, 1.2];

            // QuantLib's loop advances its index twice a pass, so it prices moneyness 0.8 and 1.2 only
            for (int tol2ndIndex = 0; tol2ndIndex < moneyness.Length; ++tol2ndIndex)
            {
                Real m = moneyness[tol2ndIndex];
                var option = new ForwardVanillaOption(m, reset, payoff, exercise);

                Real analyticPrice = analyticEngine.Calculate(option, context).NPV!.Value;

                Real mcPrice = mcEngine(option, context).NPV!.Value;
                Real error = RelativeError(analyticPrice, mcPrice, s);

                Real tolerance = tol[optionTypeIndex][tol2ndIndex];
                if (error > tolerance)
                {
                    ReportFailure(
                        "testHestonMCVsAnalyticPrices", payoff, exercise, s, q, r, today, vol, m, reset, analyticPrice,
                        mcPrice, error, tolerance);
                }

                Real mcPriceCv = mcEngineCv(option, context).NPV!.Value;

                Real errorCv = RelativeError(analyticPrice, mcPriceCv, s);
                tolerance = tol[optionTypeIndex][++tol2ndIndex];
                if (errorCv > tolerance)
                {
                    ReportFailure(
                        "testHestonMCControlVariateVsAnalyticPrices", payoff, exercise, s, q, r, today, vol, m, reset,
                        analyticPrice, mcPrice, errorCv, tolerance);
                }
            }
        }
    }

    // testValues' and testPerformanceValues' body, the engine template's.
    private static void CheckValues(
        ForwardOptionData[] values,
        Func<ForwardVanillaOption, EvaluationContext, GeneralizedBlackScholesProcess,
            Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>,
            OneAssetOptionResults> engine)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (ForwardOptionData value in values)
        {
            var payoff = new PlainVanillaPayoff(value.Type, 0.0);
            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);
            Date reset = today + TimeToDays(value.Start);

            var stochProcess = new BlackScholesMertonProcess(
                value.S,
                new FlatForward(today, value.Q, dc),
                new FlatForward(today, value.R, dc),
                new BlackConstantVol(today, new NullCalendar(), value.V, dc));

            var option = new ForwardVanillaOption(value.Moneyness, reset, payoff, exercise);

            Real calculated = engine(option, context, stochProcess, AnalyticEuropeanEngine.Calculate).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            Real tolerance = 1e-4;
            if (error > tolerance)
            {
                ReportFailure(
                    "value", payoff, exercise, value.S, value.Q, value.R, today, value.V, value.Moneyness, reset,
                    value.Result, calculated, error, tolerance);
            }
        }
    }

    // QuantLib's testForwardGreeks<Engine>, the engine template on AnalyticEuropeanEngine.
    private static void TestForwardGreeks(
        Func<ForwardVanillaOption, EvaluationContext, GeneralizedBlackScholesProcess,
            Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>,
            OneAssetOptionResults> forwardEngine)
    {
        var tolerance = new Dictionary<string, Real>
        {
            ["delta"] = 1.0e-5,
            ["gamma"] = 1.0e-5,
            ["theta"] = 1.0e-5,
            ["rho"] = 1.0e-5,
            ["divRho"] = 1.0e-5,
            ["vega"] = 1.0e-5,
        };

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] moneyness = [0.9, 1.0, 1.1];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.04, 0.05, 0.06];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] lengths = [1, 2];
        Integer[] startMonths = [6, 9];
        Volatility[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        OneAssetOptionResults Engine(ForwardVanillaOption o, EvaluationContext c, GeneralizedBlackScholesProcess p) =>
            forwardEngine(o, c, p, AnalyticEuropeanEngine.Calculate);

        foreach (OptionType type in types)
        {
            foreach (Real moneynes in moneyness)
            {
                foreach (Integer length in lengths)
                {
                    foreach (Integer startMonth in startMonths)
                    {
                        Date exDate = today + new Period(length, TimeUnit.Years);
                        var exercise = new EuropeanExercise(exDate);

                        Date reset = today + new Period(startMonth, TimeUnit.Months);

                        var payoff = new PlainVanillaPayoff(type, 0.0);

                        var option = new ForwardVanillaOption(moneynes, reset, payoff, exercise);

                        foreach (Real u in underlyings)
                        {
                            foreach (Real m in qRates)
                            {
                                foreach (Real n in rRates)
                                {
                                    foreach (Volatility v in vols)
                                    {
                                        Rate q = m, r = n;
                                        Real Npv(Real spot, Rate qr, Rate rr, Volatility vv, EvaluationContext c) =>
                                            Engine(option, c, Process(spot, qr, rr, vv, dc, c)).NPV!.Value;

                                        OneAssetOptionResults results = Engine(option, context, Process(u, q, r, v, dc, context));
                                        Real value = results.NPV!.Value;
                                        var calculated = new Dictionary<string, Real>
                                        {
                                            ["delta"] = results.Delta!.Value,
                                            ["gamma"] = results.Gamma!.Value,
                                            ["theta"] = results.Theta!.Value,
                                            ["rho"] = results.Rho!.Value,
                                            ["divRho"] = results.DividendRho!.Value,
                                            ["vega"] = results.Vega!.Value,
                                        };
                                        var expected = new Dictionary<string, Real>();

                                        if (value > u * 1.0e-5)
                                        {
                                            // perturb spot and get delta and gamma
                                            Real du = u * 1.0e-4;
                                            OneAssetOptionResults up = Engine(option, context, Process(u + du, q, r, v, dc, context));
                                            Real valueP = up.NPV!.Value, deltaP = up.Delta!.Value;
                                            OneAssetOptionResults down = Engine(option, context, Process(u - du, q, r, v, dc, context));
                                            Real valueM = down.NPV!.Value, deltaM = down.Delta!.Value;
                                            expected["delta"] = (valueP - valueM) / (2 * du);
                                            expected["gamma"] = (deltaP - deltaM) / (2 * du);

                                            // perturb rates and get rho and dividend rho
                                            Spread dr = r * 1.0e-4;
                                            valueP = Npv(u, q, r + dr, v, context);
                                            valueM = Npv(u, q, r - dr, v, context);
                                            expected["rho"] = (valueP - valueM) / (2 * dr);

                                            Spread dq = q * 1.0e-4;
                                            valueP = Npv(u, q + dq, r, v, context);
                                            valueM = Npv(u, q - dq, r, v, context);
                                            expected["divRho"] = (valueP - valueM) / (2 * dq);

                                            // perturb volatility and get vega
                                            Volatility dv = v * 1.0e-4;
                                            valueP = Npv(u, q, r, v + dv, context);
                                            valueM = Npv(u, q, r, v - dv, context);
                                            expected["vega"] = (valueP - valueM) / (2 * dv);

                                            // perturb date and get theta
                                            double dT = dc.YearFraction(today - 1, today + 1);
                                            var yesterday = new EvaluationContext { EvaluationDate = today - 1 };
                                            var tomorrow = new EvaluationContext { EvaluationDate = today + 1 };
                                            valueM = Npv(u, q, r, v, yesterday);
                                            valueP = Npv(u, q, r, v, tomorrow);
                                            expected["theta"] = (valueP - valueM) / dT;

                                            // compare
                                            foreach (string greek in calculated.Keys)
                                            {
                                                Real expct = expected[greek],
                                                    calcl = calculated[greek],
                                                    tol = tolerance[greek];
                                                Real error = RelativeError(expct, calcl, u);
                                                if (error > tol)
                                                {
                                                    ReportFailure(
                                                        greek, payoff, exercise, u, q, r, today, v, moneynes, reset, expct, calcl,
                                                        error, tol);
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
    }

    // The test suite's flatRate(rate, dc) and flatVol(vol, dc), from the evaluation date, in a Black-Scholes-Merton
    // process: QuantLib's quotes set to the loop's values.
    private static GeneralizedBlackScholesProcess Process(
        Real spot, Rate q, Rate r, Volatility v, DayCounter dc, EvaluationContext context) =>
        new BlackScholesMertonProcess(
            spot,
            FlatForward.Create(0, new NullCalendar(), q, dc, context),
            FlatForward.Create(0, new NullCalendar(), r, dc, context),
            BlackConstantVol.Create(0, new NullCalendar(), v, dc, context));

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private static void ReportFailure(
        string greekName,
        PlainVanillaPayoff payoff,
        Exercise exercise,
        Real s,
        Rate q,
        Rate r,
        Date today,
        Volatility v,
        Real moneyness,
        Date reset,
        Real expected,
        Real calculated,
        Real error,
        Real tolerance) =>
        Assert.Fail(
            $"Forward {exercise.Type} {payoff.OptionType} option with {payoff.Name} payoff:\n"
            + $"    spot value:        {s}\n    strike:            {payoff.Strike}\n    moneyness:         {moneyness}\n"
            + $"    dividend yield:    {q}\n    risk-free rate:    {r}\n    reference date:    {today}\n"
            + $"    reset date:        {reset}\n    maturity:          {exercise.LastDate()}\n"
            + $"    volatility:        {v}\n\n    expected   {greekName}: {expected}\n"
            + $"    calculated {greekName}: {calculated}\n    error:            {error}\n    tolerance:        {tolerance}");

    private readonly record struct ForwardOptionData(
        OptionType Type, Real Moneyness, Real S, Rate Q, Rate R, double Start, double T, Volatility V, Real Result, Real Tol);
}
