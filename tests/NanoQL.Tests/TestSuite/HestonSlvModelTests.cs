// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hestonslvmodel.cpp @ v1.44
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
public class HestonSlvModelTests
{
    private const double QL_EPSILON = 2.220446049250313E-16;
    private const double QL_MAX_REAL = double.MaxValue;
    private const double NullReal = 3.4028234663852886e38;
    private const double M_TWOPI = 2.0 * Math.PI;

    [Fact]
    public void TestBlackScholesFokkerPlanckFwdEquation()
    {
        // Testing Fokker-Planck forward equation for BS process...

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(28, Month.December, 2012);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        Date maturityDate = todaysDate + new Period(2, TimeUnit.Years);
        double maturity = dc.YearFraction(todaysDate, maturityDate);

        const double s0 = 100;
        double x0 = Math.Log(s0);
        const double r = 0.035;
        const double q = 0.01;
        const double v = 0.35;

        const int xGrid = (2 * 100) + 1;
        const int tGrid = 400;

        var qTS = new FlatForward(todaysDate, q, dc);
        var rTS = new FlatForward(todaysDate, r, dc);
        var vTS = new BlackConstantVol(todaysDate, new NullCalendar(), v, dc);

        var process = new GeneralizedBlackScholesProcess(s0, qTS, rTS, vTS);

        var uniformMesher = new FdmMesherComposite(new FdmBlackScholesMesher(xGrid, process, maturity, s0));

        var uniformBSFwdOp = new FdmBlackScholesFwdOp(uniformMesher, process, s0, false);

        var concentratedMesher = new FdmMesherComposite(
            new FdmBlackScholesMesher(xGrid, process, maturity, s0, null, null, 0.0001, 1.5, (s0, 0.1)));

        var concentratedBSFwdOp = new FdmBlackScholesFwdOp(concentratedMesher, process, s0, false);

        var shiftedMesher = new FdmMesherComposite(
            new FdmBlackScholesMesher(xGrid, process, maturity, s0, null, null, 0.0001, 1.5, (s0 * 1.1, 0.2)));

        var shiftedBSFwdOp = new FdmBlackScholesFwdOp(shiftedMesher, process, s0, false);

        var exercise = new EuropeanExercise(maturityDate);
        double[] strikes = [50, 80, 100, 130, 150];

        foreach (double strike in strikes)
        {
            var payoff = new PlainVanillaPayoff(OptionType.Call, strike);

            var option = new VanillaOption(payoff, exercise);

            double expected = AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value / rTS.Discount(maturityDate);
            double calcUniform = FokkerPlanckPrice1D(uniformMesher, uniformBSFwdOp, payoff, x0, maturity, tGrid);
            double calcConcentrated = FokkerPlanckPrice1D(concentratedMesher, concentratedBSFwdOp, payoff, x0, maturity, tGrid);
            double calcShifted = FokkerPlanckPrice1D(shiftedMesher, shiftedBSFwdOp, payoff, x0, maturity, tGrid);
            const double tol = 0.02;

            Assert.False(
                Math.Abs(expected - calcUniform) > tol,
                "failed to reproduce european option price with an uniform mesher"
                + $"\n   strike:     {strike}\n   calculated: {calcUniform:F8}\n   expected:   {expected:F8}\n   tolerance:  {tol:F8}");
            Assert.False(
                Math.Abs(expected - calcConcentrated) > tol,
                "failed to reproduce european option price with a concentrated mesher"
                + $"\n   strike:     {strike}\n   calculated: {calcConcentrated:F8}\n   expected:   {expected:F8}\n   tolerance:  {tol:F8}");
            Assert.False(
                Math.Abs(expected - calcShifted) > tol,
                "failed to reproduce european option price with a shifted mesher"
                + $"\n   strike:     {strike}\n   calculated: {calcShifted:F8}\n   expected:   {expected:F8}\n   tolerance:  {tol:F8}");
        }
    }

