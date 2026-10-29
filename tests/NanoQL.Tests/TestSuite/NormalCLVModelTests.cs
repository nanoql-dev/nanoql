// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/normalclvmodel.cpp @ v1.44
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
public class NormalCLVModelTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // Adapted: the test suite's flatRate and flatVol on a date are a flat curve and a constant volatility from it, and
    // those without one read the evaluation date, which the context carries; the mapping function is G()'s delegate.
    [Fact]
    public void TestBSCumlativeDistributionFunction()
    {
        // Testing Black-Scholes cumulative distribution function with constant volatility...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(22, Month.June, 2016);
        Date maturity = today + new Period(6, TimeUnit.Months);

        const double s0 = 100;
        const double rRate = 0.1;
        const double qRate = 0.05;
        const double vol = 0.25;

        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, volTS);

        var m = new NormalCLVModel(bsProcess, null, [], 5);
        var rndCalculator = new BSMRNDCalculator(bsProcess);

        const double tol = 1e5 * QL_EPSILON;
        double t = dc.YearFraction(today, maturity);
        for (double x = 10; x < 400; x += 10)
        {
            double calculated = m.Cdf(maturity, x);
            double expected = rndCalculator.Cdf(Math.Log(x), t);

            if (Math.Abs(calculated - expected) > tol)
            {
                Assert.Fail(
                    "Failed to reproduce CDF for "
                    + $"\n    strike:     {x}"
                    + $"\n    calculated: {calculated}"
                    + $"\n    expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestHestonCumlativeDistributionFunction()
    {
        // Testing Heston cumulative distribution function...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(22, Month.June, 2016);
        Date maturity = today + new Period(1, TimeUnit.Years);

        const double s0 = 100;
        const double v0 = 0.01;
        const double rRate = 0.1;
        const double qRate = 0.05;
        const double kappa = 2.0;
        const double theta = 0.09;
        const double sigma = 0.4;
        const double rho = -0.75;

        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);

        var process = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        BlackVolTermStructure hestonVolTS = new HestonBlackVolSurface(new HestonModel(process));

        var m = new NormalCLVModel(new GeneralizedBlackScholesProcess(s0, qTS, rTS, hestonVolTS), null, [], 5);

        var rndCalculator = new HestonRNDCalculator(process);

        const double tol = 1e-6;
        double t = dc.YearFraction(today, maturity);
        for (double x = 10; x < 400; x += 25)
        {
            double calculated = m.Cdf(maturity, x);
            double expected = rndCalculator.Cdf(Math.Log(x), t);

            if (Math.Abs(calculated - expected) > tol)
            {
                Assert.Fail(
                    "Failed to reproduce CDF for "
                    + $"\n    strike:     {x}"
                    + $"\n    calculated: {calculated}"
                    + $"\n    expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestIllustrative1DExample()
    {
        // Testing illustrative 1D example of normal CLV model...

        // example taken from:
        // A. Grzelak, 2015, The CLV Framework -
        // A Fresh Look at Efficient Pricing with Smile
        // http://papers.ssrn.com/sol3/papers.cfm?abstract_id=2747541

        DayCounter dc = new Actual360();
        var today = new Date(22, Month.June, 2016);

        // SABR
        const double beta = 0.5;
        const double alpha = 0.2;
        const double rho = -0.9;
        const double gamma = 0.2;

        // Ornstein-Uhlenbeck
        const double speed = 1.3;
        const double level = 0.1;
        const double vol = 0.25;
        const double x0 = 1.0;

        const double s0 = 1.0;
        const double rRate = 0.03;
        const double qRate = 0.0;

        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);

        BlackVolTermStructure sabrVol = new SABRVolTermStructure(alpha, beta, gamma, rho, s0, rRate, today, dc);

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, sabrVol);

        var ouProcess = new OrnsteinUhlenbeckProcess(speed, vol, x0, level);

        Date[] maturityDates =
        [
            today + new Period(18, TimeUnit.Days),
            today + new Period(90, TimeUnit.Days),
            today + new Period(180, TimeUnit.Days),
            today + new Period(360, TimeUnit.Days),
            today + new Period(720, TimeUnit.Days),
        ];

        var m = new NormalCLVModel(bsProcess, ouProcess, maturityDates, 4);
        Func<double, double, double> g = m.G();

        // test collocation points in x_ij
        Date[] maturities = [maturityDates[0], maturityDates[2], maturityDates[4]];

        double[][] x =
        [
            [1.070, 0.984, 0.903, 0.817],
            [0.879, 0.668, 0.472, 0.261],
            [0.528, 0.282, 0.052, -0.194],
        ];

        double[][] s =
        [
            [1.104, 1.035, 0.969, 0.895],
            [1.328, 1.122, 0.911, 0.668],
            [1.657, 1.283, 0.854, 0.339],
        ];

        double[] c = [2.3344, 0.7420, -0.7420, -2.3344];

        const double tol = 0.001;
        for (int i = 0; i < maturities.Length; ++i)
        {
            double t = dc.YearFraction(today, maturities[i]);

            for (int j = 0; j < x[0].Length; ++j)
            {
                double calculatedX = m.CollocationPointsX(maturities[i])[j];
                double expectedX = x[i][j];

                if (Math.Abs(calculatedX - expectedX) > tol)
                {
                    Assert.Fail(
                        "Failed to reproduce collocation x points for "
                        + $"\n    time:       {maturities[i]}"
                        + $"\n    j           {j}"
                        + $"\n    calculated: {calculatedX}"
                        + $"\n    expected:   {expectedX}");
                }

                double calculatedS = m.CollocationPointsY(maturities[i])[j];
                double expectedS = s[i][j];
                if (Math.Abs(calculatedS - expectedS) > tol)
                {
                    Assert.Fail(
                        "Failed to reproduce collocation s points for "
                        + $"\n    time:       {maturities[i]}"
                        + $"\n    j           {j}"
                        + $"\n    calculated: {calculatedS}"
                        + $"\n    expected:   {expectedS}");
                }

                double expectation = ouProcess.Expectation(0.0, ouProcess.X0, t);
                double stdDeviation = ouProcess.StdDeviation(0.0, ouProcess.X0, t);

                double calculatedG = g(t, expectation + (stdDeviation * c[j]));
                if (Math.Abs(calculatedG - expectedS) > tol)
                {
                    Assert.Fail(
                        "Failed to reproduce g values at collocation points for "
                        + $"\n    time:       {maturities[i]}"
                        + $"\n    j           {j}"
                        + $"\n    calculated: {calculatedG}"
                        + $"\n    expected:   {expectedS}");
                }
            }
        }
    }

    [Fact]
    public void TestMonteCarloBSOptionPricing()
    {
        // Testing Monte Carlo BS option pricing...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(22, Month.June, 2016);
        var context = new EvaluationContext { EvaluationDate = today };
        Date maturity = today + new Period(1, TimeUnit.Years);
        double t = dc.YearFraction(today, maturity);

        const double strike = 110;
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, strike);
        Exercise exercise = new EuropeanExercise(maturity);

        // Ornstein-Uhlenbeck
        const double speed = 2.3;
        const double level = 100;
        const double sigma = 0.35;
        const double x0 = 100.0;

        const double s0 = x0;
        const double vol = 0.25;
        const double rRate = 0.10;
        const double qRate = 0.04;

        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);
        BlackVolTermStructure vTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, vTS);

        var ouProcess = new OrnsteinUhlenbeckProcess(speed, sigma, x0, level);

        Date[] maturities = [today + new Period(6, TimeUnit.Months), maturity];

        var m = new NormalCLVModel(bsProcess, ouProcess, maturities, 8);
        Func<double, double, double> g = m.G();

        const int nSims = 32767;
        IRsg ld = LowDiscrepancy.MakeSequenceGenerator(1, 23455);

        var stat = new GeneralStatistics();
        for (int i = 0; i < nSims; ++i)
        {
            double dw = ld.NextSequence().Value.Span[0];

            double o_t = ouProcess.Evolve(0, x0, t, dw);
            double s = g(t, o_t);

            stat.Add(payoff.Evaluate(s));
        }

        double calculated = stat.Mean() * rTS.Discount(maturity);

        var option = new VanillaOption(payoff, exercise);
        double expected = AnalyticEuropeanEngine.Calculate(option, context, bsProcess).NPV!.Value;

        const double tol = 0.01;
        if (Math.Abs(calculated - expected) > tol)
        {
            Assert.Fail(
                "Failed to reproduce Monte-Carlo vanilla option price "
                + $"\n    time:       {maturity}"
                + $"\n    strike:     {strike}"
                + $"\n    calculated: {calculated}"
                + $"\n    expected:   {expected}");
        }

        var fdmOption = new VanillaOption(new CLVModelPayoff(payoff.OptionType, payoff.Strike, x => g(t, x)), exercise);

        calculated = FdOrnsteinUhlenbeckVanillaEngine.Calculate(fdmOption, context, ouProcess, rTS, tGrid: 50, xGrid: 800)
            .NPV!.Value;
        if (Math.Abs(calculated - expected) > tol)
        {
            Assert.Fail(
                "Failed to reproduce FDM vanilla option price "
                + $"\n    time:       {maturity}"
                + $"\n    strike:     {strike}"
                + $"\n    calculated: {calculated}"
                + $"\n    expected:   {expected}");
        }
    }

    [Fact]
    public void TestMoustacheGraph()
    {
        // Testing double no-touch pricing with normal CLV model...

        // The comparison of Black-Scholes and normal CLV prices is derived
        // from figure 8.8 in Iain J. Clark's book,
        // Foreign Exchange Option Pricing: A Practitioner's Guide

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, Month.August, 2016);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
        double maturityTime = dc.YearFraction(todaysDate, maturityDate);

        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const double s0 = 100;
        const double r = 0.02;
        const double q = 0.01;

        // parameter of the "calibrated" Heston model
        const double kappa = 1.0;
        const double theta = 0.06;
        const double rho = -0.8;
        const double sigma = 0.8;
        const double v0 = 0.09;

        YieldTermStructure rTS = new FlatForward(todaysDate, r, dc);
        YieldTermStructure qTS = new FlatForward(todaysDate, q, dc);

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho));

        BlackVolTermStructure vTS = new HestonBlackVolSurface(hestonModel);

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, vTS);

        // Ornstein-Uhlenbeck
        const double speed = -0.80;
        const double level = 100;
        const double sigmaOU = 0.15;
        const double x0 = 100;

        var ouProcess = new OrnsteinUhlenbeckProcess(speed, sigmaOU, x0, level);

        Exercise europeanExercise = new EuropeanExercise(maturityDate);

        var vanillaOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), europeanExercise);

        double atmVol = vanillaOption.ImpliedVolatility(
            AnalyticHestonEngine.Calculate(vanillaOption, context, hestonModel).NPV!.Value,
            new GeneralizedBlackScholesProcess(
                s0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), Math.Sqrt(theta), dc)),
            context);

        var analyticProcess = new GeneralizedBlackScholesProcess(
            s0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), atmVol, dc));

        List<Date> maturities = [todaysDate + new Period(2, TimeUnit.Weeks)];
        while (maturities[^1] < maturityDate)
        {
            maturities.Add(maturities[^1] + new Period(2, TimeUnit.Weeks));
        }

        var m = new NormalCLVModel(bsProcess, ouProcess, maturities, 8);
        Func<double, double, double> g = m.G();

        const int n = 18;
        double[] barrierLo = new double[n], barrierHi = new double[n], bsNPV = new double[n];

        var payoff = new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0);

        for (int i = 0; i < n; ++i)
        {
            double dist = 10.0 + (5.0 * i);

            barrierLo[i] = Math.Max(s0 - dist, 1e-2);
            barrierHi[i] = s0 + dist;
            var doubleBarrier = new DoubleBarrierOption(
                DoubleBarrierType.KnockOut, barrierLo[i], barrierHi[i], 0.0, payoff, europeanExercise);

            bsNPV[i] = AnalyticDoubleBarrierBinaryEngine.Calculate(doubleBarrier, context, analyticProcess).NPV!.Value;
        }

        const int factors = 1;
        const int tSteps = 200;
        var grid = new TimeGrid(maturityTime, tSteps);

        // QuantLib's generator takes its default seed, 0, which a sequence of 200 dimensions doesn't read
        var pathGenerator = new PathGenerator<SobolBrownianBridgeRsg>(
            ouProcess, grid, new SobolBrownianBridgeRsg(factors, tSteps, SobolBrownianGeneratorBaseOrdering.Diagonal, 1), false);

        const int nSims = 100000;
        var stats = new GeneralStatistics[n];
        for (int u = 0; u < n; ++u)
        {
            stats[u] = new GeneralStatistics();
        }

        double df = rTS.Discount(maturityDate);

        for (int i = 0; i < nSims; ++i)
        {
            bool[] touch = new bool[n];

            Sample<MonteCarloPath> path = pathGenerator.Next();

            double s = 0;
            for (int j = 1; j <= tSteps; ++j)
            {
                double t = grid[j];
                s = g(t, path.Value[j]);

                for (int u = 0; u < n; ++u)
                {
                    if (s <= barrierLo[u] || s >= barrierHi[u])
                    {
                        touch[u] = true;
                    }
                }
            }

            for (int u = 0; u < n; ++u)
            {
                if (touch[u])
                {
                    stats[u].Add(0.0);
                }
                else
                {
                    stats[u].Add(df * payoff.Evaluate(s));
                }
            }
        }

        double[] expected =
        [
            0.0093023407, 0.090099067, 0.13903052, 0.11206888, 0.059511722,
            0.016627484, -0.0091168814, -0.020725808, -0.022513829,
            -0.020280851, -0.015967361, -0.011476705, -0.0071604902,
            -0.003219595, -0.00014875974, 0.0049910036, 0.0034770584,
            0.0023821554,
        ];

        const double tol = 1e-5;
        for (int u = 0; u < n; ++u)
        {
            double calculated = stats[u].Mean() - bsNPV[u];

            if (Math.Abs(calculated - expected[u]) > tol)
            {
                Assert.Fail(
                    "Failed to reproduce Double no Touch prices"
                    + $"\n    time:          {maturityDate}"
                    + $"\n    barrier lower: {barrierLo[u]}"
                    + $"\n    barrier high:  {barrierHi[u]}"
                    + $"\n    calculated:    {calculated}"
                    + $"\n    expected:      {expected[u]}");
            }
        }
    }

    // QuantLib's CLVModelPayoff: the plain vanilla payoff on the mapped value.
    private sealed class CLVModelPayoff(OptionType type, double strike, Func<double, double> g)
        : StrikedTypePayoff(type, strike)
    {
        public override string Name => "Vanilla";

        public override double Evaluate(double price) => new PlainVanillaPayoff(OptionType, Strike).Evaluate(g(price));
    }
}
