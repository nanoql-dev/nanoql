// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2009, 2010, 2012, 2014, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2022 Ignacio Anguita
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testFdBarrierVsCached, which waits for S-bar's FdHestonBarrierEngine; testAllIntegrationMethods and testCosHestonCumulants, which wait for S-core's exp-sinh integral and numerical differentiation

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

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
    public void TestDAXCalibration()
    {
        // Testing Heston model calibration using DAX volatility data...

        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        (Real s0, YieldTermStructure riskFreeTS, YieldTermStructure dividendTS, HestonModelHelper[] options) =
            GetDAXCalibrationMarketData(context);

        const Real v0 = 0.1;
        const Real kappa = 1.0;
        const Real theta = 0.1;
        const Real sigma = 0.5;
        const Real rho = -0.5;

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);

        var model = new HestonModel(process);

        Func<VanillaOption, EvaluationContext, HestonModel, InstrumentResults>[] engines =
        [
            (o, c, m) => AnalyticHestonEngine.Calculate(o, c, m, 64),
            (o, c, m) => COSHestonEngine.Calculate(o, c, m, 12, 75),
            (o, c, m) => ExponentialFittingHestonEngine.Calculate(o, c, m),
        ];

        // QuantLib resets the model's parameters before each engine: each calibration starts from the model above
        foreach (Func<VanillaOption, EvaluationContext, HestonModel, InstrumentResults> engine in engines)
        {
            HestonModelHelper[] helpers = [.. options.Select(o => o.SetPricingEngine(engine))];

            var om = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
            HestonModel calibrated = model.Calibrate(helpers, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), context).Model;

            Real sse = 0;
            for (int i = 0; i < 13 * 8; ++i)
            {
                Real diff = helpers[i].CalibrationError(calibrated, context) * 100.0;
                sse += diff * diff;
            }

            Real expected = 177.2; // see article by A. Sepp.
            Assert.False(
                Math.Abs(sse - expected) > 1.0,
                $"Failed to reproduce calibration error\n    calculated: {sse}\n    expected:   {expected}");
        }
    }

    [Fact]
    public void TestAnalyticVsBlack()
    {
        // Testing analytic Heston engine against Black formula...
        Date settlementDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date exerciseDate = settlementDate + new Period(6, TimeUnit.Months);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 30);
        var exercise = new EuropeanExercise(exerciseDate);

        YieldTermStructure riskFreeTS = FlatRate(0.1, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.04, dayCounter, context);

        Real s0 = 32.0;

        const Real v0 = 0.05;
        const Real kappa = 5.0;
        const Real theta = 0.05;
        const Real sigma = 1.0e-4;
        const Real rho = 0.0;

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);

        var option = new VanillaOption(payoff, exercise);
        Real calculated = AnalyticHestonEngine.Calculate(option, context, new HestonModel(process), 144).NPV!.Value;

        Real yearFraction = dayCounter.YearFraction(settlementDate, exerciseDate);
        Real forwardPrice = 32 * Math.Exp((0.1 - 0.04) * yearFraction);
        Real expected = BlackFormulaFunctions.BlackFormula(
            payoff.OptionType, payoff.Strike, forwardPrice, Math.Sqrt(0.05 * yearFraction)) * Math.Exp(-0.1 * yearFraction);
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 2.0e-7;
        Assert.False(
            error > tolerance,
            $"failed to reproduce Black price with AnalyticHestonEngine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error:E}");

        calculated = FdHestonVanillaEngine.Calculate(option, context, new HestonModel(process), 200, 200, 100).NPV!.Value;
        error = Math.Abs(calculated - expected);
        tolerance = 1.0e-3;
        Assert.False(
            error > tolerance,
            $"failed to reproduce Black price with FdHestonVanillaEngine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error:E}");
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
    public void TestMcVsCached()
    {
        // Testing Monte Carlo Heston engine against cached values...
        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(28, Month.March, 2005);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 1.05);
        var exercise = new EuropeanExercise(exerciseDate);

        YieldTermStructure riskFreeTS = FlatRate(0.7, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.4, dayCounter, context);

        Real s0 = 1.05;

        var process = new HestonProcess(
            riskFreeTS, dividendTS, s0, 0.3, 1.16, 0.2, 0.8, 0.8, HestonProcessDiscretization.QuadraticExponentialMartingale);

        var option = new VanillaOption(payoff, exercise);

        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(process, 1234)
                .WithStepsPerYear(11)
                .WithAntitheticVariate()
                .WithSamples(50000)
                .ToEngine();

        OneAssetOptionResults results = engine(option, context);

        Real expected = 0.0632851308977151;
        Real calculated = results.NPV!.Value;
        Real errorEstimate = results.ErrorEstimate!.Value;
        Real tolerance = 7.5e-4;

        Assert.False(
            Math.Abs(calculated - expected) > 2.34 * errorEstimate,
            $"Failed to reproduce cached price\n    calculated: {calculated}\n    expected:   {expected} +/- {errorEstimate}");

        Assert.False(
            errorEstimate > tolerance,
            $"failed to reproduce error estimate\n    calculated: {errorEstimate}\n    expected:   {tolerance}");
    }

    [Fact]
    public void TestFdVanillaVsCached()
    {
        // Testing FD vanilla Heston engine against cached values...
        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(28, Month.March, 2005);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 1.05);
        var exercise = new EuropeanExercise(exerciseDate);

        YieldTermStructure riskFreeTS = FlatRate(0.7, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.4, dayCounter, context);

        Real s0 = 1.05;

        var option = new VanillaOption(payoff, exercise);

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, 0.3, 1.16, 0.2, 0.8, 0.8);

        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeFdHestonVanillaEngine(new HestonModel(process))
                .WithTGrid(100)
                .WithXGrid(200)
                .WithVGrid(100)
                .ToEngine();

        Real expected = 0.06325;
        Real calculated = engine(option, context).NPV!.Value;
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1.0e-4;

        Assert.False(
            error > tolerance,
            $"failed to reproduce cached price with FD engine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error:E}");
    }

    [Fact]
    public void TestFdVanillaWithDividendsVsCached()
    {
        // Testing FD vanilla Heston engine for discrete dividends...
        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 95.0);

        Real s0 = 100.0;
        YieldTermStructure riskFreeTS = FlatRate(0.05, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.0, dayCounter, context);

        var exerciseDate = new Date(28, Month.March, 2006);
        var exercise = new EuropeanExercise(exerciseDate);

        var dividendDates = new List<Date>();
        var dividends = new List<Real>();
        for (Date d = settlementDate + new Period(3, TimeUnit.Months);
             d < exercise.LastDate();
             d += new Period(6, TimeUnit.Months))
        {
            dividendDates.Add(d);
            dividends.Add(1.0);
        }

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, 0.04, 1.0, 0.04, 0.001, 0.0);

        var option = new VanillaOption(payoff, exercise);
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeFdHestonVanillaEngine(new HestonModel(process))
                .WithCashDividends(dividendDates, dividends)
                .WithTGrid(200)
                .WithXGrid(400)
                .WithVGrid(100)
                .ToEngine();

        Real calculated = engine(option, context).NPV!.Value;

        // Value calculated with an independent FD framework, validated with
        // an independent MC framework
        Real expected = 12.946;
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 5.0e-3;

        Assert.False(
            error > tolerance,
            $"failed to reproduce discrete dividend price with FD engine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error:E}");
    }

    [Fact]
    public void TestFdAmerican()
    {
        // Testing FD vanilla Heston engine for american exercise...
        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);

        Real s0 = 100.0;
        YieldTermStructure riskFreeTS = FlatRate(0.05, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.03, dayCounter, context);
        var process = new HestonProcess(riskFreeTS, dividendTS, s0, 0.04, 1.0, 0.04, 0.001, 0.0);
        var payoff = new PlainVanillaPayoff(OptionType.Put, 95.0);
        var exerciseDate = new Date(28, Month.March, 2006);
        var exercise = new AmericanExercise(settlementDate, exerciseDate);
        var option = new VanillaOption(payoff, exercise);
        Real calculated = new MakeFdHestonVanillaEngine(new HestonModel(process))
            .WithTGrid(200)
            .WithXGrid(400)
            .WithVGrid(100)
            .ToEngine()(option, context).NPV!.Value;

        BlackVolTermStructure volTS = new BlackConstantVol(settlementDate, new NullCalendar(), 0.2, dayCounter);
        var refProcess = new BlackScholesMertonProcess(s0, dividendTS, riskFreeTS, volTS);
        Real expected = FdBlackScholesVanillaEngine.Calculate(option, context, refProcess, [], null, 200, 400).NPV!.Value;

        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1.0e-3;

        Assert.False(
            error > tolerance,
            $"failed to reproduce american option price with FD engine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error:E}");
    }

    [Fact]
    public void TestKahlJaeckelCase()
    {
        // Testing MC and FD Heston engines for the Kahl-Jaeckel example...

        /* Example taken from Wilmott mag (Sept. 2005).
           "Not-so-complex logarithms in the Heston model",
           Example was also discussed within the Wilmott thread
           "QuantLib code is very high quatlity"
        */

        var settlementDate = new Date(30, Month.March, 2007);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(30, Month.March, 2017);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 200);
        var exercise = new EuropeanExercise(exerciseDate);

        var option = new VanillaOption(payoff, exercise);

        YieldTermStructure riskFreeTS = FlatRate(0.0, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.0, dayCounter, context);

        Real s0 = 100;

        const Real v0 = 0.16;
        const Real theta = v0;
        const Real kappa = 1.0;
        const Real sigma = 2.0;
        const Real rho = -0.8;

        (HestonProcessDiscretization Discretization, int NSteps, string Name)[] descriptions =
        [
            (HestonProcessDiscretization.NonCentralChiSquareVariance, 10, "NonCentralChiSquareVariance"),
            (HestonProcessDiscretization.QuadraticExponentialMartingale, 100, "QuadraticExponentialMartingale"),
        ];

        const Real tolerance = 0.2;
        const Real expected = 4.95212;

        foreach ((HestonProcessDiscretization discretization, int nSteps, string name) in descriptions)
        {
            var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho, discretization);

            OneAssetOptionResults results = new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(process, 1234)
                .WithSteps(nSteps)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(tolerance)
                .ToEngine()(option, context);

            Real calculated = results.NPV!.Value;
            Real errorEstimate = results.ErrorEstimate!.Value;

            Assert.False(
                Math.Abs(calculated - expected) > 2.34 * errorEstimate,
                $"Failed to reproduce cached price with MC engine\n    discretization: {name}\n    expected:       {expected}"
                + $"\n    calculated:     {calculated} +/- {errorEstimate}");

            Assert.False(
                errorEstimate > tolerance,
                $"failed to reproduce error estimate with MC engine\n    discretization: {name}\n    calculated    : {errorEstimate}"
                + $"\n    expected      :   {tolerance}");
        }

        (HestonProcessDiscretization Discretization, int NSteps, string Name)[] qmcDescriptions =
        [
            (HestonProcessDiscretization.BroadieKayaExactSchemeLaguerre, 1, "BroadieKayaExactSchemeLaguerre"),
            (HestonProcessDiscretization.BroadieKayaExactSchemeTrapezoidal, 1, "BroadieKayaExactSchemeTrapezoidal"),
            (HestonProcessDiscretization.BroadieKayaExactSchemeLobatto, 1, "BroadieKayaExactSchemeLobatto"),
        ];

        foreach ((HestonProcessDiscretization discretization, int nSteps, string name) in qmcDescriptions)
        {
            // QuantLib's seed 0 isn't an input here (S2R4); a Sobol sequence of so few dimensions reads no seed, its
            // direction integers all tabulated
            Real calculated = new MakeMCEuropeanHestonEngine<LowDiscrepancy, GeneralStatistics>(
                    new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho, discretization), 1)
                .WithSteps(nSteps)
                .WithSamples(1023)
                .ToEngine()(option, context).NPV!.Value;

            Assert.False(
                Math.Abs(calculated - expected) > 0.5 * tolerance,
                $"Failed to reproduce cached price with MC engine\n    discretization: {name}\n    calculated:     {calculated}"
                + $"\n    expected:       {expected}\n    tolerance:      {0.5 * tolerance}");
        }

        var hestonModel = new HestonModel(new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho));

        Real fdCalculated = FdHestonVanillaEngine.Calculate(option, context, hestonModel, 200, 401, 101).NPV!.Value;
        Real error = Math.Abs(fdCalculated - expected);
        Assert.False(
            error > 5.0e-2,
            $"failed to reproduce cached price with FD engine\n    calculated: {fdCalculated}\n    expected:   {expected}\n    error:      {error:E}");

        Real lobattoCalculated = AnalyticHestonEngine.Calculate(option, context, hestonModel, 1e-6, 1000).NPV!.Value;
        error = Math.Abs(lobattoCalculated - expected);
        Assert.False(
            error > 0.00002,
            $"failed to reproduce cached price with GaussLobatto engine\n    calculated: {lobattoCalculated}\n    expected:   {expected}\n    error:      {error:E}");

        Real cosCalculated = COSHestonEngine.Calculate(option, context, hestonModel, 16, 400).NPV!.Value;
        error = Math.Abs(cosCalculated - expected);
        Assert.False(
            error > 0.00002,
            $"failed to reproduce cached price with Cosine engine\n    calculated: {cosCalculated}\n    expected:   {expected}\n    error:      {error:E}");

        Real fittingCalculated = ExponentialFittingHestonEngine.Calculate(option, context, hestonModel).NPV!.Value;
        error = Math.Abs(fittingCalculated - expected);
        Assert.False(
            error > 0.00002,
            "failed to reproduce cached price with exponential fitting Heston engine"
            + $"\n    calculated: {fittingCalculated}\n    expected:   {expected}\n    error:      {error:E}");
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
    public void TestMultipleStrikesEngine()
    {
        // Testing multiple-strikes FD Heston engine...
        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(28, Month.March, 2006);

        var exercise = new EuropeanExercise(exerciseDate);

        YieldTermStructure riskFreeTS = FlatRate(0.06, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.02, dayCounter, context);

        Real s0 = 1.05;

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, 0.16, 2.5, 0.09, 0.8, -0.8);
        var model = new HestonModel(process);

        Real[] strikes = [1.0, 0.5, 0.75, 1.5, 2.0];

        // QuantLib's multiStrikeEngine, its cache filled by the first strike's option: CalculateStrikes on it (HR4 (ii))
        var first = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strikes[0]), exercise);
        System.Collections.Immutable.ImmutableArray<OneAssetOptionResults> multiStrike =
            FdHestonVanillaEngine.CalculateStrikes(first, strikes, context, model, 20, 400, 50);

        Real relTol = 5e-3;
        for (int i = 0; i < strikes.Length; ++i)
        {
            Real strike = strikes[i];
            var payoff = new PlainVanillaPayoff(OptionType.Put, strike);

            var aOption = new VanillaOption(payoff, exercise);

            Real npvCalculated = multiStrike[i].NPV!.Value;
            Real deltaCalculated = multiStrike[i].Delta!.Value;
            Real gammaCalculated = multiStrike[i].Gamma!.Value;
            Real thetaCalculated = multiStrike[i].Theta!.Value;

            OneAssetOptionResults single = FdHestonVanillaEngine.Calculate(aOption, context, model, 20, 400, 50);
            Real npvExpected = single.NPV!.Value;
            Real deltaExpected = single.Delta!.Value;
            Real gammaExpected = single.Gamma!.Value;
            Real thetaExpected = single.Theta!.Value;

            Assert.False(
                Math.Abs(npvCalculated - npvExpected) / npvExpected > relTol,
                $"failed to reproduce price with FD multi strike engine\n    calculated: {npvCalculated}\n    expected:   {npvExpected}\n    error:      {relTol:E}");
            Assert.False(
                Math.Abs(deltaCalculated - deltaExpected) / deltaExpected > relTol,
                $"failed to reproduce delta with FD multi strike engine\n    calculated: {deltaCalculated}\n    expected:   {deltaExpected}\n    error:      {relTol:E}");
            Assert.False(
                Math.Abs(gammaCalculated - gammaExpected) / gammaExpected > relTol,
                $"failed to reproduce gamma with FD multi strike engine\n    calculated: {gammaCalculated}\n    expected:   {gammaExpected}\n    error:      {relTol:E}");
            Assert.False(
                Math.Abs(thetaCalculated - thetaExpected) / thetaExpected > relTol,
                $"failed to reproduce theta with FD multi strike engine\n    calculated: {thetaCalculated}\n    expected:   {thetaExpected}\n    error:      {relTol:E}");
        }
    }

    [Fact]
    public void TestAnalyticPiecewiseTimeDependent()
    {
        // Testing analytic piecewise time dependent Heston prices...

        var settlementDate = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(28, Month.March, 2005);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 1.0);
        var exercise = new EuropeanExercise(exerciseDate);

        Date[] dates = [settlementDate, new Date(1, Month.January, 2007)];
        YieldTermStructure riskFreeTS = new ZeroCurve(dates, [0.0, 0.2], dayCounter);
        YieldTermStructure dividendTS = new ZeroCurve(dates, [0.0, 0.3], dayCounter);

        const Real v0 = 0.1;
        Real s0 = 1.0;

        var theta = new ConstantParameter(0.09, new PositiveConstraint());
        var kappa = new ConstantParameter(3.16, new PositiveConstraint());
        var sigma = new ConstantParameter(4.40, new PositiveConstraint());
        var rho = new ConstantParameter(-0.8, new BoundaryConstraint(-1.0, 1.0));

        var model = new PiecewiseTimeDependentHestonModel(
            riskFreeTS, dividendTS, s0, v0, theta, kappa, sigma, rho, new TimeGrid(20.0, 2));

        var option = new VanillaOption(payoff, exercise);

        var hestonProcess = new HestonProcess(
            riskFreeTS, dividendTS, s0, v0, kappa.Evaluate(0.0), theta.Evaluate(0.0), sigma.Evaluate(0.0), rho.Evaluate(0.0));
        var hestonModel = new HestonModel(hestonProcess);

        Real expected = AnalyticHestonEngine.Calculate(option, context, hestonModel).NPV!.Value;

        Real calculatedGatheral = AnalyticPTDHestonEngine.Calculate(option, context, model, 192).NPV!.Value;
        Assert.False(
            Math.Abs(calculatedGatheral - expected) > 1e-7,
            $"failed to reproduce Heston prices with Gatheral ChF\n    calculated: {calculatedGatheral}\n    expected:   {expected}");

        Real calculatedAndersenPiterbarg = AnalyticPTDHestonEngine.Calculate(
            option,
            context,
            model,
            AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg,
            FourierIntegration.GaussLobatto(1e-12, null, 100000)).NPV!.Value;

        Assert.False(
            Math.Abs(calculatedAndersenPiterbarg - expected) > 1e-9,
            $"failed to reproduce Heston prices Andersen-Piterbarg\n    calculated: {calculatedAndersenPiterbarg}\n    expected:   {expected}");
    }

    [Fact]
    public void TestDAXCalibrationOfTimeDependentModel()
    {
        // Testing time-dependent Heston model calibration...

        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        (Real s0, YieldTermStructure riskFreeTS, YieldTermStructure dividendTS, HestonModelHelper[] options) =
            GetDAXCalibrationMarketData(context);

        var modelGrid = new TimeGrid([0.25, 10.0]);

        const Real v0 = 0.1;
        var sigma = new ConstantParameter(0.5, new PositiveConstraint());
        var theta = new ConstantParameter(0.1, new PositiveConstraint());
        var rho = new ConstantParameter(-0.5, new BoundaryConstraint(-1.0, 1.0));

        double[] pTimes = [0.25];
        Parameter kappa = new PiecewiseConstantParameter(pTimes, new PositiveConstraint());

        for (int i = 0; i < pTimes.Length + 1; ++i)
        {
            kappa = kappa.SetParam(i, 10.0);
        }

        var model = new PiecewiseTimeDependentHestonModel(riskFreeTS, dividendTS, s0, v0, theta, kappa, sigma, rho, modelGrid);

        Func<VanillaOption, EvaluationContext, PiecewiseTimeDependentHestonModel, InstrumentResults>[] engines =
        [
            (o, c, m) => AnalyticPTDHestonEngine.Calculate(o, c, m),
            (o, c, m) => AnalyticPTDHestonEngine.Calculate(
                o, c, m, AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg, FourierIntegration.GaussLaguerre(64)),
            (o, c, m) => AnalyticPTDHestonEngine.Calculate(
                o, c, m, AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg, FourierIntegration.DiscreteTrapezoid(72)),
        ];

        foreach (Func<VanillaOption, EvaluationContext, PiecewiseTimeDependentHestonModel, InstrumentResults> engine in engines)
        {
            HestonModelHelper[] helpers = [.. options.Select(o => o.SetPricingEngine(engine))];

            var om = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
            PiecewiseTimeDependentHestonModel calibrated =
                model.Calibrate(helpers, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), context).Model;

            Real sse = 0;
            for (int i = 0; i < 13 * 8; ++i)
            {
                Real diff = helpers[i].CalibrationError(calibrated, context) * 100.0;
                sse += diff * diff;
            }

            Real expected = 74.4;
            Assert.False(
                Math.Abs(sse - expected) > 1.0,
                $"Failed to reproduce calibration error\n    calculated: {sse}\n    expected:   {expected}");

            // QuantLib calibrates one model through the three engines in turn: each starts where the last one ended
            model = calibrated;
        }
    }

    [Fact]
    public void TestAlanLewisReferencePrices()
    {
        // Testing Alan Lewis reference prices...

        /*
         * testing Alan Lewis reference prices posted in
         * http://wilmott.com/messageview.cfm?catid=34&threadid=90957
         */

        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        var maturityDate = new Date(5, Month.July, 2003);
        var exercise = new EuropeanExercise(maturityDate);

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure riskFreeTS = FlatRate(0.01, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.02, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.04;
        const Real rho = -0.5;
        const Real sigma = 1.0;
        const Real kappa = 4.0;
        const Real theta = 0.25;

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);
        var model = new HestonModel(process);

        Func<VanillaOption, EvaluationContext, OneAssetOptionResults>[] engines =
        [
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, 128),
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, QL_EPSILON, 100000),
            (o, c) => COSHestonEngine.Calculate(o, c, model, 20, 400),
            (o, c) => AnalyticHestonEngine.Calculate(
                o, c, model, AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg,
                FourierIntegration.GaussLobatto(null, 1e-14, 1000000), QL_EPSILON),
            (o, c) => ExponentialFittingHestonEngine.Calculate(o, c, model),
            (o, c) => AnalyticHestonEngine.Calculate(
                o, c, model, AnalyticHestonEngineComplexLogFormula.AngledContour,
                FourierIntegration.GaussLobatto(null, 1e-14, 1000000), QL_EPSILON),
            (o, c) => AnalyticHestonEngine.Calculate(
                o, c, model, AnalyticHestonEngineComplexLogFormula.OptimalCV,
                FourierIntegration.GaussLobatto(null, 1e-14, 1000000), QL_EPSILON),
        ];

        Real[] strikes = [80, 90, 100, 110, 120];
        OptionType[] types = [OptionType.Put, OptionType.Call];

        Real tol = 1e-12; // 3e-15 works on linux/ia32,
                          // but keep some buffer for other platforms

        for (int i = 0; i < strikes.Length; ++i)
        {
            Real strike = strikes[i];

            for (int j = 0; j < types.Length; ++j)
            {
                OptionType type = types[j];

                for (int k = 0; k < engines.Length; ++k)
                {
                    var option = new VanillaOption(new PlainVanillaPayoff(type, strike), exercise);

                    Real expected = AlanLewisReferencePrices[i][j];
                    Real calculated = engines[k](option, context).NPV!.Value;
                    Real relError = Math.Abs(calculated - expected) / expected;

                    Assert.False(
                        relError > tol || double.IsNaN(calculated),
                        "failed to reproduce Alan Lewis Reference prices "
                        + $"\n    strike     : {strike}\n    option type: {type}\n    engine type: {k}\n    rel. error : {relError}");
                }
            }
        }
    }

    [Fact]
    public void TestExpansionOnAlanLewisReference()
    {
        // Testing expansion on Alan Lewis reference prices...

        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        var maturityDate = new Date(5, Month.July, 2003);
        var exercise = new EuropeanExercise(maturityDate);

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure riskFreeTS = FlatRate(0.01, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.02, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.04;
        const Real rho = -0.5;
        const Real sigma = 1.0;
        const Real kappa = 4.0;
        const Real theta = 0.25;

        var process = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);
        var model = new HestonModel(process);

        // don't test Forde as it does not behave well on this example
        HestonExpansionEngineHestonExpansionFormula[] engines =
            [HestonExpansionEngineHestonExpansionFormula.LPP2, HestonExpansionEngineHestonExpansionFormula.LPP3];

        Real[] strikes = [80, 90, 100, 110, 120];
        OptionType[] types = [OptionType.Put, OptionType.Call];

        Real[] tol = [1.003e-2, 3.645e-3];

        for (int i = 0; i < strikes.Length; ++i)
        {
            Real strike = strikes[i];

            for (int j = 0; j < types.Length; ++j)
            {
                OptionType type = types[j];

                for (int k = 0; k < engines.Length; ++k)
                {
                    var option = new VanillaOption(new PlainVanillaPayoff(type, strike), exercise);

                    Real expected = AlanLewisReferencePrices[i][j];
                    Real calculated = HestonExpansionEngine.Calculate(option, context, model, engines[k]).NPV!.Value;
                    Real relError = Math.Abs(calculated - expected) / expected;

                    Assert.False(
                        relError > tol[k],
                        "failed to reproduce Alan Lewis Reference prices "
                        + $"\n    strike     : {strike}\n    option type: {type}\n    engine type: {k}\n    rel. error : {relError}");
                }
            }
        }
    }

    [Fact]
    public void TestExpansionOnFordeReference()
    {
        // Testing expansion on Forde reference prices...

        const Real forward = 100.0;
        const Real v0 = 0.04;
        const Real rho = -0.4;
        const Real sigma = 0.2;
        const Real kappa = 1.15;
        const Real theta = 0.04;

        Real[] terms = [0.1, 1.0, 5.0, 10.0];

        Real[] strikes = [60, 80, 90, 100, 110, 120, 140];

        Real[][] referenceVols =
        [
            [0.27284673574924445, 0.22360758200372477, 0.21023988547031242, 0.1990674789471587, 0.19118230678920461, 0.18721342919371017, 0.1899869903378507],
            [0.25200775151345, 0.2127275920953156, 0.20286528150874591, 0.19479398358151515, 0.18872591728967686, 0.18470857955411824, 0.18204457060905446],
            [0.21637821506229973, 0.20077227130455172, 0.19721753043236154, 0.1942233023784151, 0.191693211401571, 0.18955229722896752, 0.18491727548069495],
            [0.20672925973965342, 0.198583062164427, 0.19668274423922746, 0.1950420231354201, 0.193610364344706, 0.1923502827886502, 0.18934360917857015],
        ];

        Real[][] tol =
        [
            [0.06, 0.03, 0.03, 0.02],
            [0.15, 0.08, 0.04, 0.02],
            [0.06, 0.08, 1.0, 1.0], // forde breaks down for long maturities
        ];
        Real[][] tolAtm =
        [
            [4e-6, 7e-4, 2e-3, 9e-4],
            [7e-6, 4e-4, 9e-4, 4e-4],
            [4e-4, 3e-2, 0.28, 1.0],
        ];

        for (int j = 0; j < terms.Length; ++j)
        {
            Real term = terms[j];
            HestonExpansion[] expansions =
            [
                new LPP2HestonExpansion(kappa, theta, sigma, v0, rho, term),
                new LPP3HestonExpansion(kappa, theta, sigma, v0, rho, term),
                new FordeHestonExpansion(kappa, theta, sigma, v0, rho, term),
            ];

            for (int i = 0; i < strikes.Length; ++i)
            {
                Real strike = strikes[i];
                for (int k = 0; k < expansions.Length; ++k)
                {
                    Real expected = referenceVols[j][i];
                    Real calculated = expansions[k].ImpliedVolatility(strike, forward);
                    Real relError = Math.Abs(calculated - expected) / expected;
                    Real refTol = strike == forward ? tolAtm[k][j] : tol[k][j];

                    Assert.False(
                        relError > refTol,
                        $"failed to reproduce Forde reference vols \n    strike        : {strike}\n    expansion type: {k}\n    rel. error    : {relError}");
                }
            }
        }
    }

    [Fact]
    public void TestCosHestonEngine()
    {
        // Testing Heston pricing via COS method...

        var settlementDate = new Date(7, Month.February, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure riskFreeTS = FlatRate(0.15, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.07, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.1;
        const Real rho = -0.75;
        const Real sigma = 1.8;
        const Real kappa = 4.0;
        const Real theta = 0.22;

        var model = new HestonModel(new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho));

        Date maturityDate = settlementDate + new Period(1, TimeUnit.Years);

        var exercise = new EuropeanExercise(maturityDate);

        PlainVanillaPayoff[] payoffs =
        [
            new(OptionType.Call, s0 + 20),
            new(OptionType.Call, s0 + 150),
            new(OptionType.Put, s0 - 20),
            new(OptionType.Put, s0 - 90),
        ];

        Real[] expected = [9.364410588426075, 0.01036797658132471, 5.319092971836708, 0.01032681906278383];

        const Real tol = 1e-10;

        for (int i = 0; i < payoffs.Length; ++i)
        {
            var option = new VanillaOption(payoffs[i], exercise);

            Real calculated = COSHestonEngine.Calculate(option, context, model, 25, 600).NPV!.Value;

            Real error = Math.Abs(expected[i] - calculated);

            Assert.False(
                error > tol,
                $" failed to reproduce prices with COSHestonEngine\n    expected:   {expected[i]}\n    calculated: {calculated}\n    difference: {error}");
        }
    }

    [Fact]
    public void TestCosHestonEngineTruncation()
    {
        // Testing Heston pricing via COS method outside truncation bounds...

        var todaysDate = new Date(22, Month.August, 2022);
        var maturity = new Date(23, Month.August, 2022);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const OptionType type = OptionType.Call;
        Real underlying = 100;
        Real strike = 200;
        Rate dividendYield = 0;
        Rate riskFreeRate = 0;
        DayCounter dayCounter = new Actual365Fixed();

        var europeanExercise = new EuropeanExercise(maturity);
        YieldTermStructure riskFreeTS = new FlatForward(todaysDate, riskFreeRate, dayCounter);
        YieldTermStructure dividendTS = new FlatForward(todaysDate, dividendYield, dayCounter);

        var payoff = new PlainVanillaPayoff(type, strike);
        var europeanOption = new VanillaOption(payoff, europeanExercise);

        var hestonProcess = new HestonProcess(riskFreeTS, dividendTS, underlying, .007, .8, .007, .1, -.2);
        var hestonModel = new HestonModel(hestonProcess);

        Real npv = COSHestonEngine.Calculate(europeanOption, context, hestonModel).NPV!.Value;

        const Real tol = 1e-7;
        Real error = Math.Abs(npv - 0.0);

        Assert.False(
            error > tol,
            $" failed to reproduce prices with COSHestonEngine\n    expected:   {0.0}\n    calculated: {npv}\n    difference: {error}");
    }

    [Fact]
    public void TestCharacteristicFct()
    {
        // Testing Heston characteristic function...

        var settlementDate = new Date(30, Month.March, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure riskFreeTS = FlatRate(0.35, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.17, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.1;
        const Real rho = -0.85;
        const Real sigma = 0.8;
        const Real kappa = 2.0;
        const Real theta = 0.15;

        var model = new HestonModel(new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho));

        Real[] u = [1.0, 0.45, 3, 4];
        Real[] t = [0.01, 23.2, 3.2];

        var cosEngine = new COSHestonEngine(model);
        var analyticEngine = new AnalyticHestonEngine(model);

        const double tol = 100 * QL_EPSILON;
        foreach (Real i in u)
        {
            foreach (Real j in t)
            {
                Complex c = cosEngine.ChF(i, j);
                Complex a = analyticEngine.ChF(new Complex(i, 0.0), j);

                Real error = Complex.Abs(a - c);
                Assert.False(
                    error > tol,
                    $" failed to reproduce prices with characteristic Fct\n    Cos Engine:      {c}\n    analytic engine: {a}\n    difference:      {error}");
            }
        }
    }

    [Fact]
    public void TestAndersenPiterbargPricing()
    {
        // Testing Andersen-Piterbarg method to price under the Heston model...

        var settlementDate = new Date(30, Month.March, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure riskFreeTS = FlatRate(0.10, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.06, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.1;
        const Real rho = 0.80;
        const Real sigma = 0.75;
        const Real kappa = 1.0;
        const Real theta = 0.1;

        var model = new HestonModel(new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho));

        const AnalyticHestonEngineComplexLogFormula AP = AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg;
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults>[] engines =
        [
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, AP, FourierIntegration.GaussLaguerre()),
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, AP, FourierIntegration.GaussLobatto(null, 1e-9, 10000), 1e-9),
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, AP, FourierIntegration.DiscreteSimpson(256), 1e-8),
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, AP, FourierIntegration.DiscreteTrapezoid(164), 1e-8),
            (o, c) => AnalyticHestonEngine.Calculate(o, c, model, AP, FourierIntegration.Trapezoid(1e-8, 256), 1e-8),
            (o, c) => ExponentialFittingHestonEngine.Calculate(o, c, model),
        ];

        string[] algos =
        [
            "Gauss-Laguerre", "Gauss-Lobatto",
            "Discrete Simpson", "Discrete Trapezoid", "Trapezoid",
            "Exponential Fitting",
        ];

        Date[] maturityDates =
        [
            settlementDate + new Period(1, TimeUnit.Days),
            settlementDate + new Period(1, TimeUnit.Weeks),
            settlementDate + new Period(1, TimeUnit.Years),
            settlementDate + new Period(10, TimeUnit.Years),
        ];

        OptionType[] optionTypes = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50, 75, 90, 100, 110, 130, 150, 200];

        const Real tol = 1e-8;

        foreach (Date maturityDate in maturityDates)
        {
            var exercise = new EuropeanExercise(maturityDate);

            foreach (OptionType optionType in optionTypes)
            {
                foreach (Real strike in strikes)
                {
                    var option = new VanillaOption(new PlainVanillaPayoff(optionType, strike), exercise);

                    Real expected = AnalyticHestonEngine.Calculate(option, context, model, 192).NPV!.Value;

                    for (int k = 0; k < engines.Length; ++k)
                    {
                        Real calculated = engines[k](option, context).NPV!.Value;

                        Real error = Math.Abs(calculated - expected);

                        Assert.False(
                            error > tol,
                            " failed to reproduce prices with Andersen-Piterbarg control variate"
                            + $"\n    algorithm      : {algos[k]}\n    strike         : {strike}\n    control variate: {calculated}"
                            + $"\n    classic engine : {expected}\n    difference:      {error}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestAndersenPiterbargControlVariateIntegrand()
    {
        // Testing Andersen-Piterbarg Integrand with control variate...

        var settlementDate = new Date(17, Month.April, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        Date maturityDate = settlementDate + new Period(2, TimeUnit.Years);

        DayCounter dayCounter = new Actual365Fixed();
        const Rate r = 0.075;
        const Rate q = 0.05;
        YieldTermStructure rTS = FlatRate(r, dayCounter, context);
        YieldTermStructure qTS = FlatRate(q, dayCounter, context);

        double maturity = dayCounter.YearFraction(settlementDate, maturityDate);
        Real df = rTS.Discount(maturity);

        Real s0 = 100.0;
        Real fwd = s0 * qTS.Discount(maturity) / df;

        const Real strike = 150;
        Real sx = Math.Log(strike);
        Real dd = Math.Log(s0 * qTS.Discount(maturity) / df);

        const Real v0 = 0.08;
        const Real rho = -0.8;
        const Real sigma = 0.5;
        const Real kappa = 4.0;
        const Real theta = 0.05;

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho));

        var cosEngine = new COSHestonEngine(hestonModel);

        var engine = new AnalyticHestonEngine(
            hestonModel, AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg, FourierIntegration.GaussLaguerre());

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate));

        Real refNPV = engine.Calculate(option, context).NPV!.Value;

        Real implStdDev = BlackFormulaFunctions.BlackFormulaImpliedStdDev(OptionType.Call, strike, fwd, refNPV, df);

        Real var = cosEngine.Var(maturity);
        Real stdDev = Math.Sqrt(var);

        Real d = (Math.Log(s0 / strike) + ((r - q) * maturity) + (0.5 * var)) / stdDev;

        Real skew = cosEngine.Skew(maturity);
        Real kurt = cosEngine.Kurtosis(maturity);

        var n = new NormalDistribution();

        Real q3 = 1 / 6.0 * s0 * stdDev * ((2 * stdDev) - d) * n.Evaluate(d);
        Real q4 = 1 / 24.0 * s0 * stdDev * ((d * d) - (3 * d * stdDev) - 1) * n.Evaluate(d);
        Real q5 = 1 / 72.0 * s0 * stdDev * ((d * d * d * d) - (5 * d * d * d * stdDev) - (6 * d * d) + (15 * d * stdDev) + 3) * n.Evaluate(d);

        Real bsNPV = BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, fwd, stdDev, df);

        static Real Squared(Real x) => x * x;

        // different variance values for the control variate
        Real[] variances =
        [
            v0 * maturity,
            ((((1 - Math.Exp(-kappa * maturity)) * (v0 - theta)) / (kappa * maturity)) + theta) * maturity,

            // second moment as control variate
            var,

            // third and fourth moment pricing based on
            // Corrado C. and T. Su, (1996-b),
            // “Skewness and Kurtosis in S&P 500 IndexReturns Implied by Option Prices”,
            // Journal of Financial Research 19 (2), 175-192.
            Squared(BlackFormulaFunctions.BlackFormulaImpliedStdDev(OptionType.Call, strike, fwd, bsNPV + (skew * q3), df)),
            Squared(BlackFormulaFunctions.BlackFormulaImpliedStdDev(OptionType.Call, strike, fwd, bsNPV + (skew * q3) + (kurt * q4), df)),

            // Moment matching based on
            // Rubinstein M., (1998), “Edgeworth Binomial Trees”,
            // Journal of Derivatives 5 (3), 20-27.
            Squared(BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                OptionType.Call, strike, fwd, bsNPV + (skew * q3) + (kurt * q4) + (skew * skew * q5), df)),

            // implied vol as control variate
            Squared(implStdDev),

            // remaining function becomes zero for u -> 0
            -8.0 * Math.Log(engine.ChF(new Complex(0, -0.5), maturity).Real),
        ];

        for (int i = 0; i < variances.Length; ++i)
        {
            Real sigmaBS = Math.Sqrt(variances[i] / maturity);

            for (Real u = 0.001; u < 15; u *= 1.05)
            {
                var z = new Complex(u, -0.5);

                Complex phiBS = Complex.Exp(-0.5 * sigmaBS * sigmaBS * maturity * ((z * z) + new Complex(-z.Imaginary, z.Real)));

                Complex ex = Complex.Exp(new Complex(0.0, u * (dd - sx)));

                Complex chf = engine.ChF(z, maturity);

                Real orig = (-ex * chf / ((u * u) + 0.25)).Real;
                Real cv = (ex * (phiBS - chf) / ((u * u) + 0.25)).Real;

                Assert.False(
                    Math.Abs(cv) > 0.03,
                    " Control variate function is greater than original function"
                    + $"\n    control variate method  : {i}\n    z value                 : {u}"
                    + $"\n    control variate function: {cv}\n    original function       : {orig}");
            }
        }
    }

    [Fact]
    public void TestAndersenPiterbargConvergence()
    {
        // Testing Andersen-Piterbarg pricing convergence...

        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        var maturityDate = new Date(5, Month.July, 2003);

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure rTS = FlatRate(0.01, dayCounter, context);
        YieldTermStructure qTS = FlatRate(0.02, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.04;
        const Real rho = -0.5;
        const Real sigma = 1.0;
        const Real kappa = 4.0;
        const Real theta = 0.25;

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho));

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), new EuropeanExercise(maturityDate));

        // Alan Lewis reference prices posted in
        // http://wilmott.com/messageview.cfm?catid=34&threadid=90957
        const Real reference = 16.070154917028834278213466703938231827658768230714;

        Real[] diffs =
        [
            0.0892433814611486298, 0.00013096156482816923,
            1.34107015270501506e-07, 1.22913235145460931e-10,
            1.24344978758017533e-13,
        ];

        for (int n = 10; n <= 50; n += 10)
        {
            Real npv = AnalyticHestonEngine.Calculate(
                option,
                context,
                hestonModel,
                AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg,
                FourierIntegration.DiscreteTrapezoid(n),
                1e-13).NPV!.Value;

            Real calculatedDiff = Math.Abs(npv - reference);
            Assert.False(
                calculatedDiff > 1.25 * diffs[(n / 10) - 1],
                "failed to prove convergence for trapezoid rule "
                + $"\n  calculated difference: {calculatedDiff}"
                + $"\n  expected difference:   {diffs[(n / 10) - 1]}");
        }
    }

    [Fact]
    public void TestPiecewiseTimeDependentChFvsHestonChF()
    {
        // Testing piecewise time dependent ChF vs Heston ChF...

        var settlementDate = new Date(5, Month.July, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        var maturityDate = new Date(5, Month.July, 2018);

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure rTS = FlatRate(0.01, dayCounter, context);
        YieldTermStructure qTS = FlatRate(0.02, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.04;
        const Real rho = -0.5;
        const Real sigma = 1.0;
        const Real kappa = 4.0;
        const Real theta = 0.25;

        var thetaP = new ConstantParameter(theta, new PositiveConstraint());
        var kappaP = new ConstantParameter(kappa, new PositiveConstraint());
        var sigmaP = new ConstantParameter(sigma, new PositiveConstraint());
        var rhoP = new ConstantParameter(rho, new BoundaryConstraint(-1.0, 1.0));

        var analyticEngine = new AnalyticHestonEngine(new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho)));

        var ptdHestonEngine = new AnalyticPTDHestonEngine(
            new PiecewiseTimeDependentHestonModel(
                rTS, qTS, s0, v0, thetaP, kappaP, sigmaP, rhoP,
                new TimeGrid(dayCounter.YearFraction(settlementDate, maturityDate), 10)));

        const double tol = 100 * QL_EPSILON;
        for (Real r = 0.1; r < 4; r += 0.25)
        {
            for (Real phi = 0; phi < 360; phi += 60)
            {
                for (double t = 0.1; t <= 1.0; t += 0.3)
                {
                    Complex z = r * Complex.Exp(new Complex(0, phi));

                    Complex a = analyticEngine.ChF(z, t);
                    Complex b = ptdHestonEngine.ChF(z, t);

                    Assert.False(
                        Complex.Abs(a - b) > tol,
                        "failed to compare characteristic function "
                        + $"\n  time dependent model: {b}\n  Heston model        : {a}\n  Difference          : {Complex.Abs(a - b)}");
                }
            }
        }
    }

    [Fact]
    public void TestPiecewiseTimeDependentComparison()
    {
        // Testing piecewise time dependent ChF vs Heston ChF...

        var settlementDate = new Date(5, Month.July, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dc = new Actual365Fixed();
        var maturityDate = new Date(5, Month.July, 2018);
        double maturity = dc.YearFraction(settlementDate, maturityDate);

        YieldTermStructure rTS = FlatRate(0.05, dc, context);
        YieldTermStructure qTS = FlatRate(0.08, dc, context);

        Real s0 = 100.0;

        var modelGrid = new TimeGrid([0.25, 0.75, 10.0]);

        const Real v0 = 0.1;
        var theta = new ConstantParameter(0.1, new PositiveConstraint());
        var kappa = new ConstantParameter(1.0, new PositiveConstraint());
        var rho = new ConstantParameter(-0.75, new BoundaryConstraint(-1.0, 1.0));

        double[] pTimes = [0.25, 0.75];
        Parameter sigma = new PiecewiseConstantParameter(pTimes, new PositiveConstraint())
            .SetParam(0, 0.30)
            .SetParam(1, 0.15)
            .SetParam(2, 1.25);

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), new EuropeanExercise(maturityDate));

        var ptdModel = new PiecewiseTimeDependentHestonModel(rTS, qTS, s0, v0, theta, kappa, sigma, rho, modelGrid);

        Real calculatedGatheral = AnalyticPTDHestonEngine.Calculate(option, context, ptdModel).NPV!.Value;

        Real calculatedAndersenPiterbarg = AnalyticPTDHestonEngine.Calculate(
            option,
            context,
            ptdModel,
            AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg,
            FourierIntegration.DiscreteTrapezoid(128),
            1e-12).NPV!.Value;

        Assert.False(
            Math.Abs(calculatedGatheral - calculatedAndersenPiterbarg) > 1e-10,
            "failed to reproduce npv for time dependent Heston model "
            + $"\n  Gatheral ChF         : {calculatedGatheral}\n  AndersenPiterbarg ChF: {calculatedAndersenPiterbarg}"
            + $"\n  Difference          : {Math.Abs(calculatedGatheral - calculatedAndersenPiterbarg)}");

        var firstPartProcess = new HestonProcess(
            rTS, qTS, s0, v0, 1.0, 0.1, 0.30, -0.75, HestonProcessDiscretization.QuadraticExponentialMartingale);

        var firstPathGen = new MultiPathGenerator<RsgType>(
            firstPartProcess, new TimeGrid(pTimes[0], 6), PseudoRandom.MakeSequenceGenerator(12, 1234), false);

        var urng = new MersenneTwisterUniformRng(5678);

        var stat = new GeneralStatistics();
        Real df = rTS.Discount(maturityDate);

        const int NSims = 10000;
        for (int i = 0; i < NSims; ++i)
        {
            Real priceS = 0.0;

            for (int j = 0; j < 2; ++j)
            {
                Sample<MultiPath> path1 = (j & 1) != 0 ? firstPathGen.Antithetic() : firstPathGen.Next();
                Real spot1 = path1.Value[0].Back();
                Real v1 = path1.Value[1].Back();

                var secondPathGen = new MultiPathGenerator<RsgType>(
                    new HestonProcess(rTS, qTS, spot1, v1, 1.0, 0.1, 0.15, -0.75, HestonProcessDiscretization.QuadraticExponentialMartingale),
                    new TimeGrid(pTimes[1] - pTimes[0], 12),
                    PseudoRandom.MakeSequenceGenerator(24, urng.NextInt32()),
                    false);

                Sample<MultiPath> path2 = secondPathGen.Next();
                Real spot2 = path2.Value[0].Back();
                Real v2 = path2.Value[1].Back();

                var thirdPathGen = new MultiPathGenerator<RsgType>(
                    new HestonProcess(rTS, qTS, spot2, v2, 1.0, 0.1, 1.25, -0.75, HestonProcessDiscretization.QuadraticExponentialMartingale),
                    new TimeGrid(maturity - pTimes[1], 6),
                    PseudoRandom.MakeSequenceGenerator(12, urng.NextInt32()),
                    false);
                Sample<MultiPath> path3 = thirdPathGen.Next();
                Real spot3 = path3.Value[0].Back();

                priceS += 0.5 * option.Payoff!.Evaluate(spot3);
            }

            stat.Add(priceS * df);
        }

        Real calculatedMC = stat.Mean();
        Real errorEstimate = stat.ErrorEstimate();

        Assert.False(
            Math.Abs(calculatedMC - calculatedGatheral) > 3.0 * errorEstimate,
            "failed to reproduce npv for time dependent Heston model"
            + $"\n  Gatheral ChF     : {calculatedGatheral}\n  Monte-Carlo      : {calculatedMC}"
            + $"\n  Monte-Carlo error: {errorEstimate}\n  Difference       : {Math.Abs(calculatedGatheral - calculatedMC)}");
    }

    [Fact]
    public void TestPiecewiseTimeDependentChFAsymtotic()
    {
        // Testing piecewise time dependent ChF vs Heston ChF...

        var settlementDate = new Date(5, Month.July, 2017);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        Date maturityDate = settlementDate + new Period(13, TimeUnit.Months);

        DayCounter dc = new Actual365Fixed();
        double maturity = dc.YearFraction(settlementDate, maturityDate);
        YieldTermStructure rTS = FlatRate(0.0, dc, context);

        double[] modelTimes = [0.01, 0.5, 2.0];

        var modelGrid = new TimeGrid(modelTimes);

        const Real v0 = 0.1;
        double[] pTimes = modelTimes[..^1];

        Parameter sigma = new PiecewiseConstantParameter(pTimes, new PositiveConstraint());
        Parameter theta = new PiecewiseConstantParameter(pTimes, new PositiveConstraint());
        Parameter kappa = new PiecewiseConstantParameter(pTimes, new PositiveConstraint());
        Parameter rho = new PiecewiseConstantParameter(pTimes, new BoundaryConstraint(-1.0, 1.0));

        Real[] sigmas = [0.01, 0.2, 0.6];
        Real[] thetas = [0.16, 0.06, 0.36];
        Real[] kappas = [1.0, 0.3, 4.0];
        Real[] rhos = [0.5, -0.75, -0.25];

        for (int i = 0; i < 3; ++i)
        {
            sigma = sigma.SetParam(i, sigmas[i]);
            theta = theta.SetParam(i, thetas[i]);
            kappa = kappa.SetParam(i, kappas[i]);
            rho = rho.SetParam(i, rhos[i]);
        }

        Real s0 = 100.0;
        var ptdModel = new PiecewiseTimeDependentHestonModel(rTS, rTS, s0, v0, theta, kappa, sigma, rho, modelGrid);

        const Real eps = 1e-8;

        var ptdHestonEngine = new AnalyticPTDHestonEngine(
            ptdModel, AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg, FourierIntegration.DiscreteTrapezoid(128), eps);

        Complex dUInf = -new Complex(Math.Sqrt(1 - (rhos[0] * rhos[0])), rhos[0]) / sigmas[0];

        Complex dd = new Complex(kappas[0], ((2 * kappas[0] * rhos[0]) - sigmas[0]) / (2 * Math.Sqrt(1 - (rhos[0] * rhos[0]))))
            / (sigmas[0] * sigmas[0]);

        Complex cUInf = new(0.0, 0.0), cc = new(0.0, 0.0), clog = new(0.0, 0.0);

        for (int i = 0; i < 3; ++i)
        {
            Real kappaI = kappas[i];
            Real thetaI = thetas[i];
            Real sigmaI = sigmas[i];
            Real rhoI = rhos[i];
            double tau = Math.Min(maturity, modelGrid[i + 1]) - modelGrid[i];

            cUInf += -kappaI * thetaI * tau / sigmaI * new Complex(Math.Sqrt(1 - (rhoI * rhoI)), rhoI);

            cc += kappaI * new Complex(2 * kappaI, ((2 * kappaI * rhoI) - sigmaI) / Math.Sqrt(1 - (rhoI * rhoI))) * tau * thetaI
                / (2 * sigmaI * sigmaI);

            Complex di = (i < 2)
                ? sigmaI / sigmas[i + 1] * new Complex(Math.Sqrt(1 - (rhos[i + 1] * rhos[i + 1])), rhos[i + 1])
                : new Complex(0.0, 0.0);

            clog += 2 * kappaI * thetaI / (sigmaI * sigmaI) * Complex.Log(
                1.0 - ((di - new Complex(Math.Sqrt(1 - (rhoI * rhoI)), rhoI)) / (di + new Complex(Math.Sqrt(1 - (rhoI * rhoI)), -rhoI))));
        }

        Real epsilon = eps * Math.PI / s0;

        Real uM = FourierIntegration.AndersenPiterbargIntegrationLimit(-(cUInf + (dUInf * v0)).Real, epsilon, v0, maturity);

        const Real ExpectedUM = 18.6918883427;
        Assert.False(
            Math.Abs(uM - ExpectedUM) > 1e-5,
            "failed to reproduce Andersen-Piterbarg Integration bounds for piecewise constant time dependent Heston Model"
            + $"\n  calculated : {uM}\n  expected   : {ExpectedUM}\n  diff       : {Math.Abs(uM - ExpectedUM)}\n  tolerance  : {1e-5}");

        const Real u = 1e8;
        Complex expectedlnChF = ptdHestonEngine.LnChF(new Complex(u, 0.0), maturity);
        Complex calculatedAsympotic = (((dUInf * u) + dd) * v0) + (cUInf * u) + cc + clog;

        Assert.False(
            Complex.Abs(expectedlnChF - calculatedAsympotic) > 0.01,
            "failed to reproduce asymptotic of characteristic function"
            + $"\n  ln(ChF)   : {expectedlnChF}\n  asymptotic: {calculatedAsympotic}"
            + $"\n  diff      : {Complex.Abs(expectedlnChF - calculatedAsympotic)}\n  tolerance : {0.01}");

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), new EuropeanExercise(maturityDate));

        const Real ExpectedNPV = 17.43851162589377;
        Real calculatedNPV = ptdHestonEngine.Calculate(option, context).NPV!.Value;
        Real diffNPV = Math.Abs(ExpectedNPV - calculatedNPV);
        Assert.False(
            diffNPV > 1e-9,
            "failed to reproduce high precision prices for piecewise constant time dependent Heston model"
            + $"\n  expeceted : {ExpectedNPV}\n  calclated : {calculatedNPV}\n  diff      : {diffNPV}\n  tolerance : {1e-9}");
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
    public void TestSmallSigmaExpansion4ExpFitting()
    {
        // Testing small sigma expansion for the exponential fitting Heston engine...

        var todaysDate = new Date(13, Month.March, 2020);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        DayCounter dc = new Actual365Fixed();
        YieldTermStructure rTS = FlatRate(0.05, dc, context);
        YieldTermStructure qTS = FlatRate(0.075, dc, context);

        Real spot = 100.0;

        // special case: reduce sigma
        var maturityDate = new Date(14, Month.March, 2021);
        double maturity = dc.YearFraction(todaysDate, maturityDate);
        Real fwd = spot * qTS.Discount(maturity) / rTS.Discount(maturity);

        const Real v0 = 0.04;
        const Real rho = -0.5;
        const Real kappa = 4.0;
        const Real theta = 0.04;

        const Real moneyness = 0.1;
        Real strike = Math.Exp(-moneyness * Math.Sqrt(theta * maturity)) * fwd;

        Real expected = BlackFormulaFunctions.BlackFormula(
            OptionType.Call, strike, fwd, Math.Sqrt(v0 * maturity), rTS.Discount(maturity));

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate));

        for (Real sigma = 1e-4; sigma > 1e-12; sigma *= 0.1)
        {
            Real calculated = ExponentialFittingHestonEngine.Calculate(
                option, context, new HestonModel(new HestonProcess(rTS, qTS, spot, v0, kappa, theta, sigma, rho))).NPV!.Value;

            Real diff = Math.Abs(expected - calculated);

            Assert.False(
                diff > 0.01 * sigma,
                "failed to reproduce Black Scholes prices for Heston model with very small sigma"
                + $"\n  expeceted : {expected}\n  calclated : {calculated}\n  sigma     : {sigma}\n  diff      : {diff}\n  tolerance : {10 * sigma}");
        }

        // generic cases
        Real[] kappas = [0.5, 1.0, 4.0];
        Real[] thetas = [0.04, 0.09];
        Real[] v0s = [0.025, 0.20];
        int[] maturities = [1, 31, 182, 1850];

        foreach (int maturitie in maturities)
        {
            Date maturityDateI = todaysDate + new Period(maturitie, TimeUnit.Days);
            Real df = rTS.Discount(maturityDateI);
            Real fwdI = spot * qTS.Discount(maturityDateI) / df;

            var exercise = new EuropeanExercise(maturityDateI);

            double t = dc.YearFraction(todaysDate, maturityDateI);

            OptionType optionType = OptionType.Call;

            foreach (Real kappaI in kappas)
            {
                foreach (Real thetaI in thetas)
                {
                    foreach (Real v0I in v0s)
                    {
                        var model = new HestonModel(new HestonProcess(rTS, qTS, spot, v0I, kappaI, thetaI, 1e-13, -0.8));

                        Real stdDev = Math.Sqrt(((((1 - Math.Exp(-kappaI * t)) * (v0I - thetaI)) / (kappaI * t)) + thetaI) * t);

                        for (Real strikeI = spot * Math.Exp(-10 * stdDev); strikeI < spot * Math.Exp(10 * stdDev); strikeI *= 1.2)
                        {
                            var optionI = new VanillaOption(new PlainVanillaPayoff(optionType, strikeI), exercise);

                            Real calculated = ExponentialFittingHestonEngine.Calculate(optionI, context, model).NPV!.Value;

                            Real expectedI = BlackFormulaFunctions.BlackFormula(optionType, strikeI, fwdI, stdDev, df);

                            Real diff = Math.Abs(expectedI - calculated);
                            Assert.False(
                                diff > 1e-10,
                                "failed to reproduce Black Scholes prices for Heston model with very small sigma"
                                + $"\n  expceted  : {expectedI}\n  calculated: {calculated}\n  diff      : {diff}\n  tolerance : {1e-10}");

                            optionType = (optionType == OptionType.Call) ? OptionType.Put : OptionType.Call;
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestExponentialFitting4StrikesAndMaturities()
    {
        // Testing exponential fitting Heston engine with high precision results for large moneyness...

        var todaysDate = new Date(13, Month.May, 2020);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        DayCounter dc = new Actual365Fixed();

        YieldTermStructure rTS = FlatRate(0.0507, dc, context);
        YieldTermStructure qTS = FlatRate(0.0469, dc, context);

        Real s0 = 1.0;

        Real[] moneyness = [-20, -10, -5, 2.5, 1, 0, 1, 2.5, 5, 10, 20];
        Period[] maturities =
        [
            new(1, TimeUnit.Days),
            new(1, TimeUnit.Months),
            new(1, TimeUnit.Years),
            new(10, TimeUnit.Years),
        ];

        const Real v0 = 0.04;
        const Real rho = -0.6;
        const Real sigma = 0.75;
        const Real kappa = 2.5;
        const Real theta = 0.06;

        // Reference prices are calculated using a boost multi-precision
        // implementation of the AnalyticHestonEngine,
        // https://github.com/klausspanderen/HestonExponentialFitting

        Real[] referenceValues =
        [
            1.1631865252540813e-58,
            1.06426822273258466e-49,
            6.92896489110422086e-16,
            8.19515526286263236e-06,
            0.000625608178476390504,
            0.00417261379371945684,
            0.000625608178476390504,
            8.19515526286263236e-06,
            1.92308901296741414e-10,
            1.57327901822368115e-23,
            5.7830515043285098e-58,
            3.56081886910098813e-48,
            2.9489071194212509e-23,
            1.54181757781090727e-11,
            0.000367960011879847279,
            0.00493886106106039818,
            0.0227152343265593776,
            0.00493886106106039818,
            0.000367960011879847279,
            3.06653474407784574e-06,
            8.86665241279348934e-11,
            1.51206812371708868e-20,
            4.18506719865401643e-29,
            2.46637786897559908e-15,
            1.75338784910563671e-08,
            0.00284789176080218294,
            0.0199133097064688458,
            0.0776848755698912041,
            0.0199133097064688458,
            0.00284789176080218294,
            0.00012462190796343504,
            2.59755319566692257e-07,
            1.13853114743124721e-12,
            4.27612073892114211e-39,
            1.08387452075906664e-25,
            4.15179522944463802e-11,
            0.00134157732880653131,
            0.029018582813884912,
            0.176405213088554197,
            0.029018582813884912,
            0.00134157732880653131,
            5.43674074281991917e-06,
            6.51443921040230507e-11,
            9.25756999394709285e-21,
        ];

        var model = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho));

        int idx = 0;
        foreach (Period maturitie in maturities)
        {
            Date maturityDate = todaysDate + maturitie;
            double t = dc.YearFraction(todaysDate, maturityDate);

            var exercise = new EuropeanExercise(maturityDate);

            Real df = rTS.Discount(t);
            Real fwd = s0 * qTS.Discount(t) / df;

            for (int j = 0; j < moneyness.Length; ++j, ++idx)
            {
                Real strike = Math.Exp(-moneyness[j] * Math.Sqrt(theta * t)) * fwd;

                for (int k = 0; k < 2; ++k)
                {
                    var payoff = new PlainVanillaPayoff(k != 0 ? OptionType.Put : OptionType.Call, strike);

                    var option = new VanillaOption(payoff, exercise);

                    Real calculated = ExponentialFittingHestonEngine.Calculate(option, context, model).NPV!.Value;

                    Real expected;
                    if (payoff.OptionType == OptionType.Call)
                    {
                        expected = fwd < strike ? referenceValues[idx] : ((fwd - strike) * df) + referenceValues[idx];
                    }
                    else
                    {
                        expected = fwd > strike ? referenceValues[idx] : referenceValues[idx] - ((fwd - strike) * df);
                    }

                    Real diff = Math.Abs(calculated - expected);
                    const Real tol = 1e-8;
                    Assert.False(
                        diff > tol,
                        "failed to reproduce cached extreme Heston model prices with exponential fitted Gauss-Laguerre quadrature rule"
                        + $"\n  forward   : {fwd}\n  strike    : {strike}\n  expected  : {expected}\n  calculated: {calculated}"
                        + $"\n  diff      : {diff}\n  tolerance : {tol}");
                }
            }
        }
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
    public void TestAsymptoticControlVariate()
    {
        // Testing Heston asymptotic control variate...

        var todaysDate = new Date(4, Month.August, 2020);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        DayCounter dc = new Actual365Fixed();

        YieldTermStructure rTS = FlatRate(0.0, dc, context);

        Real s0 = 1.0;

        const Real v0 = 0.0225;
        const Real rho = 0.5;
        const Real sigma = 2.0;
        const Real kappa = 0.1;
        const Real theta = 0.01;

        var model = new HestonModel(new HestonProcess(rTS, rTS, s0, v0, kappa, theta, sigma, rho));

        Date maturityDate = todaysDate + new Period(2, TimeUnit.Years);
        double t = dc.YearFraction(todaysDate, maturityDate);
        var exercise = new EuropeanExercise(maturityDate);

        Real[] moneynesses = [-15, -10, -5, 0, 5, 10, 15];

        Real[] expected =
        [
            0.0074676425640918,
            0.008680823863233695,
            0.010479611906112223,
            0.023590088942038945,
            0.0019575784806211706,
            0.0005490310253748906,
            0.0001657118753134695,
        ];

        // QuantLib's engines; the analytic ones report their evaluations with each price, QuantLib's
        // numberOfEvaluations()
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults>[] engines =
        [
            new AnalyticHestonEngine(
                model,
                AnalyticHestonEngineComplexLogFormula.OptimalCV,
                FourierIntegration.GaussLobatto(1e-10, 1e-10, 100000)).Calculate,
            new AnalyticHestonEngine(model, AnalyticHestonEngineComplexLogFormula.OptimalCV, FourierIntegration.GaussLaguerre(96)).Calculate,
            new ExponentialFittingHestonEngine(model).Calculate,
        ];

        for (int j = 0; j < engines.Length; ++j)
        {
            for (int i = 0; i < moneynesses.Length; ++i)
            {
                Real moneyness = moneynesses[i];

                Real strike = Math.Exp(-moneyness * Math.Sqrt(theta * t));

                var payoff = new PlainVanillaPayoff(strike > 1.0 ? OptionType.Call : OptionType.Put, strike);

                var option = new VanillaOption(payoff, exercise);

                OneAssetOptionResults results = engines[j](option, context);
                Real calculated = results.NPV!.Value;

                if (results.AdditionalResults is { } additional && additional.TryGetValue("numberOfEvaluations", out object? evaluations))
                {
                    Assert.False(
                        (int)evaluations > 5000,
                        $"too many function valuation needed \n  moneyness      : {moneyness}\n  evaluations    : {evaluations}\n  max evaluations: {5000}");
                }

                Real diff = Math.Abs(calculated - expected[i]);
                Assert.False(
                    diff > 5e-8,
                    "failed to reproduce extreme Heston model values for"
                    + $"\n  moneyness : {moneyness}\n  #engine   : {j}\n  calculated: {calculated}\n  expected  : {expected[i]}"
                    + $"\n  difference: {diff}\n  tolerance : {1e-8}");
            }
        }
    }

    [Fact]
    public void TestLocalVolFromHestonModel()
    {
        // Testing Local Volatility pricing from Heston Model...

        var todaysDate = new Date(28, Month.June, 2021);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        DayCounter dc = new Actual365Fixed();

        YieldTermStructure rTS = new ZeroCurve(
            [
                todaysDate, todaysDate + new Period(90, TimeUnit.Days),
                todaysDate + new Period(180, TimeUnit.Days), todaysDate + new Period(1, TimeUnit.Years),
            ],
            [0.075, 0.05, 0.075, 0.1],
            dc);

        YieldTermStructure qTS = new ZeroCurve(
            [todaysDate, todaysDate + new Period(90, TimeUnit.Days), todaysDate + new Period(1, TimeUnit.Years)],
            [0.06, 0.04, 0.12],
            dc);

        Real s0 = 100.0;

        const Real v0 = 0.1;
        const Real rho = -0.75;
        const Real sigma = 0.8;
        const Real kappa = 1.0;
        const Real theta = 0.16;

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho));

        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, 120.0), new EuropeanExercise(todaysDate + new Period(1, TimeUnit.Years)));

        Real expected = AnalyticHestonEngine.Calculate(
            option, context, hestonModel, AnalyticHestonEngineComplexLogFormula.OptimalCV, FourierIntegration.GaussLaguerre(192)).NPV!.Value;

        var process = new BlackScholesMertonProcess(
            s0,
            qTS,
            rTS,
            new HestonBlackVolSurface(hestonModel, AnalyticHestonEngineComplexLogFormula.OptimalCV, FourierIntegration.GaussLaguerre(24)));

        Real calculated = FdBlackScholesVanillaEngine.Calculate(
            option, context, process, default, null, 25, 125, 1, FdmSchemeDesc.Douglas(), true, 0.4).NPV!.Value;

        const Real tol = 0.002;
        Real diff = Math.Abs(calculated - expected);
        Assert.False(
            diff > tol,
            "failed to reproduce Heston model values with local volatility pricing"
            + $"\n  calculated: {calculated}\n  expected  : {expected}\n  difference: {diff}\n  tolerance : {tol}");
    }

    [Fact]
    public void TestOptimalAlphaKmin()
    {
        // Testing optimal Alpha k_min for characteristic function...

        var todaysDate = new Date(1, Month.January, 2023);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        // Example taken from figure 3 in Andersen and M. Lake, 2018
        // Robust High-Precision Option Pricing by Fourier Transforms:
        DayCounter dc = new Actual365Fixed();
        var model = new HestonModel(
            new HestonProcess(FlatRate(0.0, dc, context), FlatRate(0.0, dc, context), 150, 0.01, 0.1, 0.01, 2.0, 0.8));

        var engine = new AnalyticHestonEngine(
            model, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussLobatto(null, 1e-12, 100000));

        const Real strike = 100;

        Real alphaStar = new AnalyticHestonEngineOptimalAlpha(1.0, engine).AlphaSmallerMinusOne(strike).Alpha;

        CheckSmall(alphaStar + 3.71, 0.0051);

        Date maturityDate = todaysDate + new Period(15, TimeUnit.Months);

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate));
        Real expected = engine.Calculate(option, context).NPV!.Value;

        Real angled = AnalyticHestonEngine.Calculate(
            option,
            context,
            model,
            AnalyticHestonEngineComplexLogFormula.AngledContour,
            FourierIntegration.GaussLobatto(null, 1e-12, 5000)).NPV!.Value;

        Assert.False(Math.Abs(angled - expected) / expected > 1e-10, "failed to reproduce reference values with Angled-Contour");

        Real fitting = ExponentialFittingHestonEngine.Calculate(
            option, context, model, AnalyticHestonEngineComplexLogFormula.OptimalCV).NPV!.Value;

        Assert.False(
            Math.Abs(fitting - expected) / expected > 1e-8,
            "failed to reproduce reference values with Angled-Contourand exponential fitting");
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

    [Fact]
    public void TestHestonBlackVolSurfaceAtmLevel()
    {
        // Testing atmLevel on HestonBlackVolSurface...

        var today = new Date(4, Month.March, 2026);
        DayCounter dc = new Actual365Fixed();

        const Real s0 = 100.0;
        const Rate r = 0.05, q = 0.02;

        YieldTermStructure rTS = new FlatForward(today, r, dc);
        YieldTermStructure qTS = new FlatForward(today, q, dc);

        var process = new HestonProcess(rTS, qTS, s0, 0.04, 1.0, 0.04, 0.3, -0.5);
        var model = new HestonModel(process);
        var surface = new HestonBlackVolSurface(model);

        const Real tol = 1e-12;
        foreach (double t in new[] { 0.25, 1.0, 5.0 })
        {
            Real expected = s0 * qTS.Discount(t) / rTS.Discount(t);
            Real calculated = surface.AtmLevel(t)!.Value;
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                "HestonBlackVolSurface::atmLevel mismatch"
                + $"\n   t:          {t}\n   calculated: {calculated}\n   expected:   {expected}"
                + $"\n   diff:       {calculated - expected}\n   tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestAnalyticPDFHestonEngine()
    {
        // Testing analytic PDF Heston engine... (QuantLib's HestonModelExperimentalTest suite)

        var settlementDate = new Date(5, Month.January, 2014);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure riskFreeTS = FlatRate(0.07, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.185, dayCounter, context);

        Real s0 = 100.0;

        const Real v0 = 0.1;
        const Real rho = -0.5;
        const Real sigma = 1.0;
        const Real kappa = 4.0;
        const Real theta = 0.05;

        var model = new HestonModel(new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho));

        const Real tol = 1e-6;
        var pdfEngine = new AnalyticPDFHestonEngine(model, tol);

        var analyticEngine = new AnalyticHestonEngine(model, 178);

        var maturityDate = new Date(5, Month.July, 2014);
        double maturity = dayCounter.YearFraction(settlementDate, maturityDate);
        var exercise = new EuropeanExercise(maturityDate);

        // 1. check a plain vanilla call option
        for (Real strike = 40; strike < 190; strike += 20)
        {
            var planVanillaOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), exercise);

            Real calculated = pdfEngine.Calculate(planVanillaOption, context).NPV!.Value;

            Real expected = analyticEngine.Calculate(planVanillaOption, context).NPV!.Value;

            Assert.False(
                Math.Abs(calculated - expected) > 3 * tol,
                "failed to reproduce plain vanilla european prices with the analytic probability density engine"
                + $"\n    strike     : {strike}\n    expected   : {expected}\n    calculated : {calculated}"
                + $"\n    diff       : {Math.Abs(calculated - expected)}\n    tol        ; {tol}");
        }

        // 2. digital call option (approx. with a call spread)
        for (Real strike = 40; strike < 190; strike += 10)
        {
            var digitalOption = new VanillaOption(new CashOrNothingPayoff(OptionType.Call, strike, 1.0), exercise);
            Real calculated = pdfEngine.Calculate(digitalOption, context).NPV!.Value;

            const Real eps = 0.01;
            var longCall = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike - eps), exercise);

            var shortCall = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike + eps), exercise);

            Real expected = (analyticEngine.Calculate(longCall, context).NPV!.Value
                - analyticEngine.Calculate(shortCall, context).NPV!.Value) / (2 * eps);
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                "failed to reproduce european digital prices with the analytic probability density engine"
                + $"\n    strike     : {strike}\n    expected   : {expected}\n    calculated : {calculated}"
                + $"\n    diff       : {Math.Abs(calculated - expected)}\n    tol        : {tol}");

            Real d = riskFreeTS.Discount(maturityDate);
            Real expectedCDF = 1.0 - (expected / d);
            Real calculatedCDF = pdfEngine.Cdf(strike, maturity);

            Assert.False(
                Math.Abs(expectedCDF - calculatedCDF) > tol,
                "failed to reproduce cumulative distribution function"
                + $"\n    strike        : {strike}\n    expected CDF  : {expectedCDF}\n    calculated CDF: {calculatedCDF}"
                + $"\n    diff          : {Math.Abs(calculatedCDF - expectedCDF)}\n    tol           : {tol}");
        }
    }

    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // Alan Lewis reference prices posted in http://wilmott.com/messageview.cfm?catid=34&threadid=90957: puts and
    // calls at 80, 90, 100, 110 and 120
    private static readonly Real[][] AlanLewisReferencePrices =
    [
        [7.958878113256768285213263077598987193482161301733, 26.774758743998854221382195325726949201687074848341],
        [12.017966707346304987709573290236471654992071308187, 20.933349000596710388139445766564068085476194042256],
        [17.055270961270109413522653999411000974895436309183, 16.070154917028834278213466703938231827658768230714],
        [23.017825898442800538908781834822560777763225722188, 12.132211516709844867860534767549426052805766831181],
        [29.811026202682471843340682293165857439167301370697, 9.024913483457835636553375454092357136489051667150],
    ];

    // The test suite's getDAXCalibrationMarketData, on the evaluation date.
    private static (Real S0, YieldTermStructure RiskFreeTS, YieldTermStructure DividendYield, HestonModelHelper[] Options)
        GetDAXCalibrationMarketData(EvaluationContext context)
    {
        /* this example is taken from A. Sepp
           Pricing European-Style Options under Jump Diffusion Processes
           with Stochstic Volatility: Applications of Fourier Transform
           http://math.ut.ee/~spartak/papers/stochjumpvols.pdf
        */

        Date settlementDate = context.EvaluationDate;

        DayCounter dayCounter = new Actual365Fixed();
        Calendar calendar = new TARGET();

        int[] t = [13, 41, 75, 165, 256, 345, 524, 703];
        Rate[] r = [0.0357, 0.0349, 0.0341, 0.0355, 0.0359, 0.0368, 0.0386, 0.0401];

        var dates = new List<Date> { settlementDate };
        var rates = new List<Rate> { 0.0357 };
        for (int i = 0; i < 8; ++i)
        {
            dates.Add(settlementDate + t[i]);
            rates.Add(r[i]);
        }

        YieldTermStructure riskFreeTS = new ZeroCurve(dates, rates, dayCounter);

        YieldTermStructure dividendYield = new FlatForward(settlementDate, 0.0, dayCounter);

        Volatility[] v =
        [
            0.6625, 0.4875, 0.4204, 0.3667, 0.3431, 0.3267, 0.3121, 0.3121,
            0.6007, 0.4543, 0.3967, 0.3511, 0.3279, 0.3154, 0.2984, 0.2921,
            0.5084, 0.4221, 0.3718, 0.3327, 0.3155, 0.3027, 0.2919, 0.2889,
            0.4541, 0.3869, 0.3492, 0.3149, 0.2963, 0.2926, 0.2819, 0.2800,
            0.4060, 0.3607, 0.3330, 0.2999, 0.2887, 0.2811, 0.2751, 0.2775,
            0.3726, 0.3396, 0.3108, 0.2781, 0.2788, 0.2722, 0.2661, 0.2686,
            0.3550, 0.3277, 0.3012, 0.2781, 0.2781, 0.2661, 0.2661, 0.2681,
            0.3428, 0.3209, 0.2958, 0.2740, 0.2688, 0.2627, 0.2580, 0.2620,
            0.3302, 0.3062, 0.2799, 0.2631, 0.2573, 0.2533, 0.2504, 0.2544,
            0.3343, 0.2959, 0.2705, 0.2540, 0.2504, 0.2464, 0.2448, 0.2462,
            0.3460, 0.2845, 0.2624, 0.2463, 0.2425, 0.2385, 0.2373, 0.2422,
            0.3857, 0.2860, 0.2578, 0.2399, 0.2357, 0.2327, 0.2312, 0.2351,
            0.3976, 0.2860, 0.2607, 0.2356, 0.2297, 0.2268, 0.2241, 0.2320,
        ];

        Real s0 = 4468.17;
        Real[] strike = [3400, 3600, 3800, 4000, 4200, 4400, 4500, 4600, 4800, 5000, 5200, 5400, 5600];

        var options = new List<HestonModelHelper>();

        for (int s = 0; s < 13; ++s)
        {
            for (int m = 0; m < 8; ++m)
            {
                var maturity = new Period((int)((t[m] + 3) / 7.0), TimeUnit.Weeks); // round to weeks
                options.Add(new HestonModelHelper(
                    maturity,
                    calendar,
                    s0,
                    strike[s],
                    v[(s * 8) + m],
                    riskFreeTS,
                    dividendYield,
                    context,
                    BlackCalibrationHelperCalibrationErrorType.ImpliedVolError));
            }
        }

        return (s0, riskFreeTS, dividendYield, [.. options]);
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