    [Fact]
    public void TestSquareRootZeroFlowBC()
    {
        // Testing zero-flow BC for the square root process...

        const double kappa = 1.0;
        const double theta = 0.4;
        const double sigma = 0.8;
        const double v_0 = 0.1;
        const double t = 1.0;

        const double vmin = 0.0005;
        const double h = 0.0001;

        double[,] expected =
        {
            { 0.000548, -0.000245, -0.005657, -0.001167, -0.000024 },
            { -0.000595, -0.000701, -0.003296, -0.000883, -0.000691 },
            { -0.001277, -0.001320, -0.003128, -0.001399, -0.001318 },
            { -0.001979, -0.002002, -0.003425, -0.002047, -0.002001 },
            { -0.002715, -0.002730, -0.003920, -0.002760, -0.002730 },
        };

        for (int i = 0; i < 5; ++i)
        {
            double v = vmin + (i * 0.001);
            double vm2 = v - (2 * h);
            double vm1 = v - h;
            double v0 = v;
            double v1 = v + h;
            double v2 = v + (2 * h);

            var rndCalculator = new SquareRootProcessRNDCalculator(v_0, kappa, theta, sigma);

            double pm2 = rndCalculator.Pdf(vm2, t);
            double pm1 = rndCalculator.Pdf(vm1, t);
            double p0 = rndCalculator.Pdf(v0, t);
            double p1 = rndCalculator.Pdf(v1, t);
            double p2 = rndCalculator.Pdf(v2, t);

            // test derivatives
            double flowSym2Order = (sigma * sigma * v0 / (4 * h) * (p1 - pm1))
                + (((kappa * (v0 - theta)) + (sigma * sigma / 2)) * p0);

            double flowSym4Order = (sigma * sigma * v0 / (24 * h) * (-p2 + (8 * p1) - (8 * pm1) + pm2))
                + (((kappa * (v0 - theta)) + (sigma * sigma / 2)) * p0);

            double fwd1Order = (sigma * sigma * v0 / (2 * h) * (p1 - p0))
                + (((kappa * (v0 - theta)) + (sigma * sigma / 2)) * p0);

            double fwd2Order = (sigma * sigma * v0 / (4 * h) * ((4 * p1) - (3 * p0) - p2))
                + (((kappa * (v0 - theta)) + (sigma * sigma / 2)) * p0);

            double fwd3Order = (sigma * sigma * v0 / (12 * h) * (-p2 + (6 * p1) - (3 * p0) - (2 * pm1)))
                + (((kappa * (v0 - theta)) + (sigma * sigma / 2)) * p0);

            const double tol = 0.000002;
            Assert.False(
                Math.Abs(expected[i, 0] - flowSym2Order) > tol
                || Math.Abs(expected[i, 1] - flowSym4Order) > tol
                || Math.Abs(expected[i, 2] - fwd1Order) > tol
                || Math.Abs(expected[i, 3] - fwd2Order) > tol
                || Math.Abs(expected[i, 4] - fwd3Order) > tol,
                $"failed to reproduce Zero Flow BC at\n   v:          {v}\n   tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestTransformedZeroFlowBC()
    {
        // Testing zero-flow BC for transformed Fokker-Planck forward equation...

        const double kappa = 1.0;
        const double theta = 0.4;
        const double sigma = 2.0;
        const int vGrid = 100;

        FdmMesher mesher = CreateStationaryDistributionMesher(kappa, theta, sigma, vGrid);
        Vector v = mesher.Locations(0);

        var p = new double[vGrid];
        var rnd = new SquareRootProcessRNDCalculator(theta, kappa, theta, sigma);
        for (int i = 0; i < v.Size; ++i)
        {
            p[i] = rnd.StationaryPdf(v[i]);
        }

        double alpha = 1.0 - (2 * kappa * theta / (sigma * sigma));
        var q = new double[vGrid];
        for (int i = 0; i < vGrid; ++i)
        {
            q[i] = Math.Pow(v[i], alpha) * p[i];
        }

        for (int i = 0; i < vGrid / 2; ++i)
        {
            double hm = v[i + 1] - v[i];
            double hp = v[i + 2] - v[i + 1];

            double eta = 1.0 / (hm * (hm + hp) * hp);
            double a = -eta * (Squared(hm + hp) - (hm * hm));
            double b = eta * Squared(hm + hp);
            double c = -eta * hm * hm;

            double df = (a * q[i]) + (b * q[i + 1]) + (c * q[i + 2]);
            double flow = (0.5 * sigma * sigma * v[i] * df) + (kappa * v[i] * q[i]);

            const double tol = 1e-6;
            Assert.False(
                Math.Abs(flow) > tol,
                $"failed to reproduce Zero Flow BC at\n v:          {v[i]}\n flow:       {flow}\n tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestSquareRootEvolveWithStationaryDensity()
    {
        // Testing Fokker-Planck forward equation for the square root process with stationary density...

        // Documentation for this test case:
        // http://www.spanderen.de/2013/05/04/fokker-planck-equation-feller-constraint-and-boundary-conditions/
        const double kappa = 2.5;
        const double theta = 0.2;
        const int vGrid = 100;
        const double eps = 1e-2;

        for (double sigma = 0.2; sigma < 2.01; sigma += 0.1)
        {
            double alpha = 1.0 - (2 * kappa * theta / (sigma * sigma));
            var rnd = new SquareRootProcessRNDCalculator(theta, kappa, theta, sigma);
            double vMin = rnd.StationaryInvCdf(eps);
            double vMax = rnd.StationaryInvCdf(1 - eps);

            var mesher = new FdmMesherComposite(new Uniform1dMesher(vMin, vMax, vGrid));

            Vector v = mesher.Locations(0);

            FdmSquareRootFwdOpTransformationType transform = (sigma < 0.75)
                ? FdmSquareRootFwdOpTransformationType.Plain
                : FdmSquareRootFwdOpTransformationType.Power;

            var vq = new double[v.Size];
            var vmq = new double[v.Size];
            for (int i = 0; i < v.Size; ++i)
            {
                vq[i] = Math.Pow(v[i], alpha);
                vmq[i] = 1.0 / vq[i];
            }

            var p = new double[vGrid];
            for (int i = 0; i < v.Size; ++i)
            {
                p[i] = rnd.StationaryPdf(v[i]);
                if (transform == FdmSquareRootFwdOpTransformationType.Power)
                {
                    p[i] *= vq[i];
                }
            }

            var op = new FdmSquareRootFwdOp(mesher, kappa, theta, sigma, 0, transform);

            const int n = 100;
            const double dt = 0.01;
            var evolver = new DouglasScheme(0.5, op);
            evolver.SetStep(dt);

            for (int i = 1; i <= n; ++i)
            {
                evolver.Step(p, i * dt);
            }

            const double expected = 1 - (2 * eps);

            if (transform == FdmSquareRootFwdOpTransformationType.Power)
            {
                for (int i = 0; i < v.Size; ++i)
                {
                    p[i] *= vmq[i];
                }
            }

            var f = new QFct(v, p, alpha);

            double calculated = new GaussLobattoIntegral(1000000, 1e-6).Integrate(f.Evaluate, v[0], v[v.Size - 1]);

            const double tol = 0.005;
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                "failed to reproduce stationary probability function"
                + $"\n    calculated: {calculated}\n    expected:   {expected}\n    tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestSquareRootLogEvolveWithStationaryDensity()
    {
        // Testing Fokker-Planck forward equation for the square root log process with stationary density...

        // Documentation for this test case:
        // nowhere yet :)
        const double kappa = 2.5;
        const double theta = 0.2;
        const int vGrid = 1000;
        const double eps = 1e-2;

        for (double sigma = 0.2; sigma < 2.01; sigma += 0.1)
        {
            const double lowerLimit = 0.001;

            // should not go to very large negative values, distributions flattens with sigma
            // causing numerical instabilities log/exp evaluations
            var rnd = new SquareRootProcessRNDCalculator(theta, kappa, theta, sigma);

            double vMin = Max(lowerLimit, rnd.StationaryInvCdf(eps));
            double lowEps = Max(eps, rnd.StationaryCdf(lowerLimit));

            double expected = 1 - eps - lowEps;
            double vMax = rnd.StationaryInvCdf(1 - eps);

            var mesher = new FdmMesherComposite(new Uniform1dMesher(Math.Log(vMin), Math.Log(vMax), vGrid));

            Vector v = mesher.Locations(0);

            var p = new double[vGrid];
            for (int i = 0; i < v.Size; ++i)
            {
                p[i] = StationaryLogProbabilityFct(kappa, theta, sigma, v[i]);
            }

            var op = new FdmSquareRootFwdOp(mesher, kappa, theta, sigma, 0, FdmSquareRootFwdOpTransformationType.Log);

            const int n = 100;
            const double dt = 0.01;
            var evolver = new DouglasScheme(0.5, op);
            evolver.SetStep(dt);

            for (int i = 1; i <= n; ++i)
            {
                evolver.Step(p, i * dt);
            }

            double calculated = new FdmMesherIntegral(mesher, SimpsonIntegrator).Integrate(new Vector(p));

            const double tol = 0.005;
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                "failed to reproduce stationary probability function for "
                + $"\n    sigma:      {sigma}\n    calculated: {calculated}\n    expected:   {expected}\n    tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestSquareRootFokkerPlanckFwdEquation()
    {
        // Testing Fokker-Planck forward equation for the square root process with Dirac start...

        const double kappa = 1.2;
        const double theta = 0.4;
        const double sigma = 0.7;
        const double v0 = theta;
        const double alpha = 1.0 - (2 * kappa * theta / (sigma * sigma));

        const double maturity = 1.0;

        const int xGrid = 1001;
        const int tGrid = 500;

        double vol = sigma * Math.Sqrt(theta / (2 * kappa));
        double upperBound = theta + (6 * vol);
        double lowerBound = Max(0.0002, theta - (6 * vol));

        var mesher = new FdmMesherComposite(new Uniform1dMesher(lowerBound, upperBound, xGrid));

        Vector x = mesher.Locations(0);

        var op = new FdmSquareRootFwdOp(mesher, kappa, theta, sigma, 0);

        const double dt = maturity / tGrid;
        const int n = 5;

        var p = new double[xGrid];
        var rndCalculator = new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma);
        for (int i = 0; i < p.Length; ++i)
        {
            p[i] = rndCalculator.Pdf(x[i], n * dt);
        }

        var q = new double[xGrid];
        for (int i = 0; i < xGrid; ++i)
        {
            q[i] = Math.Pow(x[i], alpha) * p[i];
        }

        var evolver = new DouglasScheme(0.5, op);
        evolver.SetStep(dt);

        for (double t = (n + 1) * dt; t <= maturity + (20 * QL_EPSILON); t += dt)
        {
            evolver.Step(p, t);
            evolver.Step(q, t);
        }

        const double tol = 0.002;

        for (int i = 0; i < x.Size; ++i)
        {
            double expected = rndCalculator.Pdf(x[i], maturity);
            double calculated = p[i];
            Assert.False(
                Math.Abs(expected - calculated) > tol,
                "failed to reproduce pdf at"
                + $"\n   x:          {x[i]:F5}\n   calculated: {calculated:F5}\n   expected:   {expected:F5}\n   tolerance:  {tol:F5}");
        }
    }

    [Fact]
    public void TestHestonFokkerPlanckFwdEquation()
    {
        // Testing Fokker-Planck forward equation for the Heston process...

        FokkerPlanckFwdTestCase[] testCases =
        [
            new(
                100.0, 0.01, 0.02,

                // Feller constraint violated, high vol-of-vol case
                // \frac{2\kappa\theta}{\sigma^2} = 2.0 * 1.0 * 0.05 / 0.2 = 0.5 < 1
                0.05, 1.0, 0.05, -0.75, Math.Sqrt(0.2),
                101, 401, 25, 25,
                0.02, 0.05,
                FdmSquareRootFwdOpTransformationType.Power,
                FdmHestonGreensFctAlgorithm.Gaussian,
                FdmSchemeDescFdmSchemeType.DouglasType),
            new(
                100.0, 0.01, 0.02,

                // Feller constraint violated, high vol-of-vol case
                // \frac{2\kappa\theta}{\sigma^2} = 2.0 * 1.0 * 0.05 / 0.2 = 0.5 < 1
                0.05, 1.0, 0.05, -0.75, Math.Sqrt(0.2),
                201, 501, 10, 10,
                0.005, 0.02,
                FdmSquareRootFwdOpTransformationType.Log,
                FdmHestonGreensFctAlgorithm.Gaussian,
                FdmSchemeDescFdmSchemeType.HundsdorferType),
            new(
                100.0, 0.01, 0.02,

                // Feller constraint violated, high vol-of-vol case
                // \frac{2\kappa\theta}{\sigma^2} = 2.0 * 1.0 * 0.05 / 0.2 = 0.5 < 1
                0.05, 1.0, 0.05, -0.75, Math.Sqrt(0.2),
                201, 501, 25, 25,
                0.01, 0.03,
                FdmSquareRootFwdOpTransformationType.Log,
                FdmHestonGreensFctAlgorithm.ZeroCorrelation,
                FdmSchemeDescFdmSchemeType.HundsdorferType),
            new(
                100.0, 0.01, 0.02,

                // Feller constraint fulfilled, low vol-of-vol case
                // \frac{2\kappa\theta}{\sigma^2} = 2.0 * 1.0 * 0.05 / 0.05 = 2.0 > 1
                0.05, 1.0, 0.05, -0.75, Math.Sqrt(0.05),
                201, 401, 5, 5,
                0.01, 0.02,
                FdmSquareRootFwdOpTransformationType.Plain,
                FdmHestonGreensFctAlgorithm.Gaussian,
                FdmSchemeDescFdmSchemeType.HundsdorferType),
        ];

        foreach (FokkerPlanckFwdTestCase testCase in testCases)
        {
            HestonFokkerPlanckFwdEquationTest(testCase);
        }
    }

    [Fact]
    public void TestHestonFokkerPlanckFwdEquationLogLVLeverage()
    {
        // Testing Fokker-Planck forward equation for the Heston process Log Transformation with leverage LV limiting case...

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(28, Month.December, 2012);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
        double maturity = dc.YearFraction(todaysDate, maturityDate);

        const double s0 = 100;
        double x0 = Math.Log(s0);
        const double r = 0.0;
        const double q = 0.0;

        const double kappa = 1.0;
        const double theta = 1.0;
        const double rho = -0.75;
        const double sigma = 0.02;
        const double v0 = theta;

        const FdmSquareRootFwdOpTransformationType transform = FdmSquareRootFwdOpTransformationType.Plain;

        DayCounter dayCounter = new Actual365Fixed();
        Calendar calendar = new TARGET();

        var rTS = new FlatForward(todaysDate, r, dayCounter);
        var qTS = new FlatForward(todaysDate, q, dayCounter);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        const int xGrid = 201;
        const int vGrid = 401;
        const int tGrid = 25;

        var rnd = new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma);

        double upperBound = rnd.StationaryInvCdf(0.99);
        double lowerBound = rnd.StationaryInvCdf(0.01);

        const double beta = 10.0;
        (double, double, bool)[] critPoints =
        [
            (lowerBound, beta, true),
            (v0, beta / 100, true),
            (upperBound, beta, true),
        ];
        var varianceMesher = new Concentrating1dMesher(lowerBound, upperBound, vGrid, critPoints);

        var equityMesher = new Concentrating1dMesher(Math.Log(2.0), Math.Log(600.0), xGrid, (x0 + 0.005, 0.1), true);

        var mesher = new FdmMesherComposite(equityMesher, varianceMesher);

        (double[] _, Date[] dates, BlackVarianceSurface surface) = CreateSmoothImpliedVol(dayCounter, calendar, todaysDate);

        var lvProcess = new BlackScholesMertonProcess(s0, qTS, rTS, surface);

        // step two days using non-correlated process
        const double eT = 2.0 / 365;

        double v = -NullReal, p_v = 0.0;
        var p = new double[mesher.Layout.Size];
        double bsV0 = Squared(lvProcess.BlackVolatility.BlackVol(0.0, s0, true));

        var rndCalculator = new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma);
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            double x = mesher.Location(iter, 0);
            if (v != mesher.Location(iter, 1))
            {
                v = mesher.Location(iter, 1);

                // the extreme tail probabilities of the non central
                // chi square distribution lead to numerical exceptions
                // on some platforms
                if (Math.Abs(v - v0) < 5 * sigma * Math.Sqrt(v0 * eT))
                {
                    p_v = rndCalculator.Pdf(v, eT);
                }
                else
                {
                    p_v = 0.0;
                }
            }

            double p_x = 1.0 / Math.Sqrt(M_TWOPI * bsV0 * eT)
                * Math.Exp(-0.5 * Squared(x - x0) / (bsV0 * eT));
            p[iter.Index] = p_v * p_x;
        }

        double dt = (maturity - eT) / tGrid;

        double[] denseStrikes =
        [
            2.222222222, 11.11111111, 20, 25, 30, 35, 40,
            44.44444444, 50, 55, 60, 65, 70, 75.55555556,
            80, 84.44444444, 88.88888889, 93.33333333, 97.77777778, 100,
            102.2222222, 106.6666667, 111.1111111, 115.5555556, 120,
            124.4444444, 166.6666667, 222.2222222, 444.4444444, 666.6666667,
        ];

        var times = new double[dates.Length];
        Matrix m = CreateLocalVolMatrixFromProcess(lvProcess, denseStrikes, dates, times, todaysDate);
        var leverage = new FixedLocalVolSurface(todaysDate, dates, denseStrikes, m, dc);

        var hestonFwdOp = new FdmHestonFwdOp(mesher, hestonProcess, transform, leverage);

        var evolver = new HundsdorferScheme(FdmSchemeDesc.Hundsdorfer().Theta, FdmSchemeDesc.Hundsdorfer().Mu, hestonFwdOp);

        double t = dt;
        evolver.SetStep(dt);

        for (int i = 0; i < tGrid; ++i, t += dt)
        {
            evolver.Step(p, t);
        }

        var exercise = new EuropeanExercise(maturityDate);

        for (int strike = 5; strike < 200; strike += 10)
        {
            var payoff = new CashOrNothingPayoff(OptionType.Put, strike, 1.0);

            var pd = new Vector(p.Length);
            FdmLinearOpIterator it = mesher.Layout.Begin();
            for (int k = 0; k < mesher.Layout.Size; ++k, it.Increment())
            {
                int idx = it.Index;
                double s = Math.Exp(mesher.Location(it, 0));

                pd[idx] = payoff.Evaluate(s) * p[idx];
            }

            double calculated = FokkerPlanckPrice2D(pd, mesher) * rTS.Discount(maturityDate);

            var option = new VanillaOption(payoff, exercise);
            double expected = FdBlackScholesVanillaEngine.Calculate(
                option, context, lvProcess, default, null, 50, 201, 0, FdmSchemeDesc.Douglas(), true, 0.2).NPV!.Value;

            const double tol = 0.015;
            Assert.False(
                Math.Abs(expected - calculated) > tol,
                "failed to reproduce Heston prices at"
                + $"\n   strike      {strike}\n   calculated: {calculated:F5}\n   expected:   {expected:F5}\n   tolerance:  {tol:F5}");
        }
    }

    [Fact]
    public void TestBlackScholesFokkerPlanckFwdEquationLocalVol()
    {
        // Testing Fokker-Planck forward equation for BS Local Vol process...

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, Month.July, 2014);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const double s0 = 100;
        double x0 = Math.Log(s0);
        const double r = 0.035;
        const double q = 0.01;

        Calendar calendar = new TARGET();
        DayCounter dayCounter = new Actual365Fixed();

        var rTS = new FlatForward(todaysDate, r, dayCounter);
        var qTS = new FlatForward(todaysDate, q, dayCounter);

        (double[] strikes, Date[] dates, BlackVarianceSurface vTS) = CreateSmoothImpliedVol(dayCounter, calendar, todaysDate);

        const int xGrid = 101;
        const int tGrid = 51;

        var process = new BlackScholesMertonProcess(s0, qTS, rTS, vTS);

        var localVol = new NoExceptLocalVolSurface(vTS, rTS, qTS, s0, 0.2);

        for (int i = 1; i < dates.Length; i += 2)
        {
            for (int j = 3; j < strikes.Length - 3; j += 2)
            {
                Date exDate = dates[i];
                Date maturityDate = exDate;
                double maturity = dc.YearFraction(todaysDate, maturityDate);
                var exercise = new EuropeanExercise(exDate);

                var uniformMesher = new FdmMesherComposite(new FdmBlackScholesMesher(xGrid, process, maturity, s0));

                // -- operator --
                var uniformBSFwdOp = new FdmLocalVolFwdOp(uniformMesher, s0, rTS, qTS, localVol);

                var concentratedMesher = new FdmMesherComposite(
                    new FdmBlackScholesMesher(xGrid, process, maturity, s0, null, null, 0.0001, 1.5, (s0, 0.1)));

                // -- operator --
                var concentratedBSFwdOp = new FdmLocalVolFwdOp(concentratedMesher, s0, rTS, qTS, localVol);

                var shiftedMesher = new FdmMesherComposite(
                    new FdmBlackScholesMesher(xGrid, process, maturity, s0, null, null, 0.0001, 1.5, (s0 * 1.1, 0.2)));

                // -- operator --
                var shiftedBSFwdOp = new FdmLocalVolFwdOp(shiftedMesher, s0, rTS, qTS, localVol);

                var payoff = new PlainVanillaPayoff(OptionType.Call, strikes[j]);

                var option = new VanillaOption(payoff, exercise);

                double expected = AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value;
                double calcUniform = FokkerPlanckPrice1D(uniformMesher, uniformBSFwdOp, payoff, x0, maturity, tGrid)
                    * rTS.Discount(maturityDate);
                double calcConcentrated = FokkerPlanckPrice1D(concentratedMesher, concentratedBSFwdOp, payoff, x0, maturity, tGrid)
                    * rTS.Discount(maturityDate);
                double calcShifted = FokkerPlanckPrice1D(shiftedMesher, shiftedBSFwdOp, payoff, x0, maturity, tGrid)
                    * rTS.Discount(maturityDate);
                const double tol = 0.05;

                Assert.False(
                    Math.Abs(expected - calcUniform) > tol,
                    "failed to reproduce european option price with an uniform mesher"
                    + $"\n   strike:     {strikes[i]}\n   calculated: {calcUniform:F8}\n   expected:   {expected:F8}\n   tolerance:  {tol:F8}");
                Assert.False(
                    Math.Abs(expected - calcConcentrated) > tol,
                    "failed to reproduce european option price with a concentrated mesher"
                    + $"\n   strike:     {strikes[i]}\n   calculated: {calcConcentrated:F8}\n   expected:   {expected:F8}\n   tolerance:  {tol:F8}");
                Assert.False(
                    Math.Abs(expected - calcShifted) > tol,
                    "failed to reproduce european option price with a shifted mesher"
                    + $"\n   strike:     {strikes[i]}\n   calculated: {calcShifted:F8}\n   expected:   {expected:F8}\n   tolerance:  {tol:F8}");
            }
        }
    }

    [Fact]
    public void TestLocalVolsvSLVPropDensity()
    {
        // Testing local volatility vs SLV model...

        var todaysDate = new Date(5, Month.October, 2015);
        Date finalDate = todaysDate + new Period(1, TimeUnit.Years);

        const double s0 = 100;
        const double r = 0.01;
        const double q = 0.02;

        Calendar calendar = new TARGET();
        DayCounter dayCounter = new Actual365Fixed();

        var rTS = new FlatForward(todaysDate, r, dayCounter);
        var qTS = new FlatForward(todaysDate, q, dayCounter);

        BlackVarianceSurface vTS = CreateSmoothImpliedVol(dayCounter, calendar, todaysDate).Surface;

        // Heston parameter from implied calibration
        const double kappa = 2.0;
        const double theta = 0.074;
        const double rho = -0.51;
        const double sigma = 0.8;
        const double v0 = 0.1974;

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var hestonModel = new HestonModel(hestonProcess);

        NoExceptLocalVolSurface localVol = new NoExceptLocalVolSurface(vTS, rTS, qTS, s0, 0.3).EnableExtrapolation(true);

        const int vGrid = 151;
        const int xGrid = 51;

        var fdmParams = new HestonSLVFokkerPlanckFdmParams(
            xGrid, vGrid, 500, 50, 100.0, 5, 2,
            0.1, 1e-4, 10000,
            1e-5, 1e-5, 0.0000025,
            1.0, 0.1, 0.9, 1e-5,
            FdmHestonGreensFctAlgorithm.ZeroCorrelation,
            FdmSquareRootFwdOpTransformationType.Log,
            FdmSchemeDesc.ModifiedCraigSneyd());

        var slvModel = new HestonSLVFDMModel(localVol, hestonModel, finalDate, fdmParams, true);

        var squareRootRndCalculator = new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma);

        foreach (HestonSLVFDMModelLogEntry logEntrie in slvModel.LogEntries)
        {
            double t = logEntrie.T;
            if (t > 0.2)
            {
                ImmutableArray<double> x = logEntrie.Mesher.GetFdm1dMeshers()[0].Locations();
                ImmutableArray<double> z = logEntrie.Mesher.GetFdm1dMeshers()[1].Locations();
                ImmutableArray<double> prob = logEntrie.Prob;

                for (int i = 0; i < z.Length; ++i)
                {
                    double pCalc = DiscreteSimpsonIntegral.Integrate(x.AsSpan(), prob.AsSpan(i * xGrid, xGrid));

                    double expected = squareRootRndCalculator.Pdf(Math.Exp(z[i]), t);
                    double calculated = pCalc / Math.Exp(z[i]);

                    Assert.False(
                        Math.Abs(expected - calculated) > 0.01 && Math.Abs((expected - calculated) / expected) > 0.04,
                        "failed to reproduce probability at "
                        + $"\n  v :          {Math.Exp(z[i])}\n  t :          {t}\n  expected :   {expected}\n  calculated : {calculated}");
                }
            }
        }
    }

    [Fact]
    public void TestBarrierPricingViaHestonLocalVol()
    {
        // Testing calibration via vanilla options...

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, Month.November, 2015);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const double S0 = 100;
        const double R = 0.1;
        const double Q = 0.025;

        const double Kappa = 2.0;
        const double Theta = 0.09;
        const double Rho = -0.75;
        const double Sigma = 0.8;
        const double V0 = 0.19;

        YieldTermStructure rTS = new FlatForward(todaysDate, R, dc);
        YieldTermStructure qTS = new FlatForward(todaysDate, Q, dc);

        var hestonProcess = new HestonProcess(rTS, qTS, S0, V0, Kappa, Theta, Sigma, Rho);

        var hestonModel = new HestonModel(hestonProcess);

        var surf = new HestonBlackVolSurface(hestonModel);

        double[] strikeValues = [50, 75, 100, 125, 150, 200, 400];
        Period[] maturities =
        [
            new(1, TimeUnit.Months), new(2, TimeUnit.Months),
            new(3, TimeUnit.Months), new(4, TimeUnit.Months),
            new(5, TimeUnit.Months), new(6, TimeUnit.Months),
            new(9, TimeUnit.Months), new(1, TimeUnit.Years),
            new(18, TimeUnit.Months), new(2, TimeUnit.Years),
            new(3, TimeUnit.Years), new(5, TimeUnit.Years),
        ];

        // QuantLib also builds a local volatility surface on surf, which the loop doesn't read

        var hestonEngine = new AnalyticHestonEngine(hestonModel, 164);

        foreach (double strike in strikeValues)
        {
            foreach (Period maturitie in maturities)
            {
                Date exerciseDate = todaysDate + maturitie;
                double t = dc.YearFraction(todaysDate, exerciseDate);

                double impliedVol = surf.BlackVol(t, strike, true);

                var bsProcess = new GeneralizedBlackScholesProcess(
                    S0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), impliedVol, dc));

                var exercise = new EuropeanExercise(exerciseDate);
                var payoff = new PlainVanillaPayoff(S0 < strike ? OptionType.Call : OptionType.Put, strike);

                var option = new VanillaOption(payoff, exercise);

                double analyticNPV = AnalyticEuropeanEngine.Calculate(option, context, bsProcess).NPV!.Value;

                double hestonNPV = hestonEngine.Calculate(option, context).NPV!.Value;

                double localVolNPV = FdBlackScholesVanillaEngine.Calculate(
                    option, context, bsProcess, default, null, 201, 801, 0, FdmSchemeDesc.Douglas(), true).NPV!.Value;

                const double Tol = 1e-3;
                Assert.False(
                    Math.Abs(analyticNPV - hestonNPV) > Tol,
                    "Heston and BS price do not match "
                    + $"\n  Heston :       {hestonNPV}\n  Black-Scholes: {analyticNPV}\n  diff :   {Math.Abs(analyticNPV - hestonNPV)}");

                Assert.False(
                    Math.Abs(analyticNPV - localVolNPV) > Tol,
                    "LocalVol and BS price do not match "
                    + $"\n  LocalVol :     {localVolNPV}\n  Black-Scholes: {analyticNPV}\n  diff :   {Math.Abs(analyticNPV - localVolNPV)}");
            }
        }
    }

    [Fact]
    public void TestMonteCarloVsFdmPricing()
    {
        // Testing Monte-Carlo vs FDM Pricing for Heston SLV models...

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, Month.December, 2015);
        Date exerciseDate = todaysDate + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const double s0 = 100;

        const double r = 0.05;
        const double q = 0.02;

        const double kappa = 2.0;
        const double theta = 0.18;
        const double rho = -0.75;
        const double sigma = 0.8;
        const double v0 = 0.19;

        var rTS = new FlatForward(todaysDate, r, dc);
        var qTS = new FlatForward(todaysDate, q, dc);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var hestonModel = new HestonModel(hestonProcess);

        var leverageFct = new LocalConstantVol(todaysDate, 0.25, dc);

        var slvProcess = new HestonSLVProcess(hestonProcess, leverageFct);

        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> mcEngine =
            new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(slvProcess, 1234)
                .WithStepsPerYear(100)
                .WithAntitheticVariate()
                .WithSamples(10000)
                .ToEngine();

        var exercise = new EuropeanExercise(exerciseDate);

        var mixingProcess = new HestonProcess(
            rTS, qTS, s0, v0, kappa, theta, sigma * 10, rho, HestonProcessDiscretization.QuadraticExponentialMartingale);

        var mixingModel = new HestonModel(mixingProcess);

        double[] strikes = [s0, 1.1 * s0];
        foreach (double strike in strikes)
        {
            var payoff = new PlainVanillaPayoff(OptionType.Call, strike);

            var option = new VanillaOption(payoff, exercise);

            double priceFDM = FdHestonVanillaEngine.Calculate(
                option, context, hestonModel, 51, 401, 101, 0, FdmSchemeDesc.ModifiedCraigSneyd(), leverageFct).NPV!.Value;

            double priceFDMWithMix = FdHestonVanillaEngine.Calculate(
                option, context, mixingModel, 51, 401, 101, 0, FdmSchemeDesc.ModifiedCraigSneyd(), leverageFct, 0.1).NPV!.Value;

            OneAssetOptionResults mc = mcEngine(option, context);
            double priceMC = mc.NPV!.Value;
            double priceError = mc.ErrorEstimate!.Value;

            Assert.False(
                priceError > 0.1,
                $"Heston Monte-Carlo error is too large\n MC Error: {priceError}\n Limit   : {0.1}");

            Assert.False(
                Math.Abs(priceFDM - priceMC) > 2.3 * priceError,
                "Heston Monte-Carlo price does not match with FDM"
                + $"\n MC Price : {priceMC}\n MC Error : {priceError}\n FDM Price: {priceFDM}");

            Assert.False(
                priceFDM != priceFDMWithMix,
                "Heston mixing FDM price does not match with non-mixing FDM"
                + $"\n Mixing FDM Price : {priceFDMWithMix}\n Non Mixing FDM Price : {priceFDM}");
        }
    }

