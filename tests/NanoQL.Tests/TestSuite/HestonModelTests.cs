// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2009, 2010, 2012, 2014, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2022 Ignacio Anguita
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testDAXCalibration, testKahlJaeckelCase, testAnalyticPiecewiseTimeDependent, testDAXCalibrationOfTimeDependentModel, testAlanLewisReferencePrices, testExpansionOnAlanLewisReference, testExpansionOnFordeReference, testCosHestonEngine, testCosHestonEngineTruncation, testCharacteristicFct, testAndersenPiterbargPricing, testPiecewiseTimeDependentChFvsHestonChF, testPiecewiseTimeDependentComparison, testPiecewiseTimeDependentChFAsymtotic, testSmallSigmaExpansion4ExpFitting, testExponentialFitting4StrikesAndMaturities, testAsymptoticControlVariate, testOptimalAlphaKmin and testAnalyticPDFHestonEngine, which wait for B4.5's COS, expansion, exponential-fitting, PDF and time-dependent Heston engines; testAnalyticVsBlack, testFdVanillaVsCached, testFdVanillaWithDividendsVsCached, testFdAmerican and testMultipleStrikesEngine, which wait for B4.3's FdHestonVanillaEngine; testMcVsCached, which waits for B4.4's MCEuropeanHestonEngine; testFdBarrierVsCached, which waits for B4.6's FdHestonBarrierEngine; testAllIntegrationMethods and testCosHestonCumulants, which wait for B6.2's exp-sinh integral and numerical differentiation; testAndersenPiterbargControlVariateIntegrand, which waits for B4.8's MCVarianceSwapEngine; testAndersenPiterbargConvergence, which waits for B4.2's discrete integrals; testLocalVolFromHestonModel and testHestonBlackVolSurfaceAtmLevel, which wait for B4.1's HestonBlackVolSurface

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class HestonModelTests
{
    [Fact]
    public void TestBlackCalibration()
    {
        // Testing Heston model calibration using a flat volatility surface...

        /* calibrate a Heston model to a constant volatility surface without
           smile. expected result is a vanishing volatility of the volatility.
           In addition theta and v0 should be equal to the constant variance */

        var context = new EvaluationContext { EvaluationDate = Date.TodaysDate() };

        DayCounter dayCounter = new Actual360();
        Calendar calendar = new NullCalendar();

        YieldTermStructure riskFreeTS = FlatRate(0.04, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.50, dayCounter, context);

        Period[] optionMaturities =
        [
            new(1, TimeUnit.Months), new(2, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months),
            new(9, TimeUnit.Months), new(1, TimeUnit.Years), new(2, TimeUnit.Years),
        ];

        var options = new List<HestonModelHelper>();
        Real s0 = 1.0;
        Volatility volatility = 0.1;

        foreach (Period optionMaturitie in optionMaturities)
        {
            for (Real moneyness = -1.0; moneyness < 2.0; moneyness += 1.0)
            {
                double tau = dayCounter.YearFraction(
                    riskFreeTS.ReferenceDate, calendar.Advance(riskFreeTS.ReferenceDate, optionMaturitie));
                Real fwdPrice = s0 * dividendTS.Discount(tau) / riskFreeTS.Discount(tau);
                Real strikePrice = fwdPrice * Math.Exp(-moneyness * volatility * Math.Sqrt(tau));

                options.Add(new HestonModelHelper(
                    optionMaturitie, calendar, s0, strikePrice, volatility, riskFreeTS, dividendTS, context));
            }
        }

        for (Real sigma = 0.1; sigma < 0.7; sigma += 0.2)
        {
            Real v0 = 0.01;
            Real kappa = 0.2;
            Real theta = 0.02;
            Real rho = -0.75;

            var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);

            var model = new HestonModel(process);

            List<HestonModelHelper> helpers = options
                .Select(o => o.SetPricingEngine<HestonModel>((option, c, m) => AnalyticHestonEngine.Calculate(option, c, m, 96)))
                .ToList();

            var om = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
            HestonModel calibrated = model.Calibrate(helpers, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), context)
                .Model;

            Real tolerance = 3.0e-3;

            Assert.False(
                calibrated.Sigma > tolerance,
                $"Failed to reproduce expected sigma\n    calculated: {calibrated.Sigma}\n    expected:   {0.0}\n    tolerance:  {tolerance}");

            Assert.False(
                Math.Abs(calibrated.Kappa * (calibrated.Theta - (volatility * volatility))) > tolerance,
                $"Failed to reproduce expected theta\n    calculated: {calibrated.Theta}\n    expected:   {volatility * volatility}");

            Assert.False(
                Math.Abs(calibrated.V0 - (volatility * volatility)) > tolerance,
                $"Failed to reproduce expected v0\n    calculated: {calibrated.V0}\n    expected:   {volatility * volatility}");
        }
    }

    [Fact]
    public void TestAnalyticVsCached()
    {
        // Testing analytic Heston engine against cached values...

        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(28, Month.March, 2005);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 1.05);
        var exercise = new EuropeanExercise(exerciseDate);

        YieldTermStructure riskFreeTS = FlatRate(0.0225, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.02, dayCounter, context);

        Real s0 = 1.0;
        Real v0 = 0.1;
        Real kappa = 3.16;
        Real theta = 0.09;
        Real sigma = 0.4;
        Real rho = -0.2;

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);

        var option = new VanillaOption(payoff, exercise);

        var engine = new AnalyticHestonEngine(new HestonModel(process), 64);

        Real expected1 = 0.0404774515;
        Real calculated1 = engine.Calculate(option, context).NPV!.Value;
        Real tolerance = 1.0e-8;

        Assert.False(
            Math.Abs(calculated1 - expected1) > tolerance,
            $"Failed to reproduce cached analytic price\n    calculated: {calculated1}\n    expected:   {expected1}");

        // reference values from www.wilmott.com, technical forum
        // search for "Heston or VG price check"

        Real[] k = [0.9, 1.0, 1.1];
        Real[] expected2 = [0.1330371, 0.0641016, 0.0270645];
        Real[] calculated2 = new Real[6];

        int i;
        for (i = 0; i < 6; ++i)
        {
            var exerciseDate2 = new Date(8 + (i / 3), Month.September, 2005);

            var payoff2 = new PlainVanillaPayoff(OptionType.Call, k[i % 3]);
            var exercise2 = new EuropeanExercise(exerciseDate2);

            YieldTermStructure riskFreeTS2 = FlatRate(0.05, dayCounter, context);
            YieldTermStructure dividendTS2 = FlatRate(0.02, dayCounter, context);

            Real s = riskFreeTS2.Discount(0.7) / dividendTS2.Discount(0.7);

            var process2 = new HestonProcess(riskFreeTS2, dividendTS2, s, 0.09, 1.2, 0.08, 1.8, -0.45);

            var option2 = new VanillaOption(payoff2, exercise2);

            calculated2[i] = AnalyticHestonEngine.Calculate(option2, context, new HestonModel(process2)).NPV!.Value;
        }

        // we are after the value for T=0.7
        double t1 = dayCounter.YearFraction(settlementDate, new Date(8, Month.September, 2005));
        double t2 = dayCounter.YearFraction(settlementDate, new Date(9, Month.September, 2005));

        for (i = 0; i < 3; ++i)
        {
            Real interpolated = calculated2[i] + ((calculated2[i + 3] - calculated2[i]) / (t2 - t1) * (0.7 - t1));

            Assert.False(
                Math.Abs(interpolated - expected2[i]) > 100 * tolerance,
                $"Failed to reproduce cached analytic prices:\n    calculated: {interpolated}\n    expected:   {expected2[i]}");
        }
    }

    [Fact]
    public void TestDifferentIntegrals()
    {
        // Testing different numerical Heston integration algorithms...

        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);

        YieldTermStructure riskFreeTS = FlatRate(0.05, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.03, dayCounter, context);

        Real[] strikes = [0.5, 0.7, 1.0, 1.25, 1.5, 2.0];
        int[] maturities = [1, 2, 3, 12, 60, 120, 360];
        OptionType[] types = [OptionType.Put, OptionType.Call];

        var equityfx = new HestonParameter(0.07, 2.0, 0.04, 0.55, -0.8);
        var highCorr = new HestonParameter(0.07, 1.0, 0.04, 0.55, 0.995);
        var lowVolOfVol = new HestonParameter(0.07, 1.0, 0.04, 0.025, -0.75);
        var highVolOfVol = new HestonParameter(0.07, 1.0, 0.04, 5.0, -0.75);
        var kappaEqSigRho = new HestonParameter(0.07, 0.4, 0.04, 0.5, 0.8);

        HestonParameter[] @params = [equityfx, highCorr, lowVolOfVol, highVolOfVol, kappaEqSigRho];
        Real[] tol = [1e-3, 1e-3, 0.2, 0.01, 1e-3];

        for (int iter = 0; iter < @params.Length; ++iter)
        {
            Real s0 = 1.0;
            var process = new HestonProcess(
                riskFreeTS,
                dividendTS,
                s0,
                @params[iter].V0,
                @params[iter].Kappa,
                @params[iter].Theta,
                @params[iter].Sigma,
                @params[iter].Rho);

            var model = new HestonModel(process);

            var lobattoEngine = new AnalyticHestonEngine(model, 1e-10, 1000000);
            var laguerreEngine = new AnalyticHestonEngine(model, 128);
            var legendreEngine = new AnalyticHestonEngine(
                model, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussLegendre(512));
            var chebyshevEngine = new AnalyticHestonEngine(
                model, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussChebyshev(512));
            var chebyshev2ndEngine = new AnalyticHestonEngine(
                model, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussChebyshev2nd(512));

            Real maxLegendreDiff = 0.0;
            Real maxChebyshevDiff = 0.0;
            Real maxChebyshev2ndDiff = 0.0;
            Real maxLaguerreDiff = 0.0;

            foreach (int maturitie in maturities)
            {
                var exercise = new EuropeanExercise(settlementDate + new Period(maturitie, TimeUnit.Months));

                foreach (Real strike in strikes)
                {
                    foreach (OptionType type in types)
                    {
                        var payoff = new PlainVanillaPayoff(type, strike);

                        var option = new VanillaOption(payoff, exercise);

                        Real lobattoNPV = lobattoEngine.Calculate(option, context).NPV!.Value;

                        Real laguerre = laguerreEngine.Calculate(option, context).NPV!.Value;

                        Real legendre = legendreEngine.Calculate(option, context).NPV!.Value;

                        Real chebyshev = chebyshevEngine.Calculate(option, context).NPV!.Value;

                        Real chebyshev2nd = chebyshev2ndEngine.Calculate(option, context).NPV!.Value;

                        maxLaguerreDiff = Math.Max(maxLaguerreDiff, Math.Abs(lobattoNPV - laguerre));
                        maxLegendreDiff = Math.Max(maxLegendreDiff, Math.Abs(lobattoNPV - legendre));
                        maxChebyshevDiff = Math.Max(maxChebyshevDiff, Math.Abs(lobattoNPV - chebyshev));
                        maxChebyshev2ndDiff = Math.Max(maxChebyshev2ndDiff, Math.Abs(lobattoNPV - chebyshev2nd));
                    }
                }
            }

            Real maxDiff = new[] { maxLaguerreDiff, maxLegendreDiff, maxChebyshevDiff, maxChebyshev2ndDiff }.Max();

            Real tr = tol[iter];
            Assert.False(
                maxDiff > tr,
                $"Failed to reproduce Heston pricing values within given tolerance\n    maxDifference: {maxDiff}\n    tolerance:     {tr}");
        }
    }

    [Fact]
    public void TestSmallSigmaExpansion()
    {
        // Testing small sigma expansion of the characteristic function...

        var settlementDate = new Date(20, Month.March, 2020);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        Date maturityDate = settlementDate + new Period(2, TimeUnit.Years);

        DayCounter dc = new Actual365Fixed();
        double t = dc.YearFraction(settlementDate, maturityDate);
        YieldTermStructure rTS = FlatRate(0.0, dc, context);

        Real spot = 100;

        Real theta = 0.1 * 0.1;
        Real v0 = theta + 0.02;
        Real kappa = 1.25;
        Real sigma = 1e-9;
        Real rho = -0.9;

        var hestonModel = new HestonModel(new HestonProcess(rTS, rTS, spot, v0, kappa, theta, sigma, rho));

        var engine = new AnalyticHestonEngine(hestonModel);

        var expectedChF = new Complex(0.990463578538352651, 2.60693475987521132e-12);

        Complex calculatedChF = engine.ChF(new Complex(0.55, -0.5), t);

        Real diffChF = Complex.Abs(expectedChF - calculatedChF);
        Real tolChF = 1e-12;
        Assert.False(
            diffChF > tolChF,
            "failed to reproduce normalized characteristic function value for small sigma"
            + $"\n  expeceted : {expectedChF}\n  calclated : {calculatedChF}\n  diff      : {diffChF}\n  tolerance : {tolChF}");

        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, 120.0), new EuropeanExercise(maturityDate));

        Real calculatedNPV = AnalyticHestonEngine.Calculate(
            option,
            context,
            hestonModel,
            AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg,
            FourierIntegration.GaussLaguerre(192)).NPV!.Value;

        Real stdDev = Math.Sqrt((((1 - Math.Exp(-kappa * t)) * (v0 - theta) / (kappa * t)) + theta) * t);

        Real expectedNPV = BlackFormulaFunctions.BlackFormula(OptionType.Call, 120.0, 100.0, stdDev);

        Real diffNPV = Math.Abs(calculatedNPV - expectedNPV);
        Real tolNPV = 50 * sigma;

        Assert.False(
            diffNPV > tolNPV,
            "failed to reproduce Black Scholes prices for Heston model with very small sigma"
            + $"\n  expeceted : {expectedNPV}\n  calclated : {calculatedNPV}\n  diff      : {diffNPV}\n  tolerance : {tolNPV}");
    }

    [Fact]
    public void TestHestonEngineIntegration()
    {
        // Testing Heston engine integration signature...

        static Real Square(Real x) => x * x;

        FourierIntegration integration = FourierIntegration.GaussLobatto(1e-12, 1e-12);

        Real c1 = integration.Calculate(1.0, Square, 1.0);

        // QuantLib's HestonIntegrationMaxBoundTestFct: a bound that counts its calls
        int callCounter = 0;
        double? TestFct()
        {
            ++callCounter;
            return 1.0;
        }

        Real c2 = integration.Calculate(1.0, Square, TestFct);

        Assert.False(
            callCounter == 0 || Math.Abs(c1 - (1 / 3.0)) > 1e-10 || Math.Abs(c2 - (1 / 3.0)) > 1e-10,
            "failed to test Heston engine integration signature");
    }

    [Fact]
    public void TestOptimalControlVariateChoice()
    {
        // Testing optimal control variate choice for the Heston model...

        Real v0 = 0.0225;
        Real rho = 0.5;
        Real sigma = 2.0;
        Real kappa = 0.1;
        Real theta = 0.01;
        double t = 2.0;

        AnalyticHestonEngineComplexLogFormula calculated =
            AnalyticHestonEngine.OptimalControlVariate(t, v0, kappa, theta, sigma, rho);

        Assert.True(
            calculated == AnalyticHestonEngineComplexLogFormula.AsymptoticChF,
            "failed to reproduce optimal control variate choice");

        calculated = AnalyticHestonEngine.OptimalControlVariate(t, v0, kappa, theta, 0.05, rho);
        Assert.True(
            calculated == AnalyticHestonEngineComplexLogFormula.AngledContour,
            "failed to reproduce optimal control variate choice");

        calculated = AnalyticHestonEngine.OptimalControlVariate(t, 0.5, kappa, theta, sigma, rho);
        Assert.True(
            calculated == AnalyticHestonEngineComplexLogFormula.AngledContour,
            "failed to reproduce optimal control variate choice");
    }

    [Fact]
    public void TestOptimalAlphaKmax()
    {
        // Testing optimal Alpha k_min for characteristic function...

        var todaysDate = new Date(1, Month.January, 2022);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        DayCounter dc = new Actual365Fixed();
        YieldTermStructure yTS = FlatRate(0.0, dc, context);
        Real spot = 75;

        double T = 2;
        Real strike = 100;

        // case 1: kappa - sigma*rho > 0
        var model = new HestonModel(new HestonProcess(yTS, yTS, spot, .1, 1.2, 0.2, 0.2, -0.8));
        var engine = new AnalyticHestonEngine(model);
        Real alphaStar = new AnalyticHestonEngineOptimalAlpha(T, engine).AlphaGreaterZero(strike).Alpha;
        CheckSmall(alphaStar - 3.22615, 1e-4);

        // case 2: kappa - sigma*rho < 0, T < t_cut
        model = new HestonModel(new HestonProcess(yTS, yTS, spot, 0.1, 1.2, 0.2, 1.5, 0.9));
        engine = new AnalyticHestonEngine(model);
        alphaStar = new AnalyticHestonEngineOptimalAlpha(T, engine).AlphaGreaterZero(strike).Alpha;
        CheckSmall(alphaStar - 0.31137, 1e-4);

        // case 3: kappa - sigma*rho < 0, T >= t_cut
        model = new HestonModel(new HestonProcess(yTS, yTS, spot, 0.1, 1.2, 0.2, 2.25, 0.9));
        engine = new AnalyticHestonEngine(model);
        alphaStar = new AnalyticHestonEngineOptimalAlpha(T, engine).AlphaGreaterZero(strike).Alpha;
        CheckSmall(alphaStar - 0.11940, 1e-4);

        // case 4: kappa - sigma*rho == 0
        model = new HestonModel(new HestonProcess(yTS, yTS, spot, 0.1, 1.0, 0.2, 2.0, 0.5));
        engine = new AnalyticHestonEngine(model);
        alphaStar = new AnalyticHestonEngineOptimalAlpha(T, engine).AlphaGreaterZero(strike).Alpha;
        CheckSmall(alphaStar - 0.28006, 1e-4);
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // QL_CHECK_SMALL: |x| below the tolerance
    private static void CheckSmall(Real x, Real tolerance) =>
        Assert.True(Math.Abs(x) < tolerance, $"{x} is not small enough (tolerance {tolerance})");

    // The test suite's HestonParameter.
    private readonly record struct HestonParameter(Real V0, Real Kappa, Real Theta, Real Sigma, Real Rho);
}
