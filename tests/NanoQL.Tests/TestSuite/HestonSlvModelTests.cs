// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hestonslvmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSquareRootZeroFlowBC, testTransformedZeroFlowBC, testSquareRootEvolveWithStationaryDensity, testSquareRootLogEvolveWithStationaryDensity, testSquareRootFokkerPlanckFwdEquation, testHestonFokkerPlanckFwdEquation, testHestonFokkerPlanckFwdEquationLogLVLeverage, testLocalVolsvSLVPropDensity, testMonteCarloVsFdmPricing, testMonteCarloCalibration, testMoustacheGraph, testDiffusionAndDriftSlvProcess, which wait for S-core's Heston stochastic-local-volatility models, processes and forward operators

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
}