    // Upstream: QuantLib's master's case for #2860.
    [Fact]
    public void TestMonteCarloCalibrationTimeDependentLocalVol()
    {
        // Testing Monte-Carlo calibration with time-dependent local volatility...

        var todaysDate = new Date(5, Month.January, 2016);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
        const double spot = 100.0;
        var rTS = new FlatForward(todaysDate, 0.05, dc);
        var qTS = new FlatForward(todaysDate, 0.02, dc);

        double[] times = [0.0, 0.5, 1.0];
        double[] strikes = [50.0, 150.0];
        var localVolMatrix = new Matrix(strikes.Length, times.Length);
        for (int i = 0; i < strikes.Length; ++i)
        {
            for (int j = 0; j < times.Length; ++j)
            {
                localVolMatrix[i, j] = 0.2 + (0.2 * times[j]);
            }
        }

        var localVol = new FixedLocalVolSurface(todaysDate, times, strikes, localVolMatrix, dc);

        // Almost deterministic, stationary variance gives L(t, S) = sigma_LV(t) / sqrt(v0).
        // A small positive vol-of-vol avoids the singular zero-vol-of-vol process limit.
        const double v0 = 0.04;
        var hestonProcess = new HestonProcess(rTS, qTS, spot, v0, 1.0, v0, 1e-6, 0.0);
        var hestonModel = new HestonModel(hestonProcess);
        var generator = new MTBrownianGeneratorFactory(1234);

        // Include a mandatory date to cover unequal time steps as well.
        Date[] mandatoryDates = [todaysDate + 73];
        double maturity = dc.YearFraction(todaysDate, maturityDate);
        double[] gridTimes = [dc.YearFraction(todaysDate, mandatoryDates[0]), maturity];
        const int timeStepsPerYear = 4;
        var grid = new TimeGrid(gridTimes, (int)(maturity * timeStepsPerYear));
        LocalVolTermStructure leverage = new HestonSLVMCModel(
            localVol, hestonModel, generator, maturityDate, timeStepsPerYear, 16, 2048, mandatoryDates).LeverageFunction;

        // Allow for the small residual variance randomness; a one-step lag is much larger.
        const double tolerance = 1e-4;
        foreach (double t in grid)
        {
            double expected = localVol.LocalVol(t, spot, true) / Math.Sqrt(v0);
            foreach (double strike in new[] { 80.0, 100.0, 120.0 })
            {
                double calculated = leverage.LocalVol(t, strike, true);
                Assert.True(Math.Abs(calculated - expected) <= tolerance, $"t {t}, strike {strike}: {calculated} for {expected}");
            }
        }
    }

