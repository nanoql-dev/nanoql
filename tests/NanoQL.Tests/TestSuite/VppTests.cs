// SPDX-FileCopyrightText: Copyright (C) 2010, 2011, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/vpp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class VppTests
{
    // QuantLib's QL_EPSILON
    private const Real QlEpsilon = 2.220446049250313e-16;

    // for a "real" gas and power forward curve
    // please see. e.g. http://www.kyos.com/?content=64
    private static readonly Real[] FuelPrices =
    [
        20.74, 21.65, 20.78, 21.58, 21.43, 20.82, 22.02, 21.52,
        21.02, 21.46, 21.75, 20.69, 22.16, 20.38, 20.82, 20.68,
        20.57, 21.92, 22.04, 20.45, 20.75, 21.92, 20.53, 20.67,
        20.88, 21.02, 20.82, 21.67, 21.82, 22.12, 20.45, 20.74,
        22.39, 20.95, 21.71, 20.70, 20.94, 21.59, 22.33, 21.13,
        21.50, 21.42, 20.56, 21.23, 21.37, 21.90, 20.62, 21.17,
        21.86, 22.04, 22.05, 21.00, 20.70, 21.12, 21.26, 22.40,
        21.31, 22.24, 21.96, 21.02, 21.71, 20.48, 21.36, 21.75,
        21.90, 20.44, 21.26, 22.29, 20.34, 21.79, 21.66, 21.50,
        20.76, 20.27, 20.84, 20.24, 21.97, 20.52, 20.98, 21.40,
        20.39, 20.71, 20.78, 20.30, 21.56, 21.72, 20.27, 21.57,
        21.82, 20.57, 21.33, 20.51, 22.32, 21.99, 20.57, 22.11,
        21.56, 22.24, 20.62, 21.70, 21.11, 21.19, 21.79, 20.46,
        22.21, 20.82, 20.52, 22.29, 20.71, 21.45, 22.40, 20.63,
        20.95, 21.97, 22.20, 20.67, 21.01, 22.25, 20.76, 21.33,
        20.49, 20.33, 21.94, 20.64, 20.99, 21.09, 20.97, 22.17,
        20.72, 22.06, 20.86, 21.40, 21.75, 20.78, 21.79, 20.47,
        21.19, 21.60, 20.75, 21.36, 21.61, 20.37, 21.67, 20.28,
        22.33, 21.37, 21.33, 20.87, 21.25, 22.01, 22.08, 20.81,
        20.70, 21.84, 21.82, 21.68, 21.24, 22.36, 20.83, 20.64,
        21.03, 20.57, 22.34, 20.96, 21.54, 21.26, 21.43, 22.39,
    ];

    private static readonly Real[] PowerPrices =
    [
        40.40, 36.71, 31.87, 25.81, 31.61, 35.00, 46.22, 60.68,
        42.45, 38.01, 33.84, 29.79, 31.84, 38.53, 49.23, 59.92,
        43.85, 37.47, 34.89, 29.99, 30.85, 29.19, 29.25, 38.67,
        36.90, 25.93, 22.12, 20.19, 17.19, 19.29, 13.51, 18.14,
        33.76, 30.48, 25.63, 18.01, 23.86, 32.41, 48.56, 64.69,
        38.42, 39.31, 32.73, 29.97, 31.41, 35.02, 46.85, 58.12,
        39.14, 35.42, 32.61, 28.76, 29.41, 35.83, 46.73, 61.41,
        61.01, 59.43, 60.43, 66.29, 62.79, 62.66, 57.66, 51.63,
        62.18, 60.53, 61.94, 64.86, 59.57, 58.15, 53.74, 48.36,
        45.64, 51.21, 51.54, 50.79, 54.50, 49.92, 41.58, 39.81,
        28.86, 37.42, 39.78, 42.36, 45.67, 36.84, 33.91, 28.75,
        62.97, 63.84, 62.91, 68.77, 64.33, 61.95, 59.12, 54.89,
        63.62, 60.90, 66.57, 69.51, 64.71, 59.89, 57.28, 57.10,
        65.09, 63.82, 67.52, 70.51, 65.59, 59.36, 58.22, 54.64,
        52.17, 53.02, 57.12, 53.50, 53.16, 49.21, 52.21, 40.96,
        49.01, 47.94, 49.89, 53.83, 52.96, 50.33, 51.72, 46.99,
        39.06, 47.99, 47.91, 52.35, 48.51, 47.39, 50.45, 43.66,
        25.62, 35.76, 42.76, 46.51, 45.62, 46.79, 48.76, 41.00,
        52.65, 55.57, 57.67, 56.79, 55.15, 54.74, 50.31, 47.49,
        53.72, 55.62, 55.89, 58.11, 54.46, 52.92, 49.61, 44.68,
        51.59, 57.44, 56.50, 55.12, 57.22, 54.61, 49.92, 45.20,
    ];

    // Adapted: the evaluation date is the context's, which nothing here reads; the process's jump generator is the
    // working state QuantLib's hidden generator is.
    [Fact]
    public void TestGemanRoncoroniProcess()
    {
        // Testing Geman-Roncoroni process...

        /* Example induced by H. Geman, A. Roncoroni,
           "Understanding the Fine Structure of Electricity Prices",
           http://papers.ssrn.com/sol3/papers.cfm?abstract_id=638322
           Results are verified against the authors MatLab-Code.
           http://semeq.unipmn.it/files/Ch19_spark_spread.zip
        */

        var today = new Date(18, Month.December, 2011);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        YieldTermStructure rTS = new FlatForward(today, 0.03, dc);

        const Real x0 = 3.3;
        const Real beta = 0.05;
        const Real alpha = 3.1;
        const Real gamma = -0.09;
        const Real delta = 0.07;
        const Real eps = -0.40;
        const Real zeta = -0.40;
        const Real d = 1.6;
        const Real k = 1.0;
        const Real tau = 0.5;
        const Real sig2 = 10.0;
        const Real a = -7.0;
        const Real b = -0.3;
        const Real theta1 = 35.0;
        const Real theta2 = 9.0;
        const Real theta3 = 0.10;
        const Real psi = 1.9;

        var grProcess = new GemanRoncoroniProcess(
            x0, alpha, beta, gamma, delta, eps, zeta, d, k, tau, sig2, a, b, theta1, theta2, theta3, psi);

        const Real speed = 5.0;
        Real vol = Math.Sqrt(1.4);
        const Real betaG = 0.08;
        const Real alphaG = 1.0;
        const Real x0G = 1.1;

        Func<Real, Real> f = x => alphaG + (betaG * x);

        StochasticProcess1D eouProcess = new ExtendedOrnsteinUhlenbeckProcess(
            speed, vol, x0G, f, ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial);

        ImmutableArray<StochasticProcess1D> processes = [grProcess, eouProcess];
        var correlation = new Matrix(2, 2, 1.0);
        correlation[0, 1] = correlation[1, 0] = 0.25;

        var pArray = new StochasticProcessArray(processes, correlation);

        const Real T = 10.0;
        const int stepsPerYear = 250;
        const int steps = (int)(T * stepsPerYear);

        var grid = new TimeGrid(T, steps);

        RsgType rsg = PseudoRandom.MakeSequenceGenerator(pArray.Size * (grid.Size - 1), 421);

        var npv = new GeneralStatistics();
        var onTime = new GeneralStatistics();
        var generator = new MultiPathGenerator<RsgType>(pArray, grid, rsg, false);

        const Real heatRate = 8.0;
        const int nrTrails = 250;

        for (int n = 0; n < nrTrails; ++n)
        {
            Real plantValue = 0.0;
            Sample<MultiPath> path = generator.Next();

            for (int i = 1; i <= steps; ++i)
            {
                Real t = (Real)i / stepsPerYear;
                Real df = rTS.Discount(t);

                Real fuelPrice = Math.Exp(path.Value[1][i]);
                Real electricityPrice = Math.Exp(path.Value[0][i]);

                Real sparkSpread = electricityPrice - (heatRate * fuelPrice);
                plantValue += Math.Max(0.0, sparkSpread) * df;
                onTime.Add(sparkSpread > 0.0 ? 1.0 : 0.0);
            }

            npv.Add(plantValue);
        }

        const Real expectedNPV = 12500;
        Real calculatedNPV = npv.Mean();
        Real errorEstimateNPV = npv.ErrorEstimate();

        if (Math.Abs(calculatedNPV - expectedNPV) > 3.0 * errorEstimateNPV)
        {
            Assert.Fail(
                "Failed to reproduce cached price with MC engine"
                + $"\n    calculated: {calculatedNPV}"
                + $"\n    expected:   {expectedNPV} +/- {errorEstimateNPV}");
        }

        const Real expectedOnTime = 0.43;
        Real calculatedOnTime = onTime.Mean();
        Real errorEstimateOnTime = Math.Sqrt(calculatedOnTime * (1 - calculatedOnTime)) / nrTrails;

        if (Math.Abs(calculatedOnTime - expectedOnTime) > 3.0 * errorEstimateOnTime)
        {
            Assert.Fail(
                "Failed to reproduce cached price with MC engine"
                + $"\n    calculated: {calculatedNPV}"
                + $"\n    expected:   {expectedNPV} +/- {errorEstimateNPV}");
        }
    }

    [Fact]
    public void TestSimpleExtOUStorageEngine()
    {
        // Testing simple-storage option based on ext. OU model...
        var settlementDate = new Date(18, Month.December, 2011);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = settlementDate + new Period(12, TimeUnit.Months);

        List<Date> exerciseDates = [settlementDate + new Period(1, TimeUnit.Days)];
        while (exerciseDates[^1] < maturityDate)
        {
            exerciseDates.Add(exerciseDates[^1] + new Period(1, TimeUnit.Days));
        }

        var bermudanExercise = new BermudanExercise(exerciseDates);

        const Real x0 = 3.0;
        const Real speed = 1.0;
        const Real volatility = 0.5;
        const Rate irRate = 0.1;

        var ouProcess = new ExtendedOrnsteinUhlenbeckProcess(speed, volatility, x0, ConstantB(x0));
        YieldTermStructure rTS = new FlatForward(settlementDate, irRate, dayCounter);

        var storageOption = new VanillaStorageOption(bermudanExercise, 50, 0, 1);

        const Real expected = 69.5755;
        Real calculated = FdSimpleExtOUStorageEngine.Calculate(storageOption, context, ouProcess, rTS, 1, 25).NPV!.Value;

        Assert.False(
            Math.Abs(expected - calculated) > 5e-2,
            "Failed to reproduce cached values"
            + $"\n calculated: {calculated}"
            + $"\n   expected: {expected}");
    }

    [Fact]
    public void TestKlugeExtOUSpreadOption()
    {
        // Testing simple Kluge ext-Ornstein-Uhlenbeck spread option...
        var settlementDate = new Date(18, Month.December, 2011);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = settlementDate + new Period(1, TimeUnit.Years);
        Real maturity = dayCounter.YearFraction(settlementDate, maturityDate);

        const Real speed = 1.0;
        Real vol = Math.Sqrt(1.4);
        const Real betaG = 0.0;
        const Real alphaG = 3.0;
        const Real x0G = 3.0;

        const Rate irRate = 0.0;
        const Real heatRate = 2.0;
        const Real rho = 0.5;

        ExtOUWithJumpsProcess klugeProcess = CreateKlugeProcess();
        Func<Real, Real> f = x => alphaG + (betaG * x);

        var extOUProcess = new ExtendedOrnsteinUhlenbeckProcess(
            speed, vol, x0G, f, ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial);

        YieldTermStructure rTS = new FlatForward(settlementDate, irRate, dayCounter);

        var klugeOUProcess = new KlugeExtOUProcess(rho, klugeProcess, extOUProcess);

        Payoff payoff = new PlainVanillaPayoff(OptionType.Call, 0.0);
        Real[] spreadFactors = [1.0, -heatRate];
        BasketPayoff basketPayoff = new AverageBasketPayoff(payoff, spreadFactors);

        var exercise = new EuropeanExercise(maturityDate);

        var option = new BasketOption(basketPayoff, exercise);

        var grid = new TimeGrid(maturity, 50);
        RsgType rsg = PseudoRandom.MakeSequenceGenerator(klugeOUProcess.Factors * (grid.Size - 1), 1234);

        var generator = new MultiPathGenerator<RsgType>(klugeOUProcess, grid, rsg, false);

        var npv = new GeneralStatistics();
        const Size nTrails = 20000;
        for (Size i = 0; i < nTrails; ++i)
        {
            MultiPath path = generator.Next().Value;

            Real[] p = [Math.Exp(path[0].Back() + path[1].Back()), Math.Exp(path[2].Back())];
            npv.Add(basketPayoff.Evaluate(p));
        }

        Real calculated = FdKlugeExtOUSpreadEngine.Calculate(option, context, klugeOUProcess, rTS, 5, 200, 50, 20).NPV!.Value;
        Real expectedMC = npv.Mean();
        Real mcError = npv.ErrorEstimate();
        Assert.False(
            Math.Abs(expectedMC - calculated) > 3 * mcError,
            "Failed to reproduce referenc values"
            + $"\n    calculated:   {calculated}"
            + $"\n    expected(MC): {expectedMC}"
            + $"\n    mc error    : {mcError}");
    }

    [Fact]
    public void TestVPPIntrinsicValue()
    {
        // Testing VPP step condition...
        var today = new Date(18, Month.December, 2011);
        var context = new EvaluationContext { EvaluationDate = today };

        // vpp parameters
        const Real pMin = 8;
        const Real pMax = 40;
        const Size tMinUp = 2;
        const Size tMinDown = 2;
        const Real startUpFuel = 20;
        const Real startUpFixCost = 100;
        const Real fuelCostAddon = 3.0;

        var exercise = new SwingExercise(today, today + 6, 3600);

        // Expected values are calculated using mixed integer programming
        // based on the gnu linear programming toolkit. For details please see:
        // http://spanderen.de/
        //        2011/06/23/vpp-pricing-ii-mixed-integer-linear-programming/
        Real[] efficiency = [0.35, 0.4, 0.45, 0.5, 0.55, 0.6, 0.9];
        Real[] expected = [0.0, 2056.04, 11145.577778, 26452.04, 44512.461818, 62000.626667, 137591.911111];

        for (Size i = 0; i < efficiency.Length; ++i)
        {
            Real heatRate = 1.0 / efficiency[i];

            var option = new VanillaVPPOption(heatRate, pMin, pMax, tMinUp, tMinDown, startUpFuel, startUpFixCost, exercise);

            Real calculated = DynProgVPPIntrinsicValueEngine.Calculate(option, context, FuelPrices, PowerPrices, fuelCostAddon).NPV!.Value;

            Assert.False(
                Math.Abs(expected[i] - calculated) > 1e-4,
                "Failed to reproduce reference values"
                + $"\n    calculated: {calculated}"
                + $"\n    expected:   {expected[i]}");
        }
    }

    [Fact]
    public void TestVPPPricing()
    {
        // Testing VPP pricing using perfect foresight or FDM...
        var today = new Date(18, Month.December, 2011);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var context = new EvaluationContext { EvaluationDate = today };

        // vpp parameter
        const Real heatRate = 2.5;
        const Real pMin = 8;
        const Real pMax = 40;
        const Size tMinUp = 6;
        const Size tMinDown = 2;
        const Real startUpFuel = 20;
        const Real startUpFixCost = 100;

        var exercise = new SwingExercise(today, today + 6, 3600);

        var vppOption = new VanillaVPPOption(heatRate, pMin, pMax, tMinUp, tMinDown, startUpFuel, startUpFixCost, exercise);

        KlugeExtOUProcess klugeOUProcess = CreateKlugeExtOUProcess();
        ExtOUWithJumpsProcess lnPowerProcess = klugeOUProcess.KlugeProcess;
        ExtendedOrnsteinUhlenbeckProcess ouProcess = lnPowerProcess.ExtendedOrnsteinUhlenbeckProcess;
        ExtendedOrnsteinUhlenbeckProcess lnGasProcess = klugeOUProcess.ExtOUProcess;

        Real beta = lnPowerProcess.Beta;
        Real eta = lnPowerProcess.Eta;
        Real lambda = lnPowerProcess.JumpIntensity;
        Real alpha = ouProcess.Speed;
        Real volatility_x = ouProcess.Volatility;
        Real kappa = lnGasProcess.Speed;
        Real volatility_u = lnGasProcess.Volatility;

        const Rate irRate = 0.00;
        const Real fuelCostAddon = 3.0;

        YieldTermStructure rTS = new FlatForward(today, irRate, dc);

        Size nHours = PowerPrices.Length;

        var fuelShape = new (Real Time, Real Value)[nHours];
        var powerShape = new (Real Time, Real Value)[nHours];

        for (Size i = 0; i < nHours; ++i)
        {
            Real t = (i + 1) / (365 * 24.0);

            Real fuelPrice = FuelPrices[i];
            Real gs = Math.Log(fuelPrice) - (volatility_u * volatility_u / (4 * kappa) * (1 - Math.Exp(-2 * kappa * t)));
            fuelShape[i] = (t, gs);

            Real powerPrice = PowerPrices[i];
            Real ps = Math.Log(powerPrice) - (volatility_x * volatility_x / (4 * alpha) * (1 - Math.Exp(-2 * alpha * t)))
                - (lambda / beta * Math.Log((eta - Math.Exp(-beta * t)) / (eta - 1.0)));
            powerShape[i] = (t, ps);
        }

        // Test: intrinsic value
        Real intrinsic = DynProgVPPIntrinsicValueEngine.Calculate(vppOption, context, FuelPrices, PowerPrices, fuelCostAddon).NPV!.Value;
        const Real expectedIntrinsic = 2056.04;
        Assert.False(
            Math.Abs(intrinsic - expectedIntrinsic) > 0.1,
            "Failed to reproduce intrinsic value"
            + $"\n    calculated: {intrinsic}"
            + $"\n    expected  : {expectedIntrinsic}");

        // Test: finite difference price
        Real fdmPrice = FdSimpleKlugeExtOUVPPEngine.Calculate(
            vppOption, context, klugeOUProcess, rTS, fuelShape, powerShape, fuelCostAddon, 1, 25, 11, 10).NPV!.Value;
        const Real expectedFdmPrice = 5217.68;
        Assert.False(
            Math.Abs(fdmPrice - expectedFdmPrice) > 0.1,
            "Failed to reproduce finite difference price"
            + $"\n    calculated: {fdmPrice}"
            + $"\n    expected  : {expectedFdmPrice}");

        // Test: Monte-Carlo perfect foresight price
        var stepConditionFactory = new FdmVPPStepConditionFactory(vppOption);

        var oneDimMesher = new FdmMesherComposite(stepConditionFactory.StateMesher());
        Size nStates = oneDimMesher.Layout.Dim[0];

        var vppMesh = new FdmVPPStepConditionMesher(0, oneDimMesher);

        var grid = new TimeGrid(dc.YearFraction(today, exercise.LastDate() + 1), exercise.Dates.Length);

        RsgType rsg = PseudoRandom.MakeSequenceGenerator(klugeOUProcess.Factors * (grid.Size - 1), 1234);

        var generator = new MultiPathGenerator<RsgType>(klugeOUProcess, grid, rsg, false);

        var npv = new GeneralStatistics();
        const Size nTrails = 2500;
        for (Size i = 0; i < nTrails; ++i)
        {
            MultiPath path = generator.Next().Value;
            FdmVPPStepCondition stepCondition = stepConditionFactory.Build(
                vppMesh,
                fuelCostAddon,
                new PathFuelPrice(path, fuelShape),
                new PathSparkSpreadPrice(heatRate, path, fuelShape, powerShape));

            var state = new Real[nStates];
            for (Size j = exercise.Dates.Length; j > 0; --j)
            {
                stepCondition.ApplyTo(state, grid.At(j));
                Real factor = rTS.Discount(grid.At(j)) / rTS.Discount(grid.At(j - 1));
                for (Size k = 0; k < nStates; ++k)
                {
                    state[k] *= factor;
                }
            }

            npv.Add(state[^1]);
        }

        Real npvMC = npv.Mean();
        Real errorMC = npv.ErrorEstimate();
        const Real expectedMC = 5250.0;
        Assert.False(
            Math.Abs(npvMC - expectedMC) > 3 * errorMC,
            "Failed to reproduce Monte-Carlo price"
            + $"\n    calculated: {npvMC}"
            + $"\n    error     ; {errorMC}"
            + $"\n    expected  : {expectedMC}");

        npv.Reset();

        // Test: Longstaff Schwartz least squares Monte-Carlo
        // implementation is not strictly correct but saves some coding
        const Size nCalibrationTrails = 1000;
        var calibrationPaths = new List<MultiPath>(nCalibrationTrails);
        var stepConditions = new List<FdmVPPStepCondition>(nCalibrationTrails);
        var sparkSpreads = new List<FdmInnerValueCalculator>(nCalibrationTrails);

        for (Size i = 0; i < nCalibrationTrails; ++i)
        {
            // QuantLib's vector copies each sample; the generator writes its next path over the one it returned
            calibrationPaths.Add(Copy(generator.Next().Value));
            sparkSpreads.Add(new PathSparkSpreadPrice(heatRate, calibrationPaths[^1], fuelShape, powerShape));
            stepConditions.Add(stepConditionFactory.Build(
                vppMesh, fuelCostAddon, new PathFuelPrice(calibrationPaths[^1], fuelShape), sparkSpreads[^1]));
        }

        FdmLinearOpIterator iter = oneDimMesher.Layout.Begin();

        // prices of all calibration paths for all states
        var prices = new Real[nCalibrationTrails][];
        for (Size j = 0; j < nCalibrationTrails; ++j)
        {
            prices[j] = new Real[nStates];
        }

        // regression coefficients for all states and all exercise dates
        var coeff = new Vector[nStates][];
        for (Size k = 0; k < nStates; ++k)
        {
            coeff[k] = new Vector[exercise.Dates.Length];
        }

        // regression functions
        const Size dim = 1;
        IReadOnlyList<Func<Vector, double>> v = LsmBasisSystem.MultiPathBasisSystem(dim, 5, LsmBasisSystemPolynomialType.Monomial);

        for (Size i = exercise.Dates.Length; i > 0; --i)
        {
            Real t = grid.At(i);

            var x = new Vector[nCalibrationTrails];
            for (Size j = 0; j < nCalibrationTrails; ++j)
            {
                x[j] = new Vector(dim);
                x[j][0] = sparkSpreads[j].InnerValue(iter, t);
            }

            for (Size k = 0; k < nStates; ++k)
            {
                var y = new Real[nCalibrationTrails];
                for (Size j = 0; j < nCalibrationTrails; ++j)
                {
                    y[j] = prices[j][k];
                }

                coeff[k][i - 1] = GeneralLinearLeastSquares.Create(x, y, v).Coefficients();

                for (Size j = 0; j < nCalibrationTrails; ++j)
                {
                    prices[j][k] = 0.0;
                    for (Size l = 0; l < v.Count; ++l)
                    {
                        prices[j][k] += coeff[k][i - 1][l] * v[l](x[j]);
                    }
                }
            }

            for (Size j = 0; j < nCalibrationTrails; ++j)
            {
                stepConditions[j].ApplyTo(prices[j], grid.At(i));
            }
        }

        Real tmpValue = 0.0;
        for (Size i = 0; i < nTrails; ++i)
        {
            var x = new Vector(dim);
            var state = new Real[nStates];
            var contState = new Real[nStates];

            MultiPath path = (i % 2) != 0 ? generator.Antithetic().Value : generator.Next().Value;

            var fuelPrices = new PathFuelPrice(path, fuelShape);
            var pathSparkSpreads = new PathSparkSpreadPrice(heatRate, path, fuelShape, powerShape);

            for (Size j = exercise.Dates.Length; j > 0; --j)
            {
                Real t = grid.At(j);
                Real fuelPrice = fuelPrices.InnerValue(iter, t);
                Real sparkSpread = pathSparkSpreads.InnerValue(iter, t);
                Real startUpCost = startUpFixCost + ((fuelPrice + fuelCostAddon) * startUpFuel);

                x[0] = sparkSpread;
                for (Size k = 0; k < nStates; ++k)
                {
                    contState[k] = 0.0;
                    for (Size l = 0; l < v.Count; ++l)
                    {
                        contState[k] += coeff[k][j - 1][l] * v[l](x);
                    }
                }

                Real pMinFlow = pMin * (sparkSpread - (heatRate * fuelCostAddon));
                Real pMaxFlow = pMax * (sparkSpread - (heatRate * fuelCostAddon));

                // rollback continuation states and the path states
                for (Size s = 0; s < 2 * tMinUp; ++s)
                {
                    if (s < tMinUp)
                    {
                        state[s] += pMinFlow;
                        contState[s] += pMinFlow;
                    }
                    else
                    {
                        state[s] += pMaxFlow;
                        contState[s] += pMaxFlow;
                    }
                }

                // dynamic programming using the continuation values
                var retVal = new Real[nStates];
                for (Size s = 0; s < tMinUp - 1; ++s)
                {
                    retVal[s] = retVal[tMinUp + s] = (contState[s + 1] > contState[tMinUp + s + 1]) ? state[s + 1] : state[tMinUp + s + 1];
                }

                if (contState[2 * tMinUp] >= Math.Max(contState[tMinUp - 1], contState[(2 * tMinUp) - 1]))
                {
                    retVal[tMinUp - 1] = retVal[(2 * tMinUp) - 1] = state[2 * tMinUp];
                }
                else if (contState[tMinUp - 1] >= contState[(2 * tMinUp) - 1])
                {
                    retVal[tMinUp - 1] = retVal[(2 * tMinUp) - 1] = state[tMinUp - 1];
                }
                else
                {
                    retVal[tMinUp - 1] = retVal[(2 * tMinUp) - 1] = state[(2 * tMinUp) - 1];
                }

                for (Size s = 0; s < tMinDown - 1; ++s)
                {
                    retVal[(2 * tMinUp) + s] = state[(2 * tMinUp) + s + 1];
                }

                if (contState[^1] >= Math.Max(contState[0], contState[tMinUp]) - startUpCost)
                {
                    retVal[^1] = state[^1];
                }
                else if (contState[0] > contState[tMinUp])
                {
                    retVal[^1] = state[0] - startUpCost;
                }
                else
                {
                    retVal[^1] = state[tMinUp] - startUpCost;
                }

                state = retVal;
            }

            tmpValue += 0.5 * state[^1];
            if ((i % 2) != 0)
            {
                npv.Add(tmpValue, 1.0);
                tmpValue = 0.0;
            }
        }

        npvMC = npv.Mean();
        errorMC = npv.ErrorEstimate();
        Assert.False(
            Math.Abs(npvMC - fdmPrice) > 3 * errorMC,
            "Failed to reproduce Least Square Monte-Carlo price"
            + $"\n    calculated   : {npvMC}"
            + $"\n    error        : {errorMC}"
            + $"\n    expected FDM : {fdmPrice}");
    }

    [Fact]
    public void TestKlugeExtOUMatrixDecomposition()
    {
        // Testing KlugeExtOU matrix decomposition...
        var today = new Date(18, Month.December, 2011);

        KlugeExtOUProcess klugeOUProcess = CreateKlugeExtOUProcess();

        const Size xGrid = 50;
        const Size yGrid = 20;
        const Size uGrid = 20;
        const Real maturity = 1;

        ExtOUWithJumpsProcess klugeProcess = klugeOUProcess.KlugeProcess;
        ExtendedOrnsteinUhlenbeckProcess ouProcess = klugeProcess.ExtendedOrnsteinUhlenbeckProcess;

        var mesher = new FdmMesherComposite(
            new FdmSimpleProcess1dMesher(xGrid, ouProcess, maturity),
            new ExponentialJump1dMesher(yGrid, klugeProcess.Beta, klugeProcess.JumpIntensity, klugeProcess.Eta),
            new FdmSimpleProcess1dMesher(uGrid, klugeOUProcess.ExtOUProcess, maturity));

        var op = new FdmKlugeExtOUOp(
            mesher, klugeOUProcess, new FlatForward(today, 0.0, new ActualActual(ActualActualConvention.ISDA)), [], 16);
        op.SetTime(0.1, 0.2);

        var x = new Vector(mesher.Layout.Size);
        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(new MersenneTwisterUniformRng(12345));
        for (Size i = 0; i < x.Size; ++i)
        {
            x[i] = rng.Next().Value;
        }

        const Real tol = 1e-9;
        var applyExpected = new Vector(x.Size);
        op.Apply(x.AsSpan(), applyExpected.AsSpan());
        var applyExpectedMixed = new Vector(x.Size);
        op.ApplyMixed(x.AsSpan(), applyExpectedMixed.AsSpan());

        System.Collections.Immutable.ImmutableArray<SparseMatrix> matrixDecomp = op.ToMatrixDecomp();
        Vector applyCalculated = SparseMatrixFunctions.Prod(op.ToMatrix(), x);
        Vector applyCalculatedMixed = SparseMatrixFunctions.Prod(matrixDecomp[^1], x);

        for (Size i = 0; i < x.Size; ++i)
        {
            Real diffApply = Math.Abs(applyExpected[i] - applyCalculated[i]);
            Assert.False(
                diffApply > tol && diffApply > Math.Abs(applyExpected[i]) * tol,
                "Failed to reproduce apply operation"
                + $"\n    expected  : {applyExpected[i]}"
                + $"\n    calculated: {applyCalculated[i]}"
                + $"\n    diff      : {diffApply}");

            Real diffMixed = Math.Abs(applyExpectedMixed[i] - applyCalculatedMixed[i]);
            Assert.False(
                diffMixed > tol && diffMixed > Math.Abs(applyExpected[i]) * tol,
                "Failed to reproduce apply operation"
                + $"\n    expected  : {applyExpectedMixed[i]}"
                + $"\n    calculated: {applyCalculatedMixed[i]}"
                + $"\n    diff      : {diffMixed}");
        }

        for (Size i = 0; i < 3; ++i)
        {
            var applyExpectedDir = new Vector(x.Size);
            op.ApplyDirection(i, x.AsSpan(), applyExpectedDir.AsSpan());
            Vector applyCalculatedDir = SparseMatrixFunctions.Prod(matrixDecomp[i], x);
            for (Size j = 0; j < x.Size; ++j)
            {
                Real diff = Math.Abs(applyExpectedDir[j] - applyCalculatedDir[j]);
                Assert.False(
                    diff > tol && diff > Math.Abs(applyExpectedDir[j] * tol),
                    "Failed to reproduce apply operation"
                    + $"\n    expected  : {applyExpectedDir[i]}"
                    + $"\n    calculated: {applyCalculatedDir[i]}"
                    + $"\n    diff : {diff}");
            }
        }
    }

    private static Func<Real, Real> ConstantB(Real b) => x => b;

    private static MultiPath Copy(MultiPath path) =>
        new([.. Enumerable.Range(0, path.AssetNumber).Select(j =>
        {
            MonteCarloPath p = path[j];
            var values = new Vector(p.Length);
            for (int i = 0; i < p.Length; ++i)
            {
                values[i] = p[i];
            }

            return new MonteCarloPath(p.TimeGrid, values);
        })]);

    private static ExtOUWithJumpsProcess CreateKlugeProcess()
    {
        Real[] x0 = [3.0, 0.0];

        const Real beta = 5.0;
        const Real eta = 2.0;
        const Real jumpIntensity = 1.0;
        const Real speed = 1.0;
        const Real volatility = 2.0;

        var ouProcess = new ExtendedOrnsteinUhlenbeckProcess(speed, volatility, x0[0], ConstantB(x0[0]));
        return new ExtOUWithJumpsProcess(ouProcess, x0[1], beta, jumpIntensity, eta);
    }

    private static KlugeExtOUProcess CreateKlugeExtOUProcess()
    {
        // model definition
        const Real beta = 200;
        const Real eta = 1.0 / 0.2;
        const Real lambda = 4.0;
        const Real alpha = 7.0;
        const Real volatility_x = 1.4;
        const Real kappa = 4.45;
        Real volatility_u = Math.Sqrt(1.3);
        const Real rho = 0.7;

        Real[] x0 = [0.0, 0.0];

        var ouProcess = new ExtendedOrnsteinUhlenbeckProcess(alpha, volatility_x, x0[0], ConstantB(x0[0]));

        var lnPowerProcess = new ExtOUWithJumpsProcess(ouProcess, x0[1], beta, lambda, eta);

        const Real u = 0.0;
        var lnGasProcess = new ExtendedOrnsteinUhlenbeckProcess(kappa, volatility_u, u, ConstantB(u));

        return new KlugeExtOUProcess(rho, lnPowerProcess, lnGasProcess);
    }

    // QuantLib's std::lower_bound on the shape at (t − √ε, 0)
    private static Real ShapeAt((Real Time, Real Value)[] shape, Real t)
    {
        (Real Time, Real Value) key = (t - Math.Sqrt(QlEpsilon), 0.0);
        int lo = 0, hi = shape.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (shape[mid].Time < key.Time || (!(key.Time < shape[mid].Time) && shape[mid].Value < key.Value))
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return shape[lo].Value;
    }

    // QuantLib's PathFuelPrice: the gas price on the path at the hour t names
    private sealed class PathFuelPrice(MultiPath path, (Real Time, Real Value)[] shape) : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t)
        {
            Assert.True(t - Math.Sqrt(QlEpsilon) <= shape[^1].Time, "invalid time");

            var i = (Size)(t * 365U * 24U);
            Real f = ShapeAt(shape, t);
            return Math.Exp(path[2][i] + f);
        }

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }

    // QuantLib's PathSparkSpreadPrice: power less gas at the heat rate on the path at the hour t names
    private sealed class PathSparkSpreadPrice(
        Real heatRate, MultiPath path, (Real Time, Real Value)[] fuelShape, (Real Time, Real Value)[] powerShape)
        : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t)
        {
            Assert.True(t - Math.Sqrt(QlEpsilon) <= powerShape[^1].Time, "invalid time");

            var i = (Size)(t * 365U * 24U);
            Real f = ShapeAt(powerShape, t);
            Real g = ShapeAt(fuelShape, t);

            return Math.Exp(f + path[0][i] + path[1][i]) - (heatRate * Math.Exp(g + path[2][i]));
        }

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }
}
