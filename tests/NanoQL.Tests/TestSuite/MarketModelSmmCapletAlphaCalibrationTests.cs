// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/marketmodel_smmcapletalphacalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MarketModelSmmCapletAlphaCalibrationTests
{
    [Fact]
    public void TestFunction() => TestAlphaCalibration(false);

    // QuantLib's master's testFunction with its homogeneity flag a parameter (#2910, dbe958954), which its
    // testMaximumHomogeneityCalibration runs with true (Divergences/AlphaFinderTests.cs); testFunction, false, is
    // 1.44's case.
    internal static void TestAlphaCalibration(bool maximizeHomogeneity)
    {
        // Testing alpha caplet calibration in a lognormal coterminal swap market model...
        var setup = new CommonVars(alpha: 0.0);

        int numberOfRates = setup.TodaysForwards.Length;

        var evolution = new EvolutionDescription(setup.RateTimes);

        var fwdCorr = new ExponentialForwardCorrelation(setup.RateTimes, setup.LongTermCorrelation, setup.Beta);

        var cs = new LMMCurveState(setup.RateTimes);
        cs.SetOnForwardRates(setup.TodaysForwards);

        var corr = new CotSwapFromFwdCorrelation(fwdCorr, cs, setup.Displacement);

        var swapVariances = new PiecewiseConstantVariance[numberOfRates];
        for (int i = 0; i < numberOfRates; ++i)
        {
            swapVariances[i] = new PiecewiseConstantAbcdVariance(setup.A, setup.B, setup.C, setup.D, i, setup.RateTimes);
        }

        // create calibrator
        double[] alphaInitial = [.. Enumerable.Repeat(setup.Alpha, numberOfRates)];
        double[] alphaMax = [.. Enumerable.Repeat(1.0, numberOfRates)];
        double[] alphaMin = [.. Enumerable.Repeat(-1.0, numberOfRates)];
        var calibrator = new CTSMMCapletAlphaFormCalibration(
            evolution, corr, swapVariances, setup.CapletVols, cs, setup.Displacement, alphaInitial, alphaMax, alphaMin, maximizeHomogeneity);

        // calibrate
        int maxIterations = 10;
        double capletTolerance = 1e-4; // i.e. 1 bp
        int innerMaxIterations = 100;
        double innerTolerance = 1e-8;
        CTSMMCapletCalibrationResults results = calibrator.Calibrate(
            setup.NumberOfFactors, maxIterations, capletTolerance, innerMaxIterations, innerTolerance);
        Assert.True(results.Converged, "calibration failed");

        CommonVars.CheckFit(setup, cs, swapVariances, results.SwapPseudoRoots(), capletTolerance);
    }

    // QuantLib's setup, the same in the three caplet calibration files but for alpha_.
    private sealed class CommonVars
    {
        public CommonVars(double alpha)
        {
            // Times
            var calendar = new NullCalendar();
            var todaysDate = new Date(15, October, 2026);
            Date endDate = todaysDate + new Period(66, TimeUnit.Months);
            var context = new EvaluationContext { EvaluationDate = todaysDate };
            var dates = new Schedule(
                todaysDate, endDate, new Period(Frequency.Semiannual), calendar, BusinessDayConvention.Following,
                BusinessDayConvention.Following, DateGenerationRule.Backward, false, context);
            RateTimes = new double[dates.Size - 1];
            double[] accruals = new double[RateTimes.Length - 1];
            var dayCounter = new SimpleDayCounter();
            for (int i = 1; i < dates.Size; ++i)
            {
                RateTimes[i - 1] = dayCounter.YearFraction(todaysDate, dates[i]);
            }

            for (int i = 1; i < RateTimes.Length; ++i)
            {
                accruals[i - 1] = RateTimes[i] - RateTimes[i - 1];
            }

            // Rates & displacement
            TodaysForwards = new double[accruals.Length];
            Alpha = alpha;
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                TodaysForwards[i] = 0.03 + (0.0025 * i);
            }

            double[] mktCapletVols =
            [
                0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, 0.1340376439125532,
            ];

            var curveStateLmm = new LMMCurveState(RateTimes);
            curveStateLmm.SetOnForwardRates(TodaysForwards);
            int numberOfSwaps = curveStateLmm.CoterminalSwapRates().Length;
            CapletVols = new double[numberOfSwaps];
            for (int i = 0; i < numberOfSwaps; i++)
            {
                CapletVols[i] = mktCapletVols[i];
            }
        }

        public double[] RateTimes { get; }

        public double[] TodaysForwards { get; }

        public double[] CapletVols { get; }

        public int NumberOfFactors => 3;

        public double Alpha { get; }

        public double Displacement => 0.0;

        public double A => 0.0;

        public double B => 0.17;

        public double C => 1.0;

        public double D => 0.10;

        public double LongTermCorrelation => 0.5;

        public double Beta => 0.2;

        // QuantLib's checks after each calibration: the swaptions fitted to 1e-14, the caplets to the tolerance.
        public static void CheckFit(
            CommonVars setup,
            CurveState cs,
            PiecewiseConstantVariance[] swapVariances,
            IReadOnlyList<Matrix> swapPseudoRoots,
            double capletTolerance)
        {
            int numberOfRates = setup.TodaysForwards.Length;
            double[] rateTimes = setup.RateTimes;
            var smm = new PseudoRootFacade(
                swapPseudoRoots, rateTimes, cs.CoterminalSwapRates(), [.. Enumerable.Repeat(setup.Displacement, numberOfRates)]);
            var flmm = new CotSwapToFwdAdapter(smm);
            Matrix capletTotCovariance = flmm.TotalCovariance(numberOfRates - 1);

            double[] capletVols = new double[numberOfRates];
            for (int i = 0; i < numberOfRates; ++i)
            {
                capletVols[i] = Math.Sqrt(capletTotCovariance[i, i] / rateTimes[i]);
            }

            var errors = new List<string>();

            // check perfect swaption fit
            double error, swapTolerance = 1e-14;
            var swapTerminalCovariance = new Matrix(numberOfRates, numberOfRates, 0.0);
            for (int i = 0; i < numberOfRates; ++i)
            {
                double expSwaptionVol = swapVariances[i].TotalVolatility(i);
                swapTerminalCovariance += swapPseudoRoots[i] * MatrixFunctions.Transpose(swapPseudoRoots[i]);
                double swaptionVol = Math.Sqrt(swapTerminalCovariance[i, i] / rateTimes[i]);
                error = Math.Abs(swaptionVol - expSwaptionVol);
                if (error > swapTolerance)
                {
                    errors.Add(
                        $"failed to reproduce {DataFormattersFunctions.Ordinal(i + 1)} swaption vol:\n expected:  {DataFormattersFunctions.Rate(expSwaptionVol)}\n realized:  {DataFormattersFunctions.Rate(swaptionVol)}\n error:     {error}\n tolerance: {swapTolerance}");
                }
            }

            // check caplet fit
            for (int i = 0; i < numberOfRates; ++i)
            {
                error = Math.Abs(capletVols[i] - setup.CapletVols[i]);
                if (error > capletTolerance)
                {
                    errors.Add(
                        $"failed to reproduce {DataFormattersFunctions.Ordinal(i + 1)} caplet vol:\n expected:         {DataFormattersFunctions.Rate(setup.CapletVols[i])}\n realized:         {DataFormattersFunctions.Rate(capletVols[i])}\n percentage error: {error / setup.CapletVols[i]}\n error:            {error}\n tolerance:        {capletTolerance}");
                }
            }

            Assert.Empty(errors);
        }
    }
}