    [Fact]
    public void TestMonteCarloCalibration()
    {
        // Testing Monte-Carlo Calibration...

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, Month.January, 2016);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const double s0 = 100;

        const double r = 0.05;
        const double q = 0.02;

        var rTS = new FlatForward(todaysDate, r, dc);
        var qTS = new FlatForward(todaysDate, q, dc);

        var localVol = new LocalConstantVol(todaysDate, 0.3, dc);

        // parameter of the "calibrated" Heston model
        const double kappa = 1.0;
        const double theta = 0.06;
        const double rho = -0.75;
        const double sigma = 0.4;
        const double v0 = 0.09;

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var hestonModel = new HestonModel(hestonProcess);

        const int xGrid = 400;
        int[] nSims = [40000];

        foreach (int nSim in nSims)
        {
            const bool sobol = true;

            LocalVolTermStructure leverageFct = new HestonSLVMCModel(
                localVol,
                hestonModel,
                sobol
                    ? new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 1234, SobolRsgDirectionIntegers.JoeKuoD7)
                    : new MTBrownianGeneratorFactory(1234),
                maturityDate,
                91,
                xGrid,
                nSim).LeverageFunction;

            var bsProcess = new GeneralizedBlackScholesProcess(
                s0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), 0.3, dc));

            double[] strikes = [50, 80, 100, 120, 150, 200];
            Date[] maturities =
            [
                todaysDate + new Period(3, TimeUnit.Months), todaysDate + new Period(6, TimeUnit.Months),
                todaysDate + new Period(12, TimeUnit.Months),
            ];

            double qualityFactor = 0.0;
            double maxQualityFactor = 0.0;
            int nValues = 0;

            foreach (Date maturity in maturities)
            {
                double maturityTime = dc.YearFraction(todaysDate, maturity);

                var exercise = new EuropeanExercise(maturity);

                foreach (double strike in strikes)
                {
                    var payoff = new PlainVanillaPayoff(strike < s0 ? OptionType.Put : OptionType.Call, strike);

                    var option = new VanillaOption(payoff, exercise);

                    OneAssetOptionResults bs = AnalyticEuropeanEngine.Calculate(option, context, bsProcess);
                    double bsNPV = bs.NPV!.Value;
                    double bsVega = bs.Vega!.Value;

                    if (bsNPV > 0.02)
                    {
                        double fdmNPV = FdHestonVanillaEngine.Calculate(
                            option,
                            context,
                            hestonModel,
                            Math.Max(26, (int)(maturityTime * 51)),
                            201,
                            51,
                            0,
                            FdmSchemeDesc.ModifiedCraigSneyd(),
                            leverageFct).NPV!.Value;

                        double diff = Math.Abs(fdmNPV - bsNPV) / bsVega * 1e4;
                        qualityFactor += diff;
                        maxQualityFactor = Max(maxQualityFactor, diff);
                        ++nValues;
                    }
                }
            }

            Assert.False(
                qualityFactor / nValues > 7.5,
                "Failed to reproduce average calibration quality"
                + $"\n average calibration quality : {qualityFactor / nValues}bp\n tolerance                  :  5.0bp");

            Assert.False(
                qualityFactor / nValues > 15.0,
                "Failed to reproduce maximum calibration error"
                + $"\n maximum calibration error : {maxQualityFactor}bp\n tolerance                 : 15.0bp");
        }
    }

    [Fact]
    public void TestMoustacheGraph()
    {
        // Testing double no touch pricing with SLV and mixing...

        /*
         A more detailed description of this test case can found on
         https://hpcquantlib.wordpress.com/2016/01/10/monte-carlo-calibration-of-the-heston-stochastic-local-volatiltiy-model/

         The comparison of Black-Scholes and SLV prices is derived
         from figure 8.8 in Iain J. Clark's book,
         Foreign Exchange Option Pricing: A Practitioner’s Guide
        */

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(5, Month.January, 2016);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
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

        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), r, dc, context);
        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), q, dc, context);

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho));

        var europeanExercise = new EuropeanExercise(maturityDate);

        var vanillaOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), europeanExercise);

        double implVol = vanillaOption.ImpliedVolatility(
            AnalyticHestonEngine.Calculate(vanillaOption, context, hestonModel).NPV!.Value,
            new GeneralizedBlackScholesProcess(
                s0, qTS, rTS, BlackConstantVol.Create(0, new NullCalendar(), Math.Sqrt(theta), dc, context)),
            context);

        var analyticProcess = new GeneralizedBlackScholesProcess(
            s0, qTS, rTS, BlackConstantVol.Create(0, new NullCalendar(), implVol, dc, context));

        List<double> expiries = [];
        var timeStepPeriod = new Period(1, TimeUnit.Weeks);
        for (Date expiry = todaysDate + timeStepPeriod; expiry <= maturityDate; expiry += timeStepPeriod)
        {
            expiries.Add(dc.YearFraction(todaysDate, expiry));
        }

        var timeGrid = new TimeGrid([.. expiries]);

        // first build the true local vol surface from another Heston model
        LocalVolTermStructure localVol = GetFixedLocalVolFromHeston(hestonModel, timeGrid);

        var sobolGeneratorFactory = new SobolBrownianGeneratorFactory(
            SobolBrownianGeneratorBaseOrdering.Diagonal, 1234, SobolRsgDirectionIntegers.JoeKuoD7);

        const int xGrid = 100;
        const int nSim = 20000;

        const double eta = 0.90;

        var modHestonModel = new HestonModel(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, eta * sigma, rho));

        LocalVolTermStructure leverageFct = new HestonSLVMCModel(
            localVol, modHestonModel, sobolGeneratorFactory, maturityDate, 182, xGrid, nSim).LeverageFunction;

        double[] expected =
        [
            0.0334, 0.1141, 0.1319, 0.0957, 0.0464, 0.0058, -0.0192,
            -0.0293, -0.0297, -0.0251, -0.0192, -0.0134, -0.0084, -0.0045,
            -0.0015, 0.0005, 0.0017, 0.0020,
        ];
        const double tol = 1e-2;

        for (int i = 0; i < 18; ++i)
        {
            double dist = 10.0 + (5.0 * i);

            double barrierLo = Max(s0 - dist, 1e-2);
            double barrierHi = s0 + dist;
            var doubleBarrier = new DoubleBarrierOption(
                DoubleBarrierType.KnockOut,
                barrierLo,
                barrierHi,
                0.0,
                new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0),
                europeanExercise);

            double bsNPV = AnalyticDoubleBarrierBinaryEngine.Calculate(doubleBarrier, context, analyticProcess).NPV!.Value;

            double slvNPV = FdHestonDoubleBarrierEngine.Calculate(
                doubleBarrier, context, modHestonModel, 51, 101, 31, 0, FdmSchemeDesc.Hundsdorfer(), leverageFct).NPV!.Value;

            double diff = slvNPV - bsNPV;
            Assert.False(
                Math.Abs(diff - expected[i]) > tol,
                "Failed to reproduce price difference for a Double-No-Touch option between Black-Scholes and Heston "
                + $"Stochastic Local Volatility model\n Barrier Low        : {barrierLo}\n Barrier High       : {barrierHi}"
                + $"\n Black-Scholes Price: {bsNPV}\n Heston SLV Price   : {slvNPV}\n diff               : {diff}"
                + $"\n expected diff      : {expected[i]}\n tolerance          : {tol}");
        }
    }

    [Fact]
    public void TestDiffusionAndDriftSlvProcess()
    {
        // Testing diffusion and drift of the SLV process...

        var todaysDate = new Date(6, Month.June, 2020);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        DayCounter dc = new Actual365Fixed();
        Date maturityDate = todaysDate + new Period(6, TimeUnit.Months);
        double maturity = dc.YearFraction(todaysDate, maturityDate);

        const double s0 = 100;
        const double r = -0.005;
        const double q = 0.04;

        var rTS = new FlatForward(todaysDate, r, dc);
        var qTS = new FlatForward(todaysDate, q, dc);

        LocalVolTermStructure localVol = GetFixedLocalVolFromHeston(
            new HestonModel(new HestonProcess(rTS, qTS, s0, 0.1, 1.0, 0.13, 0.8, 0.4)), new TimeGrid(maturity, 20));

        const double kappa = 2.5;
        const double theta = 1.0;
        const double rho = -0.75;
        const double sigma = 2.4;
        const double v0 = 1.0;

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var hestonModel = new HestonModel(hestonProcess);

        var slvProcess = new HestonSLVProcess(hestonProcess, localVol);

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), new EuropeanExercise(maturityDate));

        double expected = FdHestonVanillaEngine.Calculate(
            option, context, hestonModel, 26, 201, 101, 0, FdmSchemeDesc.ModifiedCraigSneyd(), localVol).NPV!.Value;

        const int nSims = 16733;
        const int nTimeSteps = 40;
        double df = rTS.Discount(maturity);

        var rsg = new SobolBrownianBridgeRsg(
            2, nTimeSteps, SobolBrownianGeneratorBaseOrdering.Diagonal, 12345, SobolRsgDirectionIntegers.JoeKuoD7);

        var stats = new GeneralStatistics();

        double dt = maturity / nTimeSteps;
        double sqrtDt = Math.Sqrt(dt);

        for (int i = 0; i < nSims; ++i)
        {
            double t = 0.0;
            var x = new Vector([s0, v0]);

            ReadOnlySpan<double> n = rsg.NextSequence().Value.Span;

            for (int j = 0; j < nTimeSteps; ++j, t += dt)
            {
                var dw = new Vector([n[j], n[j + nTimeSteps]]);

                // full truncation scheme
                var xt = new Vector([x[0], (x[1] > 0) ? x[1] : 0.0]);

                x = slvProcess.Apply(x, (slvProcess.Diffusion(t, xt) * sqrtDt * dw) + (slvProcess.Drift(t, xt) * dt));
            }

            stats.Add(df * option.Payoff!.Evaluate(x[0]));
        }

        double calculated = stats.Mean();
        double errorEstimate = stats.ErrorEstimate();

        double diff = Math.Abs(expected - calculated);

        Assert.False(
            diff > 2.35 * errorEstimate,
            "Failed to reproduce call option price with HestonSLVProcess diffusion and drift discretization scheme"
            + $"\n expected   : {expected}\n calculated : {calculated}\n error est. : {errorEstimate}\n diff       : {diff}");
    }

    // The test suite's getFixedLocalVolFromHeston: the Heston model's local volatility, from its implied volatility
    // surface, sampled on the strikes of a local-volatility density calculator's meshers at each time of the grid.
    private static FixedLocalVolSurface GetFixedLocalVolFromHeston(HestonModel hestonModel, TimeGrid timeGrid)
    {
        var trueImpliedVolSurf = new HestonBlackVolSurface(
            hestonModel, AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg, FourierIntegration.GaussLaguerre(32));

        HestonProcess hestonProcess = hestonModel.Process;

        var localVol = new NoExceptLocalVolSurface(
            trueImpliedVolSurf,
            hestonProcess.RiskFreeRate,
            hestonProcess.DividendYield,
            hestonProcess.S0,
            Math.Sqrt(hestonProcess.Theta));

        var localVolRND = new LocalVolRNDCalculator(
            hestonProcess.S0, hestonProcess.RiskFreeRate, hestonProcess.DividendYield, localVol, timeGrid);

        List<double[]> strikes = [];
        for (int i = 1; i < timeGrid.Size; ++i)
        {
            double t = timeGrid[i];
            Fdm1dMesher fdm1dMesher = localVolRND.Mesher(t);

            ImmutableArray<double> logStrikes = fdm1dMesher.Locations();
            double[] strikeSlice = new double[logStrikes.Length];

            for (int j = 0; j < logStrikes.Length; ++j)
            {
                strikeSlice[j] = Math.Exp(logStrikes[j]);
            }

            strikes.Add(strikeSlice);
        }

        int nStrikes = strikes[0].Length;
        var localVolMatrix = new Matrix(nStrikes, timeGrid.Size - 1);
        for (int i = 1; i < timeGrid.Size; ++i)
        {
            double t = timeGrid[i];
            double[] strikeSlice = strikes[i - 1];

            for (int j = 0; j < nStrikes; ++j)
            {
                double s = strikeSlice[j];
                localVolMatrix[j, i - 1] = localVol.LocalVol(t, s, true);
            }
        }

        Date todaysDate = hestonProcess.RiskFreeRate.ReferenceDate;
        DayCounter dc = hestonProcess.RiskFreeRate.DayCounter;

        List<double> expiries = [];
        for (int i = 1; i < timeGrid.Size; ++i)
        {
            expiries.Add(timeGrid[i]);
        }

        return new FixedLocalVolSurface(todaysDate, expiries, strikes, localVolMatrix, dc);
    }

    private static double FokkerPlanckPrice1D(
        FdmMesher mesher, FdmLinearOpComposite op, StrikedTypePayoff payoff, double x0, double maturity, int tGrid)
    {
        Vector x = mesher.Locations(0);
        var p = new Vector(x.Size, 0.0);

        Assert.True(x.Size > 3 && x[1] <= x0 && x[x.Size - 2] >= x0, "insufficient mesher");

        int upperb = 0;
        while (upperb < x.Size && !(x[upperb] > x0))
        {
            ++upperb;
        }

        int lowerb = upperb - 1;

        if (ComparisonFunctions.CloseEnough(x[upperb], x0))
        {
            int idx = upperb;
            double dx = (x[idx + 1] - x[idx - 1]) / 2.0;
            p[idx] = 1.0 / dx;
        }
        else if (ComparisonFunctions.CloseEnough(x[lowerb], x0))
        {
            int idx = lowerb;
            double dx = (x[idx + 1] - x[idx - 1]) / 2.0;
            p[idx] = 1.0 / dx;
        }
        else
        {
            double dx = x[upperb] - x[lowerb];
            double lowerP = (x[upperb] - x0) / dx;
            double upperP = (x0 - x[lowerb]) / dx;

            int lowerIdx = lowerb;
            int upperIdx = upperb;

            double lowerDx = (x[lowerIdx + 1] - x[lowerIdx - 1]) / 2.0;
            double upperDx = (x[upperIdx + 1] - x[upperIdx - 1]) / 2.0;

            p[lowerIdx] = lowerP / lowerDx;
            p[upperIdx] = upperP / upperDx;
        }

        var evolver = new DouglasScheme(FdmSchemeDesc.Douglas().Theta, op);
        double dt = maturity / tGrid;
        evolver.SetStep(dt);

        for (double t = dt; t <= maturity + (20 * QL_EPSILON); t += dt)
        {
            evolver.Step(p.AsSpan(), t);
        }

        var payoffTimesDensity = new double[x.Size];
        for (int i = 0; i < x.Size; ++i)
        {
            payoffTimesDensity[i] = payoff.Evaluate(Math.Exp(x[i])) * p[i];
        }

        CubicNaturalSpline f = new CubicNaturalSpline(x.AsSpan(), payoffTimesDensity).EnableExtrapolation();
        return new GaussLobattoIntegral(1000, 1e-6).Integrate(u => f.Evaluate(u), x[0], x[x.Size - 1]);
    }

    private static double StationaryLogProbabilityFct(double kappa, double theta, double sigma, double z)
    {
        double alpha = 2 * kappa * theta / (sigma * sigma);
        double beta = alpha / theta;

        return Math.Pow(beta, alpha) * Math.Exp(z * alpha)
            * Math.Exp((-beta * Math.Exp(z)) - new GammaFunction().LogValue(alpha));
    }

    private static FdmMesher CreateStationaryDistributionMesher(double kappa, double theta, double sigma, int vGrid)
    {
        const double qMin = 0.01;
        const double qMax = 0.99;
        double dq = (qMax - qMin) / (vGrid - 1);

        var rnd = new SquareRootProcessRNDCalculator(theta, kappa, theta, sigma);
        var v = new double[vGrid];
        for (int i = 0; i < vGrid; ++i)
        {
            v[i] = rnd.StationaryInvCdf(qMin + (i * dq));
        }

        return new FdmMesherComposite(new Predefined1dMesher(v));
    }

    private static double FokkerPlanckPrice2D(Vector p, FdmMesherComposite mesher) =>
        new FdmMesherIntegral(mesher, SimpsonIntegrator).Integrate(p);

    // DiscreteSimpsonIntegral() as FdmMesherIntegral's one-dimensional integrator
    private static double SimpsonIntegrator(Vector x, Vector f) => DiscreteSimpsonIntegral.Integrate(x.AsSpan(), f.AsSpan());

    private static double HestonPxBoundary(double maturity, double eps, HestonModel model)
    {
        var pdfEngine = new AnalyticPDFHestonEngine(model);
        double sInit = model.Process.S0;
        double xMin = new Brent().Solve(
            x => pdfEngine.Cdf(x, maturity) - eps, sInit * 1e-3, sInit, sInit * 0.001, 1000 * sInit);

        return xMin;
    }

    private static void HestonFokkerPlanckFwdEquationTest(FokkerPlanckFwdTestCase testCase)
    {
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var todaysDate = new Date(28, Month.December, 2014);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        Period[] maturities =
        [
            new(1, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(9, TimeUnit.Months),
            new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years),
        ];

        Date maturityDate = todaysDate + maturities[^1];
        double maturity = dc.YearFraction(todaysDate, maturityDate);

        double s0 = testCase.S0;
        double x0 = Math.Log(s0);
        double r = testCase.R;
        double q = testCase.Q;

        double kappa = testCase.Kappa;
        double theta = testCase.Theta;
        double rho = testCase.Rho;
        double sigma = testCase.Sigma;
        double v0 = testCase.V0;
        double alpha = 1.0 - (2 * kappa * theta / (sigma * sigma));

        var rTS = new FlatForward(todaysDate, r, dc);
        var qTS = new FlatForward(todaysDate, q, dc);

        var process = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var model = new HestonModel(process);

        int xGrid = testCase.XGrid;
        int vGrid = testCase.VGrid;
        int tGridPerYear = testCase.TGridPerYear;

        FdmSquareRootFwdOpTransformationType transformationType = testCase.TrafoType;
        double lowerBound, upperBound;
        (double, double, bool)[] cPoints;

        var rnd = new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma);
        switch (transformationType)
        {
            case FdmSquareRootFwdOpTransformationType.Log:
                {
                    upperBound = Math.Log(rnd.StationaryInvCdf(0.9995));
                    lowerBound = Math.Log(0.00001);

                    double v0Center = Math.Log(v0);
                    const double v0Density = 10.0;
                    const double upperBoundDensity = 100;
                    const double lowerBoundDensity = 1.0;
                    cPoints =
                    [
                        (lowerBound, lowerBoundDensity, false),
                        (v0Center, v0Density, true),
                        (upperBound, upperBoundDensity, false),
                    ];
                }

                break;
            case FdmSquareRootFwdOpTransformationType.Plain:
                {
                    upperBound = rnd.StationaryInvCdf(0.9995);
                    lowerBound = rnd.StationaryInvCdf(1e-5);

                    double v0Center = v0;
                    const double v0Density = 0.1;
                    const double lowerBoundDensity = 0.0001;
                    cPoints =
                    [
                        (lowerBound, lowerBoundDensity, false),
                        (v0Center, v0Density, true),
                    ];
                }

                break;
            case FdmSquareRootFwdOpTransformationType.Power:
                {
                    upperBound = rnd.StationaryInvCdf(0.9995);
                    lowerBound = 0.000075;

                    double v0Center = v0;
                    const double v0Density = 1.0;
                    const double lowerBoundDensity = 0.005;
                    cPoints =
                    [
                        (lowerBound, lowerBoundDensity, false),
                        (v0Center, v0Density, true),
                    ];
                }

                break;
            default:
                throw new NanoQLException("unknown transformation type");
        }

        var varianceMesher = new Concentrating1dMesher(lowerBound, upperBound, vGrid, cPoints, 1e-12);

        const double sEps = 1e-4;
        double sLowerBound = Math.Log(HestonPxBoundary(maturity, sEps, model));
        double sUpperBound = Math.Log(HestonPxBoundary(maturity, 1 - sEps, model));

        var spotMesher = new Concentrating1dMesher(sLowerBound, sUpperBound, xGrid, (x0, 0.1), true);

        var mesher = new FdmMesherComposite(spotMesher, varianceMesher);

        var hestonFwdOp = new FdmHestonFwdOp(mesher, process, transformationType);

        var evolver = new ModifiedCraigSneydScheme(
            FdmSchemeDesc.ModifiedCraigSneyd().Theta, FdmSchemeDesc.ModifiedCraigSneyd().Mu, hestonFwdOp);

        // step one days using non-correlated process
        const double eT = 1.0 / 365;
        Vector p = new FdmHestonGreensFct(mesher, process, testCase.TrafoType).Get(eT, testCase.GreensAlgorithm);

        double[] strikes = [50, 80, 90, 100, 110, 120, 150, 200];

        double t = eT;
        foreach (Period maturitie in maturities)
        {
            // calculate step size
            Date nextMaturityDate = todaysDate + maturitie;
            double nextMaturityTime = dc.YearFraction(todaysDate, nextMaturityDate);

            double dt = (nextMaturityTime - t) / tGridPerYear;
            evolver.SetStep(dt);

            for (int i = 0; i < tGridPerYear; ++i, t += dt)
            {
                evolver.Step(p.AsSpan(), t + dt);
            }

            double avg = 0, min = QL_MAX_REAL, max = 0;
            foreach (double strike in strikes)
            {
                var payoff = new PlainVanillaPayoff((strike > s0) ? OptionType.Call : OptionType.Put, strike);

                var pd = new Vector(p.Size);
                FdmLinearOpIterator iter = mesher.Layout.Begin();
                for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
                {
                    int idx = iter.Index;
                    double s = Math.Exp(mesher.Location(iter, 0));

                    pd[idx] = payoff.Evaluate(s) * p[idx];
                    if (transformationType == FdmSquareRootFwdOpTransformationType.Power)
                    {
                        double v = mesher.Location(iter, 1);
                        pd[idx] *= Math.Pow(v, -alpha);
                    }
                }

                double calculated = FokkerPlanckPrice2D(pd, mesher) * rTS.Discount(nextMaturityDate);

                var exercise = new EuropeanExercise(nextMaturityDate);

                var option = new VanillaOption(payoff, exercise);

                double expected = AnalyticHestonEngine.Calculate(option, context, model).NPV!.Value;
                double absDiff = Math.Abs(expected - calculated);
                double relDiff = absDiff / Max(QL_EPSILON, expected);
                double diff = Min(absDiff, relDiff);

                avg += diff;
                min = Min(diff, min);
                max = Max(diff, max);

                Assert.False(
                    diff > testCase.Eps,
                    "failed to reproduce Heston SLV prices at"
                    + $"\n   strike      {strike}\n   kappa       {kappa}\n   theta       {theta}\n   rho         {rho}"
                    + $"\n   sigma       {sigma}\n   v0          {v0}\n   transform   {transformationType}"
                    + $"\n   calculated: {calculated:F5}\n   expected:   {expected:F5}\n   tolerance:  {testCase.Eps:F5}");
            }

            avg /= strikes.Length;

            Assert.False(
                avg > testCase.AvgEps,
                "failed to reproduce Heston SLV prices on average at"
                + $"\n   kappa       {kappa}\n   theta       {theta}\n   rho         {rho}\n   sigma       {sigma}"
                + $"\n   v0          {v0}\n   transform   {transformationType}"
                + $"\n   average diff: {avg:F5}\n   tolerance:  {testCase.AvgEps:F5}");
        }
    }

    private static Matrix CreateLocalVolMatrixFromProcess(
        BlackScholesMertonProcess lvProcess, double[] strikes, Date[] dates, double[] times, Date todaysDate)
    {
        LocalVolTermStructure localVol = lvProcess.LocalVolatility;

        DayCounter dc = localVol.DayCounter;

        Assert.True(times.Length == dates.Length, "mismatch");

        for (int i = 0; i < times.Length; ++i)
        {
            times[i] = dc.YearFraction(todaysDate, dates[i]);
        }

        var surface = new Matrix(strikes.Length, dates.Length);

        for (int i = 0; i < strikes.Length; ++i)
        {
            for (int j = 0; j < dates.Length; ++j)
            {
                try
                {
                    surface[i, j] = localVol.LocalVol(dates[j], strikes[i], true);
                }
                catch (NanoQLException)
                {
                    surface[i, j] = 0.2;
                }
            }
        }

        return surface;
    }

    private static (double[] Strikes, Date[] Dates, BlackVarianceSurface Surface) CreateSmoothImpliedVol(
        DayCounter dc, Calendar cal, Date todaysDate)
    {
        int[] times = [13, 41, 75, 165, 256, 345, 524, 703];
        Date[] dates = [.. times.Select(time => todaysDate + time)];

        double[] surfaceStrikes =
        [
            2.222222222, 11.11111111, 44.44444444, 75.55555556, 80, 84.44444444, 88.88888889, 93.33333333, 97.77777778, 100,
            102.2222222, 106.6666667, 111.1111111, 115.5555556, 120, 124.4444444, 166.6666667, 222.2222222, 444.4444444, 666.6666667,
        ];

        double[] v =
        [
            1.015873, 1.015873, 0.915873, 0.89729, 0.796493, 0.730914, 0.631335, 0.568895,
            0.851309, 0.821309, 0.781309, 0.641309, 0.635593, 0.583653, 0.508045, 0.463182,
            0.686034, 0.630534, 0.590534, 0.500534, 0.448706, 0.416661, 0.375470, 0.353442,
            0.526034, 0.482263, 0.447713, 0.387703, 0.355064, 0.337438, 0.316966, 0.306859,
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

        var blackVolMatrix = new Matrix(surfaceStrikes.Length, dates.Length);
        for (int i = 0; i < surfaceStrikes.Length; ++i)
        {
            for (int j = 0; j < dates.Length; ++j)
            {
                blackVolMatrix[i, j] = v[(i * dates.Length) + j];
            }
        }

        BlackVarianceSurface volTS = new BlackVarianceSurface(
            todaysDate,
            cal,
            dates,
            surfaceStrikes,
            blackVolMatrix,
            dc,
            BlackVarianceSurfaceExtrapolation.ConstantExtrapolation,
            BlackVarianceSurfaceExtrapolation.ConstantExtrapolation).SetInterpolation<Bicubic>();

        return (surfaceStrikes, dates, volTS);
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;

    // std::min
    private static double Min(double a, double b) => b < a ? b : a;

    private static double Squared(double x) => x * x;

    private sealed class QFct
    {
        private readonly double[] q;
        private readonly double alpha;
        private readonly CubicNaturalSpline spline;

        internal QFct(Vector v, double[] p, double alpha)
        {
            q = new double[v.Size];
            for (int i = 0; i < v.Size; ++i)
            {
                q[i] = Math.Pow(v[i], alpha) * p[i];
            }

            this.alpha = alpha;
            spline = new CubicNaturalSpline(v.AsSpan(), q);
        }

        internal double Evaluate(double v) => spline.Evaluate(v, true) * Math.Pow(v, -alpha);
    }

    private sealed record FokkerPlanckFwdTestCase(
        double S0,
        double R,
        double Q,
        double V0,
        double Kappa,
        double Theta,
        double Rho,
        double Sigma,
        int XGrid,
        int VGrid,
        int TGridPerYear,
        int TMinGridPerYear,
        double AvgEps,
        double Eps,
        FdmSquareRootFwdOpTransformationType TrafoType,
        FdmHestonGreensFctAlgorithm GreensAlgorithm,
        FdmSchemeDescFdmSchemeType SchemeType);
}
