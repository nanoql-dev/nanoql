// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/riskneutraldensitycalculator.cpp @ v1.44
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
public class RiskNeutralDensityCalculatorTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // M_SQRT2 (qldefines.hpp)
    private const double M_SQRT2 = 1.41421356237309504880;

    [Fact]
    public void TestDensityAgainstOptionPrices()
    {
        // Testing density against option prices...

        DayCounter dayCounter = new Actual365Fixed();
        Date todaysDate = Date.TodaysDate();

        const Real s0 = 100;

        const Real r = 0.075;
        const Real q = 0.04;
        const Real v = 0.27;

        var rTS = new FlatForward(todaysDate, r, dayCounter);

        var qTS = new FlatForward(todaysDate, q, dayCounter);

        var bsmProcess = new BlackScholesMertonProcess(s0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), v, dayCounter));

        var bsm = new BSMRNDCalculator(bsmProcess);
        Real[] times = [0.5, 1.0, 2.0];
        Real[] strikes = [75.0, 100.0, 150.0];

        foreach (Real t in times)
        {
            Real stdDev = v * Math.Sqrt(t);
            Real df = rTS.Discount(t);
            Real fwd = s0 * qTS.Discount(t) / df;

            foreach (Real strike in strikes)
            {
                Real xs = Math.Log(strike);
                var blackCalc = new BlackCalculator(OptionType.Put, strike, fwd, stdDev, df);

                Real tol = 10 * Math.Sqrt(QL_EPSILON);
                Real calculatedCDF = bsm.Cdf(xs, t);
                Real expectedCDF = blackCalc.StrikeSensitivity / df;

                Assert.False(
                    Math.Abs(calculatedCDF - expectedCDF) > tol,
                    "failed to reproduce Black-Scholes-Merton cdf"
                    + $"\n   calculated: {calculatedCDF}\n   expected:   {expectedCDF}"
                    + $"\n   diff:       {calculatedCDF - expectedCDF}\n   tol:        {tol}");

                Real deltaStrike = strike * Math.Sqrt(QL_EPSILON);

                Real calculatedPDF = bsm.Pdf(xs, t);
                Real expectedPDF = strike / df
                    * (new BlackCalculator(OptionType.Put, strike + deltaStrike, fwd, stdDev, df).StrikeSensitivity
                        - new BlackCalculator(OptionType.Put, strike - deltaStrike, fwd, stdDev, df).StrikeSensitivity)
                    / (2 * deltaStrike);

                Assert.False(
                    Math.Abs(calculatedPDF - expectedPDF) > tol,
                    "failed to reproduce Black-Scholes-Merton pdf"
                    + $"\n   calculated: {calculatedPDF}\n   expected:   {expectedPDF}"
                    + $"\n   diff:       {calculatedPDF - expectedPDF}\n   tol:        {tol}");
            }
        }
    }

    [Fact]
    public void TestBSMagainstHestonRND()
    {
        // Testing Black-Scholes-Merton and Heston densities...

        DayCounter dayCounter = new Actual365Fixed();
        Date todaysDate = Date.TodaysDate();

        const Real s0 = 10;

        const Real r = 0.155;
        const Real q = 0.0721;
        const Real v = 0.27;

        const Real kappa = 1.0;
        const Real theta = v * v;
        const Real rho = -0.75;
        const Real v0 = v * v;
        const Real sigma = 0.0001;

        var rTS = new FlatForward(todaysDate, r, dayCounter);
        var qTS = new FlatForward(todaysDate, q, dayCounter);

        var bsmProcess = new BlackScholesMertonProcess(s0, qTS, rTS, new BlackConstantVol(todaysDate, new NullCalendar(), v, dayCounter));

        var bsm = new BSMRNDCalculator(bsmProcess);
        var heston = new HestonRNDCalculator(new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho), 1e-8);

        Real[] times = [0.5, 1.0, 2.0];
        Real[] strikes = [7.5, 10, 15];
        Real[] probs = [1e-6, 0.01, 0.5, 0.99, 1.0 - 1e-6];

        foreach (Real t in times)
        {
            foreach (Real strike in strikes)
            {
                Real xs = Math.Log(strike);

                Real expectedPDF = bsm.Pdf(xs, t);
                Real calculatedPDF = heston.Pdf(xs, t);

                const Real tol = 1e-4;
                Assert.False(
                    Math.Abs(expectedPDF - calculatedPDF) > tol,
                    "failed to reproduce Black-Scholes-Merton pdf with the Heston model"
                    + $"\n   calculated: {calculatedPDF}\n   expected:   {expectedPDF}"
                    + $"\n   diff:       {calculatedPDF - expectedPDF}\n   tol:        {tol}");

                Real expectedCDF = bsm.Cdf(xs, t);
                Real calculatedCDF = heston.Cdf(xs, t);

                Assert.False(
                    Math.Abs(expectedCDF - calculatedCDF) > tol,
                    "failed to reproduce Black-Scholes-Merton cdf with the Heston model"
                    + $"\n   calculated: {calculatedCDF}\n   expected:   {expectedCDF}"
                    + $"\n   diff:       {calculatedCDF - expectedCDF}\n   tol:        {tol}");
            }

            foreach (Real prob in probs)
            {
                Real expectedInvCDF = bsm.InvCdf(prob, t);
                Real calculatedInvCDF = heston.InvCdf(prob, t);

                const Real tol = 1e-3;
                Assert.False(
                    Math.Abs(expectedInvCDF - calculatedInvCDF) > tol,
                    "failed to reproduce Black-Scholes-Merton inverse cdf with the Heston model"
                    + $"\n   calculated: {calculatedInvCDF}\n   expected:   {expectedInvCDF}"
                    + $"\n   diff:       {calculatedInvCDF - expectedInvCDF}\n   tol:        {tol}");
            }
        }
    }

    [Fact]
    public void TestLocalVolatilityRND()
    {
        // Testing Fokker-Planck forward equation for local volatility process to calculate risk neutral densities...

        DayCounter dayCounter = new Actual365Fixed();
        var todaysDate = new Date(28, Month.December, 2012);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const Real r = 0.015;
        const Real q = 0.025;
        const Real s0 = 100;
        const Real v = 0.25;

        var rTS = new FlatForward(todaysDate, r, dayCounter);
        var qTS = new FlatForward(todaysDate, q, dayCounter);

        var timeGrid = new TimeGrid(1.0, 101);

        var constVolCalc = new LocalVolRNDCalculator(s0, rTS, qTS, new LocalConstantVol(todaysDate, v, dayCounter), timeGrid, 201);

        const Real rTol = 0.01, atol = 0.005;
        for (double t = 0.1; t < 0.99; t += 0.015)
        {
            Real stdDev = v * Math.Sqrt(t);
            Real xm = (-0.5 * stdDev * stdDev) + Math.Log(s0 * qTS.Discount(t) / rTS.Discount(t));

            var gaussianPDF = new NormalDistribution(xm, stdDev);
            var gaussianCDF = new CumulativeNormalDistribution(xm, stdDev);

            for (Real x = xm - (3 * stdDev); x < xm + (3 * stdDev); x += 0.05)
            {
                Real expectedPDF = gaussianPDF.Evaluate(x);
                Real calculatedPDF = constVolCalc.Pdf(x, t);
                Real absDiffPDF = Math.Abs(expectedPDF - calculatedPDF);

                Assert.False(
                    absDiffPDF > atol || absDiffPDF / expectedPDF > rTol,
                    "failed to reproduce forward probability density"
                    + $"\n   time:       {t}\n   spot        {Math.Exp(x)}\n   calculated: {calculatedPDF}"
                    + $"\n   expected:   {expectedPDF}\n   abs diff:   {absDiffPDF}\n   rel diff:   {absDiffPDF / expectedPDF}"
                    + $"\n   abs tol:    {atol}\n   rel tol:    {rTol}");

                Real expectedCDF = gaussianCDF.Evaluate(x);
                Real calculatedCDF = constVolCalc.Cdf(x, t);
                Real absDiffCDF = Math.Abs(expectedCDF - calculatedCDF);

                Assert.False(
                    absDiffCDF > atol,
                    "failed to reproduce forward cumulative probability density"
                    + $"\n   time:       {t}\n   spot        {Math.Exp(x)}\n   calculated: {calculatedCDF}"
                    + $"\n   expected:   {expectedCDF}\n   abs diff:   {absDiffCDF}\n   abs tol:    {atol}");

                Real expectedX = x;
                Real calculatedX = constVolCalc.InvCdf(expectedCDF, t);
                Real absDiffX = Math.Abs(expectedX - calculatedX);

                Assert.False(
                    absDiffX > atol || absDiffX / expectedX > rTol,
                    "failed to reproduce inverse cumulative probability density"
                    + $"\n   time:       {t}\n   spot        {Math.Exp(x)}\n   calculated: {calculatedX}"
                    + $"\n   expected:   {expectedX}\n   abs diff:   {absDiffX}\n   abs tol:    {atol}");
            }
        }

        Real tl = timeGrid.At(timeGrid.Size - 5);
        Real xl = constVolCalc.Mesher(tl).Location(0);
        Assert.True(
            constVolCalc.Pdf(xl + 0.0001, tl) > 0.0 && constVolCalc.Pdf(xl - 0.0001, tl) == 0.0,
            "probability outside interpolation range is not zero");

        const Real b1 = 0.25;
        const Real b2 = 0.03;
        const Real b3 = 0.005;
        const Real b4 = -0.02;
        const Real b5 = -0.005;

        var dumasVolSurface = new DumasParametricVolSurface(b1, b2, b3, b4, b5, s0, rTS, qTS, todaysDate);

        var bsmProcess = new BlackScholesMertonProcess(s0, qTS, rTS, dumasVolSurface);

        LocalVolTermStructure localVolSurface = new NoExceptLocalVolSurface(dumasVolSurface, rTS, qTS, s0, b1);

        double[] adaptiveGrid = AdaptiveTimeGrid(400, 50, 5.0, 3.0);

        var dumasTimeGrid = new TimeGrid(adaptiveGrid);

        var dumasVolCalc = new LocalVolRNDCalculator(s0, rTS, qTS, localVolSurface, dumasTimeGrid, 401, 0.1, 1e-8);

        Real[] strikes = [25, 50, 95, 100, 105, 150, 200, 400];
        Date[] maturities =
        [
            todaysDate + new Period(1, TimeUnit.Weeks), todaysDate + new Period(1, TimeUnit.Months),
            todaysDate + new Period(3, TimeUnit.Months), todaysDate + new Period(6, TimeUnit.Months),
            todaysDate + new Period(12, TimeUnit.Months), todaysDate + new Period(18, TimeUnit.Months),
            todaysDate + new Period(2, TimeUnit.Years), todaysDate + new Period(3, TimeUnit.Years),
        ];

        var failures = new List<string>();
        foreach (Date maturity in maturities)
        {
            Real expiry = rTS.DayCounter.YearFraction(todaysDate, maturity);

            var exercise = new EuropeanExercise(maturity);

            foreach (Real strike in strikes)
            {
                var payoff = new PlainVanillaPayoff((strike > s0) ? OptionType.Call : OptionType.Put, strike);

                var option = new VanillaOption(payoff, exercise);
                Real expected = FdBlackScholesVanillaEngine.Calculate(
                    option,
                    context,
                    bsmProcess,
                    tGrid: Math.Max(51, (int)(expiry * 101)),
                    xGrid: 201,
                    dampingSteps: 0,
                    schemeDesc: FdmSchemeDesc.Douglas(),
                    localVol: true,
                    illegalLocalVolOverwrite: b1).NPV!.Value;

                Real tx = Math.Max(dumasTimeGrid.At(1), dumasTimeGrid.ClosestTime(expiry));
                Fdm1dMesher x = dumasVolCalc.Mesher(tx);

                Real ProbWeightedPayoff(Real u) => dumasVolCalc.Pdf(u, expiry) * payoff.Evaluate(Math.Exp(u));

                Real df = rTS.Discount(expiry);
                Real calculated = new GaussLobattoIntegral(10000, 1e-10).Integrate(ProbWeightedPayoff, x.Location(0), x.Location(x.Size - 1)) * df;

                Real absDiff = Math.Abs(expected - calculated);

                if (absDiff > 0.5 * atol)
                {
                    failures.Add(
                        "failed to reproduce option prices for"
                        + $"\n   expiry:     {expiry}\n   strike:     {strike}\n   expected:   {expected}"
                        + $"\n   calculated: {calculated}\n   diff:       {absDiff}\n   abs tol:    {atol}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void TestSquareRootProcessRND()
    {
        // Testing probability density for a square root process...

        (Real V0, Real Kappa, Real Theta, Real Sigma)[] @params =
        [
            (0.17, 1.0, 0.09, 0.5),
            (1.0, 0.6, 0.1, 0.75),
            (0.005, 0.6, 0.1, 0.05),
        ];

        foreach ((Real v0, Real kappa, Real theta, Real sigma) in @params)
        {
            var rndCalculator = new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma);

            const Real t = 0.75;
            Real tInfty = 60.0 / kappa;

            const Real tol = 1e-10;
            for (Real v = 1e-5; v < 1.0; v += (v < theta) ? 0.005 : 0.1)
            {
                Real cdfCalculated = rndCalculator.Cdf(v, t);
                Real cdfExpected = new GaussLobattoIntegral(10000, 0.01 * tol).Integrate(x => rndCalculator.Pdf(x, t), 0, v);

                Assert.False(
                    Math.Abs(cdfCalculated - cdfExpected) > tol,
                    "failed to calculate cdf"
                    + $"\n   t:          {t}\n   v:          {v}\n   calculated: {cdfCalculated}\n   expected:   {cdfExpected}"
                    + $"\n   diff:       {cdfCalculated - cdfExpected}\n   tolerance:  {tol}");

                if (cdfExpected < (1 - 1e-6) && cdfExpected > 1e-6)
                {
                    Real vCalculated = rndCalculator.InvCdf(cdfCalculated, t);

                    Assert.False(
                        Math.Abs(v - vCalculated) > tol,
                        "failed to calculate round trip cdf <-> invcdf"
                        + $"\n   t:          {t}\n   v:          {v}\n   cdf:        {cdfExpected}\n   calculated: {vCalculated}"
                        + $"\n   diff:       {v - vCalculated}\n   tolerance:  {tol}");
                }

                Real statPdfCalculated = rndCalculator.Pdf(v, tInfty);
                Real statPdfExpected = rndCalculator.StationaryPdf(v);

                Assert.False(
                    Math.Abs(statPdfCalculated - statPdfExpected) > tol,
                    "failed to calculate stationary pdf"
                    + $"\n   v:          {v}\n   calculated: {statPdfCalculated}\n   expected:   {statPdfExpected}"
                    + $"\n   diff:       {statPdfCalculated - statPdfExpected}\n   tolerance:  {tol}");

                Real statCdfCalculated = rndCalculator.Cdf(v, tInfty);
                Real statCdfExpected = rndCalculator.StationaryCdf(v);

                Assert.False(
                    Math.Abs(statCdfCalculated - statCdfExpected) > tol,
                    "failed to calculate stationary cdf"
                    + $"\n   v:          {v}\n   calculated: {statCdfCalculated}\n   expected:   {statCdfExpected}"
                    + $"\n   diff:       {statCdfCalculated - statCdfExpected}\n   tolerance:  {tol}");
            }

            for (Real q = 1e-5; q < 1.0; q += 0.001)
            {
                Real statInvCdfCalculated = rndCalculator.InvCdf(q, tInfty);
                Real statInvCdfExpected = rndCalculator.StationaryInvCdf(q);

                Assert.False(
                    Math.Abs(statInvCdfCalculated - statInvCdfExpected) > tol,
                    "failed to calculate stationary inverse of cdf"
                    + $"\n   q:          {q}\n   calculated: {statInvCdfCalculated}\n   expected:   {statInvCdfExpected}"
                    + $"\n   diff:       {statInvCdfCalculated - statInvCdfExpected}\n   tolerance:  {tol}");
            }
        }
    }

    [Fact]
    public void TestBlackScholesWithSkew()
    {
        // Testing probability density for a BSM process with strike dependent volatility vs local volatility...

        var todaysDate = new Date(3, Month.October, 2016);
        DayCounter dc = new Actual365Fixed();
        Date maturityDate = todaysDate + new Period(3, TimeUnit.Months);
        double maturity = dc.YearFraction(todaysDate, maturityDate);

        // use Heston model to create volatility surface with skew
        const Real r = 0.08;
        const Real q = 0.03;
        const Real s0 = 100;
        const Real v0 = 0.06;
        const Real kappa = 1.0;
        const Real theta = 0.06;
        const Real sigma = 0.4;
        const Real rho = -0.75;

        var rTS = new FlatForward(todaysDate, r, dc);
        var qTS = new FlatForward(todaysDate, q, dc);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        var hestonSurface = new HestonBlackVolSurface(
            new HestonModel(hestonProcess),
            AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg,
            FourierIntegration.DiscreteTrapezoid(128));

        var timeGrid = new TimeGrid(maturity, 51);

        LocalVolTermStructure localVol = new NoExceptLocalVolSurface(hestonSurface, rTS, qTS, s0, Math.Sqrt(theta));

        var localVolCalc = new LocalVolRNDCalculator(s0, rTS, qTS, localVol, timeGrid, 151, 0.25);

        var hestonCalc = new HestonRNDCalculator(hestonProcess);

        var gbsmCalc = new GBSMRNDCalculator(new BlackScholesMertonProcess(s0, qTS, rTS, hestonSurface));

        Real[] strikes = [85, 75, 90, 110, 125, 150];

        foreach (Real strike in strikes)
        {
            Real logStrike = Math.Log(strike);

            Real expected = hestonCalc.Cdf(logStrike, maturity);
            Real calculatedGBSM = gbsmCalc.Cdf(strike, maturity);

            const Real gbsmTol = 1e-5;
            Assert.False(
                Math.Abs(expected - calculatedGBSM) > gbsmTol,
                "failed to match Heston and GBSM cdf"
                + $"\n   t:          {maturity}\n   k:          {strike}\n   calculated: {calculatedGBSM}\n   expected:   {expected}"
                + $"\n   diff:       {Math.Abs(calculatedGBSM - expected)}\n   tol:        {gbsmTol}");

            Real calculatedLocalVol = localVolCalc.Cdf(logStrike, maturity);
            const Real localVolTol = 1e-3;
            Assert.False(
                Math.Abs(expected - calculatedLocalVol) > localVolTol,
                "failed to match Heston and local Volatility cdf"
                + $"\n   t:          {maturity}\n   k:          {strike}\n   calculated: {calculatedLocalVol}\n   expected:   {expected}"
                + $"\n   diff:       {Math.Abs(calculatedLocalVol - expected)}\n   tol:        {localVolTol}");
        }

        foreach (Real strike in strikes)
        {
            Real logStrike = Math.Log(strike);

            Real expected = hestonCalc.Pdf(logStrike, maturity) / strike;
            Real calculatedGBSM = gbsmCalc.Pdf(strike, maturity);

            const Real gbsmTol = 1e-5;
            Assert.False(
                Math.Abs(expected - calculatedGBSM) > gbsmTol,
                "failed to match Heston and GBSM pdf"
                + $"\n   t:          {maturity}\n   k:          {strike}\n   calculated: {calculatedGBSM}\n   expected:   {expected}"
                + $"\n   diff:       {Math.Abs(calculatedGBSM - expected)}\n   tol:        {gbsmTol}");

            Real calculatedLocalVol = localVolCalc.Pdf(logStrike, maturity) / strike;
            const Real localVolTol = 1e-4;
            Assert.False(
                Math.Abs(expected - calculatedLocalVol) > localVolTol,
                "failed to match Heston and local Volatility pdf"
                + $"\n   t:          {maturity}\n   k:          {strike}\n   calculated: {calculatedLocalVol}\n   expected:   {expected}"
                + $"\n   diff:       {Math.Abs(calculatedLocalVol - expected)}\n   tol:        {localVolTol}");
        }

        Real[] quantiles = [0.05, 0.25, 0.5, 0.75, 0.95];
        foreach (Real quantile in quantiles)
        {
            Real expected = Math.Exp(hestonCalc.InvCdf(quantile, maturity));
            Real calculatedGBSM = gbsmCalc.InvCdf(quantile, maturity);

            const Real gbsmTol = 1e-3;
            Assert.False(
                Math.Abs(expected - calculatedGBSM) > gbsmTol,
                "failed to match Heston and GBSM inverse cdf"
                + $"\n   t:          {maturity}\n   quantile:   {quantile}\n   calculated: {calculatedGBSM}\n   expected:   {expected}"
                + $"\n   diff:       {Math.Abs(calculatedGBSM - expected)}\n   tol:        {gbsmTol}");

            Real calculatedLocalVol = Math.Exp(localVolCalc.InvCdf(quantile, maturity));
            const Real localVolTol = 0.2;
            Assert.False(
                Math.Abs(expected - calculatedLocalVol) > localVolTol,
                "failed to match Heston and local Volatility inverse cdf"
                + $"\n   t:          {maturity}\n   quantile:   {quantile}\n   calculated: {calculatedLocalVol}\n   expected:   {expected}"
                + $"\n   diff:       {Math.Abs(calculatedLocalVol - expected)}\n   tol:        {localVolTol}");
        }
    }

    [Fact]
    public void TestMassAtZeroCEVProcessRND()
    {
        // Testing the mass at zero for a constant elasticity of variance (CEV) process...
        const Real f0 = 100.0;
        const double t = 2.75;

        (Real Alpha, Real Beta)[] parameters =
        [
            (0.1, 1.6),
            (0.01, 2.0),
            (10.0, 0.35),
            (50.0, 0.1),
        ];

        const Real tol = 1e-4;

        foreach ((Real alpha, Real beta) in parameters)
        {
            var calculator = new CEVRNDCalculator(f0, alpha, beta);

            Real ax = 15.0 * Math.Sqrt(t) * alpha * Math.Pow(f0, beta);

            Real calculated = new GaussLobattoIntegral(1000, 1e-8).Integrate(
                x => calculator.Pdf(x, t), Math.Max(QL_EPSILON, f0 - ax), f0 + ax)
                + calculator.MassAtZero(t);

            Assert.False(
                Math.Abs(calculated - 1.0) > tol,
                "failed to reproduce the total probability mass"
                + $"\n   alpha:     {alpha}"
                + $"\n   beta:      {beta}"
                + $"\n   prob mass: {calculated}"
                + $"\n   tolerance: {tol}");
        }
    }

    [Fact]
    public void TestCEVCDF()
    {
        // Testing CDF for a constant elasticity of variance (CEV) process...
        const Real f0 = 2.1;
        const double t = 0.75;

        const Real alpha = 0.1;
        Real[] betas = [0.45, 1.25];

        const Real tol = 1e-6;

        // QuantLib's loop starts at the second elasticity, as here
        for (Size i = 1; i < betas.Length; ++i)
        {
            Real beta = betas[i];
            var calculator = new CEVRNDCalculator(f0, alpha, beta);

            for (Real x = 1.3; x < 3.1; x += 0.1)
            {
                Real cdfValue = calculator.Cdf(x, t);
                Real calculated = calculator.InvCdf(cdfValue, t);

                Assert.False(
                    Math.Abs(x - calculated) > tol,
                    "failed to reproduce the inverse cumulative probability"
                    + $"\n   alpha:     {alpha}"
                    + $"\n   beta:      {beta}"
                    + $"\n   x:         {x}"
                    + $"\n   calculated:{calculated}"
                    + $"\n   difference:{x - calculated}"
                    + $"\n   tolerance: {tol}");
            }
        }
    }

    [Fact]
    public void TestSmileSectionRNDvsBSM()
    {
        // Testing SmileSectionRNDCalculator on a flat-vol smile against BSMRNDCalculator...

        DayCounter dc = new Actual365Fixed();
        Date today = Date.TodaysDate();
        Date maturity = today + new Period(1, TimeUnit.Years);

        const Real s0 = 100.0;
        const Real r = 0.05;
        const Real q = 0.02;
        const Real v = 0.20;

        var rTS = new FlatForward(today, r, dc);
        var qTS = new FlatForward(today, q, dc);

        var bsmProcess = new BlackScholesMertonProcess(s0, qTS, rTS, new BlackConstantVol(today, new NullCalendar(), v, dc));

        var bsmRnd = new BSMRNDCalculator(bsmProcess);

        Real fwd = s0 * qTS.Discount(maturity) / rTS.Discount(maturity);
        var smile = new FlatSmileSection(maturity, v, dc, today, fwd);
        var smileRnd = new SmileSectionRNDCalculator(smile);
        Real T = smile.ExerciseTime;

        Real logFwd = Math.Log(fwd);
        const Real tol = 1e-3;

        foreach (Real offset in new[] { -0.30, -0.15, 0.0, 0.15, 0.30 })
        {
            Real x = logFwd + offset;

            Real expectedCDF = bsmRnd.Cdf(x, T);
            Real calculatedCDF = smileRnd.Cdf(x, T);
            Assert.False(
                Math.Abs(expectedCDF - calculatedCDF) > tol,
                "failed to reproduce BSM cdf with SmileSection RND"
                + $"\n   x:          {x}\n   calculated: {calculatedCDF}\n   expected:   {expectedCDF}"
                + $"\n   diff:       {calculatedCDF - expectedCDF}\n   tolerance:  {tol}");

            Real expectedPDF = bsmRnd.Pdf(x, T);
            Real calculatedPDF = smileRnd.Pdf(x, T);
            Assert.False(
                Math.Abs(expectedPDF - calculatedPDF) > tol,
                "failed to reproduce BSM pdf with SmileSection RND"
                + $"\n   x:          {x}\n   calculated: {calculatedPDF}\n   expected:   {expectedPDF}"
                + $"\n   diff:       {calculatedPDF - expectedPDF}\n   tolerance:  {tol}");
        }

        foreach (Real prob in new[] { 0.05, 0.25, 0.5, 0.75, 0.95 })
        {
            Real expectedInvCDF = bsmRnd.InvCdf(prob, T);
            Real calculatedInvCDF = smileRnd.InvCdf(prob, T);
            Assert.False(
                Math.Abs(expectedInvCDF - calculatedInvCDF) > tol,
                "failed to reproduce BSM invcdf with SmileSection RND"
                + $"\n   prob:       {prob}\n   calculated: {calculatedInvCDF}\n   expected:   {expectedInvCDF}"
                + $"\n   diff:       {calculatedInvCDF - expectedInvCDF}\n   tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestSmileSectionRNDQuantileRange()
    {
        // Testing SmileSectionRNDCalculator quantile-range saturation...

        const Real T = 1.0;
        const Real fwd = 100.0;
        const Real vol = 0.20;
        const Real nStd = 1.0;
        var smile = new FlatSmileSection(T, vol, null, fwd);
        var rnd = new SmileSectionRNDCalculator(smile, 200, nStd);
        var phi = new CumulativeNormalDistribution();

        Real logMean = Math.Log(fwd) - (0.5 * vol * vol * T);
        const Real tol = 1e-6;
        foreach (Real sign in new[] { -1.0, 1.0 })
        {
            Real probability = phi.Evaluate(sign * 2.0 * nStd);
            Real calculated = rnd.InvCdf(probability);
            Real expected = logMean + (sign * nStd * vol * Math.Sqrt(T));

            Assert.False(
                Math.Abs(calculated - expected) > tol,
                "failed to saturate at the requested normal quantile"
                + $"\n   side:       {sign}\n   calculated: {calculated}\n   expected:   {expected}"
                + $"\n   difference: {calculated - expected}\n   tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestSmileSectionRNDLowVolFatWings()
    {
        // Testing SmileSectionRNDCalculator quantile range at a low at-the-money volatility...

        // Wings reaching far in log-moneyness on a 5% ATM volatility, which a
        // search stepping in ATM standard deviations cannot cross.
        Real[] svi = [-0.7124561725, 0.2210528950, 3.2365013812, -0.0171352352, -0.0547257886];
        const Real fwd = 50.0;
        const Real nStd = 8.0;

        var smile = new SviSmileSection(1.0, fwd, svi);
        var rnd = new SmileSectionRNDCalculator(smile, 200, nStd);
        var phi = new CumulativeNormalDistribution();
        var invPhi = new InverseCumulativeNormal();

        const Real tol = 1e-3;
        Real edge = rnd.InvCdf(phi.Evaluate(-nStd));
        Real reached = invPhi.Evaluate(rnd.Cdf(edge));

        Assert.False(
            Math.Abs(reached + nStd) > tol,
            "failed to reach the requested quantile"
            + $"\n   requested: {-nStd}\n   reached:   {reached}\n   at x:      {edge}\n   tolerance: {tol}");

        // Beyond the range the quantile saturates.
        Real beyond = rnd.InvCdf(phi.Evaluate(-nStd - 1.0));
        Assert.False(
            Math.Abs(beyond - edge) > tol,
            "failed to saturate past the tabulated range"
            + $"\n   at the edge: {edge}\n   beyond it:   {beyond}\n   tolerance:   {tol}");
    }

    [Fact]
    public void TestSmileSectionRNDvsHeston()
    {
        // Testing SmileSectionRNDCalculator on a Heston-implied smile against HestonRNDCalculator...

        DayCounter dc = new Actual365Fixed();
        Date today = Date.TodaysDate();
        Date maturity = today + new Period(1, TimeUnit.Years);

        const Real s0 = 100.0;
        const Real r = 0.05;
        const Real q = 0.02;

        var rTS = new FlatForward(today, r, dc);
        var qTS = new FlatForward(today, q, dc);

        const Real v0 = 0.04, kappa = 1.0, theta = 0.04;
        const Real sigma = 0.3, rho = -0.5;

        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);
        var hestonModel = new HestonModel(hestonProcess);

        var hestonRnd = new HestonRNDCalculator(hestonProcess);

        var hestonVolSurface = new HestonBlackVolSurface(hestonModel);

        double T = hestonVolSurface.TimeFromReference(maturity);
        Real fwd = s0 * qTS.Discount(maturity) / rTS.Discount(maturity);

        var smile = new AtmSmileSection(hestonVolSurface.SmileSection(T), fwd);
        var smileRnd = new SmileSectionRNDCalculator(smile);

        Real logFwd = Math.Log(fwd);
        const Real cdfTol = 5e-3;
        const Real pdfTol = 1e-3;

        foreach (Real offset in new[] { -0.30, -0.15, 0.0, 0.15, 0.30 })
        {
            Real x = logFwd + offset;

            Real expectedCDF = hestonRnd.Cdf(x, T);
            Real calculatedCDF = smileRnd.Cdf(x, T);
            Assert.False(
                Math.Abs(expectedCDF - calculatedCDF) > cdfTol,
                "failed to reproduce Heston cdf with SmileSection RND"
                + $"\n   x:          {x}\n   calculated: {calculatedCDF}\n   expected:   {expectedCDF}"
                + $"\n   diff:       {calculatedCDF - expectedCDF}\n   tolerance:  {cdfTol}");

            Real expectedPDF = hestonRnd.Pdf(x, T);
            Real calculatedPDF = smileRnd.Pdf(x, T);
            Assert.False(
                Math.Abs(expectedPDF - calculatedPDF) > pdfTol,
                "failed to reproduce Heston pdf with SmileSection RND"
                + $"\n   x:          {x}\n   calculated: {calculatedPDF}\n   expected:   {expectedPDF}"
                + $"\n   diff:       {calculatedPDF - expectedPDF}\n   tolerance:  {pdfTol}");
        }
    }

    [Fact]
    public void TestSmileSectionRNDMissingAtmLevel()
    {
        // Testing that SmileSectionRNDCalculator raises when the smile has no atmLevel...

        Date today = Date.TodaysDate();
        Date maturity = today + new Period(1, TimeUnit.Years);
        var smile = new FlatSmileSection(maturity, 0.20, new Actual365Fixed(), today);
        var rnd = new SmileSectionRNDCalculator(smile);

        Real T = smile.ExerciseTime;
        NanoQLException e = Assert.Throws<NanoQLException>(() => rnd.InvCdf(0.5, T));
        Assert.Contains("AtmSmileSection", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSmileSectionRNDMartingale()
    {
        // Testing that SmileSectionRNDCalculator reproduces the forward across volatility levels...

        // Scaling SVI total variance by c^2 fattens the wings while holding their
        // shape. The tail then carries a share of E[S] far out of proportion to its
        // probability, which is what a coarse or truncated quantile grid loses.
        Date today = Date.TodaysDate();
        Date maturity = today + new Period(1, TimeUnit.Years);
        Real T = new Actual365Fixed().YearFraction(today, maturity);
        const Real fwd = 50.0;

        // The residual is the tabulated quantile range, not the grid: refining the
        // grid stalls at this level because invcdf takes a probability, which pins
        // the deepest reachable quantile near 8 standard deviations.
        const Real tol = 5.0e-3;

        var gh = new GaussHermiteIntegration(64);
        Vector nodes = gh.X();
        Vector weights = gh.Weights();
        var phi = new CumulativeNormalDistribution();

        foreach (Real c in new[] { 0.7, 1.0, 1.5, 2.0, 2.5 })
        {
            Real[] svi = [c * c * 0.1125, c * c * 0.10, 0.10, 0.525, 0.0];
            var smile = new SviSmileSection(T, fwd, svi);
            var rnd = new SmileSectionRNDCalculator(smile);

            Real mean = 0.0;
            for (int i = 0; i < gh.Order; ++i)
            {
                Real x = nodes[i];
                Real u = Math.Clamp(phi.Evaluate(M_SQRT2 * x), QL_EPSILON, 1.0 - QL_EPSILON);
                mean += weights[i] * Math.Exp(-x * x) * Math.Exp(rnd.InvCdf(u));
            }

            mean /= Math.Sqrt(Math.PI);

            Real relError = Math.Abs(mean - fwd) / fwd;
            Assert.False(
                relError > tol,
                "failed to reproduce the forward from the SmileSectionRNDCalculator quantile"
                + $"\n   vol scale : {c:F8}\n   atm vol   : {smile.Volatility(fwd):F8}\n   E[S]      : {mean:F8}"
                + $"\n   forward   : {fwd:F8}\n   rel error : {relError:F8}\n   tolerance : {tol:F8}");
        }
    }

    [Fact]
    public void TestSmileSectionRNDLeftTailCdf()
    {
        // Testing SmileSectionRNDCalculator cdf deep in the left tail...

        // Below the forward the cdf is the small number, so taking it as
        // 1 - P(S > K) spends it against a value close to one: at four standard
        // deviations that left ~3 correct bits, and by six it returned zero. The
        // put digital carries the same probability directly.
        Date today = Date.TodaysDate();
        Date maturity = today + new Period(1, TimeUnit.Years);
        Real T = new Actual365Fixed().YearFraction(today, maturity);
        const Real fwd = 50.0;
        Real[] svi = [0.1125, 0.10, 0.10, 0.525, 0.0];

        // A flat smile has an analytic left tail, so the reference here is
        // independent of how the calculator obtains it.
        const Real flatVol = 0.20;
        Real stdDev = flatVol * Math.Sqrt(T);
        var flat = new FlatSmileSection(T, flatVol, null, fwd);
        var flatRnd = new SmileSectionRNDCalculator(flat);
        var phi = new CumulativeNormalDistribution();

        // Difference-quotient error grows as the probability shrinks, to a
        // measured 1.4e-5 at the deepest point below.
        const Real flatTol = 1e-4;
        foreach (Real nStdDev in new[] { 2.0, 3.0, 4.0, 5.0, 6.0 })
        {
            Real x = Math.Log(fwd) - (nStdDev * stdDev);
            Real d2 = ((Math.Log(fwd) - x) / stdDev) - (0.5 * stdDev);

            Real calculated = flatRnd.Cdf(x, T);
            Real expected = phi.Evaluate(-d2);

            Assert.True(expected > 0.0);
            Real relError = Math.Abs(calculated - expected) / expected;
            Assert.False(
                relError > flatTol,
                "failed to reproduce the analytic left-tail cdf"
                + $"\n   std deviations: {nStdDev}\n   calculated:     {calculated}\n   expected:       {expected}"
                + $"\n   rel error:      {relError}\n   tolerance:      {flatTol}");
        }

        var smile = new SviSmileSection(T, fwd, svi);
        var rnd = new SmileSectionRNDCalculator(smile);

        const Real tol = 1e-6;
        foreach (Real offset in new[] { -2.0, -3.0, -4.0, -5.0, -6.0 })
        {
            Real x = Math.Log(fwd) + offset;
            Real k = Math.Exp(x);

            Real calculated = rnd.Cdf(x, T);
            Real expected = smile.DigitalOptionPrice(k, OptionType.Put, 1.0, Math.Abs(k) * 2.0e-6);

            Assert.True(expected > 0.0);
            Real relError = Math.Abs(calculated - expected) / expected;
            Assert.False(
                relError > tol,
                "failed to reproduce the left-tail cdf"
                + $"\n   log-moneyness: {offset}\n   strike:        {k}\n   calculated:    {calculated}"
                + $"\n   expected:      {expected}\n   rel error:     {relError}\n   tolerance:     {tol}");
        }
    }

    private static double[] AdaptiveTimeGrid(int maxStepsPerYear, int minStepsPerYear, Real decay, Real endTime)
    {
        Real maxDt = 1.0 / maxStepsPerYear;
        Real minDt = 1.0 / minStepsPerYear;

        Real t = 0.0;
        var times = new List<double> { t };
        while (t < endTime)
        {
            Real dt = (maxDt * Math.Exp(-decay * t)) + (minDt * (1.0 - Math.Exp(-decay * t)));
            t += dt;
            times.Add(Math.Min(endTime, t));
        }

        return [.. times];
    }

    // Svetlana Borovkova, Ferry J. Permana
    // Implied volatility in oil markets
    // http://www.researchgate.net/publication/46493859_Implied_volatility_in_oil_markets
    private sealed class DumasParametricVolSurface : BlackVolatilityTermStructure
    {
        private readonly Real b1, b2, b3, b4, b5;
        private readonly Real spot;
        private readonly YieldTermStructure rTS;
        private readonly YieldTermStructure qTS;

        // QuantLib's structure floats with the evaluation date, settlement days 0 on a null calendar
        public DumasParametricVolSurface(
            Real b1, Real b2, Real b3, Real b4, Real b5, Real spot, YieldTermStructure rTS, YieldTermStructure qTS, Date evaluationDate)
            : base(evaluationDate, new NullCalendar(), BusinessDayConvention.Following, rTS.DayCounter, 0)
        {
            this.b1 = b1;
            this.b2 = b2;
            this.b3 = b3;
            this.b4 = b4;
            this.b5 = b5;
            this.spot = spot;
            this.rTS = rTS;
            this.qTS = qTS;
        }

        public override Date MaxDate => Date.MaxDate;

        public override double MinStrike() => 0.0;

        public override double MaxStrike() => double.MaxValue;

        protected override double BlackVolImpl(double t, double strike)
        {
            if (!(t >= 0.0))
            {
                throw new NanoQLException("t must be >= 0");
            }

            if (t < QL_EPSILON)
            {
                return b1;
            }

            Real fwd = spot * qTS.Discount(t) / rTS.Discount(t);
            Real mn = Math.Log(fwd / strike) / Math.Sqrt(t);

            return b1 + (b2 * mn) + (b3 * mn * mn) + (b4 * t) + (b5 * mn * t);
        }
    }
}
