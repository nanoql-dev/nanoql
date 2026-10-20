// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swaptionvolatilitycube.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testObservability (Vectors/TermStructures/SabrSwaptionVolatilityCubeVectorTests.cs); testZabrObservability (Vectors/TermStructures/ZabrSwaptionVolatilityCubeVectorTests.cs)

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.TestSuite.SwaptionVolStructuresUtilitiesTests;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwaptionVolatilityCubeTests
{
    // Adapted: the normal at-the-money matrix, whose reference date floats (QuantLib's calendar-first constructor), is
    // built by Create from the context; the cube calibrates when it is built, under the context, which its
    // at-the-money strikes take.
    [Fact]
    public void TestSabrNormalVolatility()
    {
        // Testing sabr normal volatility...

        var vars = new CommonVars();

        double[][] parametersGuess = Guesses(vars, 0.2, 0.5, 0.4, 0.0);
        bool[] isParameterFixed = [false, false, false, false];

        var volCube = new SabrSwaptionVolatilityCube(
            vars.NormalVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);
        double tolerance = 7.0e-4;
        vars.MakeAtmVolTest(volCube, tolerance);
    }

    // Adapted: the evaluation date (line 199) is the context's, and the curve, the matrix and the cube are built
    // under it; the cube calibrates when it is built, and its at-the-money strike takes the context.
    [Fact]
    public void TestSabrNormalTwoPassUsesStrikeSpreadMoneyness()
    {
        // Testing K-F interpolation in a two-pass normal SABR cube...

        var vars = new CommonVars();
        Date today = vars.Context.EvaluationDate;
        Date[] dates =
        [
            today,
            vars.Conventions.Calendar.Advance(today, 1 * TimeUnit.Years),
            vars.Conventions.Calendar.Advance(today, 5 * TimeUnit.Years),
            vars.Conventions.Calendar.Advance(today, 15 * TimeUnit.Years),
            vars.Conventions.Calendar.Advance(today, 70 * TimeUnit.Years),
        ];
        double[] zeroRates = [0.01, 0.015, 0.025, 0.035, 0.045];
        var termStructure = new ZeroCurve(dates, zeroRates, new Actual365Fixed(), vars.Conventions.Calendar);

        var swapIndex = new EuriborSwapIsdaFixA(2 * TimeUnit.Years, termStructure);
        var shortSwapIndex = new EuriborSwapIsdaFixA(1 * TimeUnit.Years, termStructure);
        int sections = vars.Cube.Tenors.Options.Count * vars.Cube.Tenors.Swaps.Count;
        Matrix normalVols = 0.05 * vars.Atm.Vols;
        SwaptionVolatilityMatrix normalAtm = SwaptionVolatilityMatrix.Create(
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            normalVols,
            vars.Conventions.DayCounter,
            vars.Context,
            false,
            VolatilityType.Normal);
        var volSpreads = new double[sections][];
        for (int section = 0; section < sections; ++section)
        {
            volSpreads[section] = new double[vars.Cube.StrikeSpreads.Count];
            for (int strike = 0; strike < vars.Cube.StrikeSpreads.Count; ++strike)
            {
                volSpreads[section][strike] = 0.03 * vars.Cube.VolSpreads[section, strike];
            }
        }

        var parametersGuess = new double[sections][];
        for (int i = 0; i < sections; ++i)
        {
            parametersGuess[i] = [0.05, 0.5, 0.4, 0.0];
        }

        var volCube = new SabrSwaptionVolatilityCube(
            normalAtm,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            volSpreads,
            swapIndex,
            shortSwapIndex,
            false,
            parametersGuess,
            [false, true, false, false],
            true,
            vars.Context);

        Period targetOption = 5 * TimeUnit.Years;
        Period targetSwap = 5 * TimeUnit.Years;
        double targetTime = normalAtm.TimeFromReference(normalAtm.OptionDateFromTenor(targetOption));
        double targetLength = normalAtm.SwapLength(targetSwap);
        double targetForward = volCube.AtmStrike(targetOption, targetSwap, vars.Context);
        double targetStrikeSpread = 0.02;
        double targetStrike = targetForward + targetStrikeSpread;

        Matrix sparseParameters = volCube.SparseSabrParameters();
        static int FindRow(Matrix matrix, double optionTime, double swapLength)
        {
            for (int row = 0; row < matrix.Rows; ++row)
            {
                if (ComparisonFunctions.CloseEnough(matrix[row, 0], swapLength)
                    && ComparisonFunctions.CloseEnough(matrix[row, 1], optionTime))
                {
                    return row;
                }
            }

            throw new NanoQLException("parameter row not found");
        }

        double[] optionNodes =
        [
            normalAtm.TimeFromReference(normalAtm.OptionDateFromTenor(1 * TimeUnit.Years)),
            normalAtm.TimeFromReference(normalAtm.OptionDateFromTenor(10 * TimeUnit.Years)),
        ];
        double[] swapNodes = [normalAtm.SwapLength(2 * TimeUnit.Years), normalAtm.SwapLength(10 * TimeUnit.Years)];

        double InterpolatedSpread(bool ratioMoneyness)
        {
            var cornerSpreads = new Matrix(2, 2, 0.0);
            for (int i = 0; i < 2; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    int row = FindRow(sparseParameters, optionNodes[i], swapNodes[j]);
                    double sourceForward = sparseParameters[row, 6];
                    double[] parameters =
                    [
                        sparseParameters[row, 2], sparseParameters[row, 3], sparseParameters[row, 4],
                        sparseParameters[row, 5],
                    ];
                    var smile = new SabrSmileSection(optionNodes[i], sourceForward, parameters, 0.0, VolatilityType.Normal);
                    double sourceStrike = ratioMoneyness
                        ? sourceForward * targetStrike / targetForward
                        : sourceForward + targetStrikeSpread;
                    double sourceAtm = normalAtm.Volatility(optionNodes[i], swapNodes[j], sourceForward, true);
                    cornerSpreads[i, j] = smile.Volatility(sourceStrike) - sourceAtm;
                }
            }

            double optionWeight = (targetTime - optionNodes[0]) / (optionNodes[1] - optionNodes[0]);
            double swapWeight = (targetLength - swapNodes[0]) / (swapNodes[1] - swapNodes[0]);
            return ((1.0 - optionWeight) * (((1.0 - swapWeight) * cornerSpreads[0, 0]) + (swapWeight * cornerSpreads[0, 1])))
                + (optionWeight * (((1.0 - swapWeight) * cornerSpreads[1, 0]) + (swapWeight * cornerSpreads[1, 1])));
        }

        Matrix denseMarketVols = volCube.VolCubeAtmCalibrated();
        int targetRow = FindRow(denseMarketVols, targetTime, targetLength);
        double targetAtm = normalAtm.Volatility(targetTime, targetLength, targetForward, true);
        double actual = denseMarketVols[targetRow, 6];
        double expected = targetAtm + InterpolatedSpread(false);
        double oldRatioResult = targetAtm + InterpolatedSpread(true);

        CheckSmall(actual - expected, 1.0e-12);
        Assert.True(Math.Abs(actual - oldRatioResult) > 1.0e-8, $"{Math.Abs(actual - oldRatioResult)} isn't above 1e-8");
    }

    // Adapted: the two at-the-money matrices, whose reference dates float, are built by Create from the context; the
    // cubes calibrate when they are built, under the context, which their at-the-money strikes take, so the
    // tolerance cube's no-throw (lines 461-469) is its construction's; a section's SabrSmileSection cast is a type
    // check.
    [Fact]
    public void TestSabrAtmCalibrationWithAsymmetricSmile()
    {
        // Testing exact SABR ATM calibration with asymmetric smiles...

        var vars = new CommonVars();

        void CheckAtmRecovery(SwaptionVolatilityStructure atmVols, double[] smileSpreads, double alphaGuess)
        {
            int sections = vars.Cube.Tenors.Options.Count * vars.Cube.Tenors.Swaps.Count;
            var volSpreads = new double[sections][];
            for (int section = 0; section < sections; ++section)
            {
                volSpreads[section] = [.. smileSpreads];
            }

            var parametersGuess = new double[sections][];
            for (int i = 0; i < sections; ++i)
            {
                parametersGuess[i] = [alphaGuess, 0.5, 0.4, 0.0];
            }

            var volCube = new SabrSwaptionVolatilityCube(
                atmVols,
                vars.Cube.Tenors.Options,
                vars.Cube.Tenors.Swaps,
                vars.Cube.StrikeSpreads,
                volSpreads,
                vars.SwapIndexBase,
                vars.ShortSwapIndexBase,
                false,
                parametersGuess,
                [false, true, false, false],
                true,
                vars.Context,
                null,
                null,
                null,
                null,
                false,
                50,
                false,
                0.0001,
                true);

            foreach (Period option in vars.Cube.Tenors.Options)
            {
                foreach (Period swap in vars.Cube.Tenors.Swaps)
                {
                    double strike = volCube.AtmStrike(option, swap, vars.Context);
                    double expectedVol = atmVols.Volatility(option, swap, strike, true);
                    double calculated = volCube.Volatility(option, swap, strike, true);
                    CheckSmall(calculated - expectedVol, 1.0e-10);
                    SabrSmileSection smile = Assert.IsType<SabrSmileSection>(volCube.SmileSection(option, swap));
                    CheckSmall(smile.Beta - 0.5, 1.0e-12);
                }
            }

            Matrix parameters = volCube.DenseSabrParameters();
            Matrix marketVols = volCube.VolCubeAtmCalibrated();
            bool hasRatioMoneynessAdjustment = false;
            for (int row = 0; row < parameters.Rows; ++row)
            {
                double optionTime = parameters[row, 1];
                double swapLength = parameters[row, 0];
                double forward = parameters[row, 6];
                double[] sabrParameters = [parameters[row, 2], parameters[row, 3], parameters[row, 4], parameters[row, 5]];
                var smile = new SabrSmileSection(
                    optionTime, forward, sabrParameters, atmVols.Shift(optionTime, swapLength), atmVols.VolatilityType);

                double squaredError = 0.0;
                double maxError = 0.0;
                for (int i = 0; i < vars.Cube.StrikeSpreads.Count; ++i)
                {
                    double error = smile.Volatility(forward + vars.Cube.StrikeSpreads[i]) - marketVols[row, i + 2];
                    squaredError += error * error;
                    double absolute = Math.Abs(error);
                    maxError = maxError < absolute ? absolute : maxError;
                }

                int n = vars.Cube.StrikeSpreads.Count;
                double rmsError = Math.Sqrt(squaredError / (n - 1));
                CheckSmall(parameters[row, 7] - rmsError, 1.0e-12);
                CheckSmall(parameters[row, 8] - maxError, 1.0e-12);

                if (atmVols.VolatilityType == VolatilityType.Normal)
                {
                    double atmVol = atmVols.Volatility(optionTime, swapLength, forward, true);
                    for (int i = 0; i < smileSpreads.Length; ++i)
                    {
                        CheckSmall(marketVols[row, i + 2] - atmVol - smileSpreads[i], 1.0e-12);
                    }
                }
                else
                {
                    double atmVol = atmVols.Volatility(optionTime, swapLength, forward, true);
                    for (int i = 0; i < smileSpreads.Length; ++i)
                    {
                        hasRatioMoneynessAdjustment = hasRatioMoneynessAdjustment
                            || Math.Abs(marketVols[row, i + 2] - atmVol - smileSpreads[i]) > 1.0e-10;
                    }
                }
            }

            if (atmVols.VolatilityType == VolatilityType.ShiftedLognormal)
            {
                Assert.True(hasRatioMoneynessAdjustment);
            }

            var twoPassCube = new SabrSwaptionVolatilityCube(
                atmVols,
                vars.Cube.Tenors.Options,
                vars.Cube.Tenors.Swaps,
                vars.Cube.StrikeSpreads,
                volSpreads,
                vars.SwapIndexBase,
                vars.ShortSwapIndexBase,
                false,
                parametersGuess,
                [false, true, false, false],
                true,
                vars.Context);
            bool hasUnanchoredAtmNode = false;
            foreach (Period option in vars.Cube.Tenors.Options)
            {
                foreach (Period swap in vars.Cube.Tenors.Swaps)
                {
                    double strike = twoPassCube.AtmStrike(option, swap, vars.Context);
                    double expectedVol = atmVols.Volatility(option, swap, strike, true);
                    double calculated = twoPassCube.Volatility(option, swap, strike, true);
                    hasUnanchoredAtmNode = hasUnanchoredAtmNode || Math.Abs(calculated - expectedVol) > 1.0e-8;
                }
            }

            Assert.True(hasUnanchoredAtmNode);

            var unanchoredSinglePassCube = new SabrSwaptionVolatilityCube(
                atmVols,
                vars.Cube.Tenors.Options,
                vars.Cube.Tenors.Swaps,
                vars.Cube.StrikeSpreads,
                volSpreads,
                vars.SwapIndexBase,
                vars.ShortSwapIndexBase,
                false,
                parametersGuess,
                [false, true, false, false],
                false,
                vars.Context,
                null,
                null,
                null,
                null,
                false,
                50,
                false,
                0.0001,
                true);
            bool singlePassHasUnanchoredAtmNode = false;
            foreach (Period option in vars.Cube.Tenors.Options)
            {
                foreach (Period swap in vars.Cube.Tenors.Swaps)
                {
                    double strike = unanchoredSinglePassCube.AtmStrike(option, swap, vars.Context);
                    double expectedVol = atmVols.Volatility(option, swap, strike, true);
                    double calculated = unanchoredSinglePassCube.Volatility(option, swap, strike, true);
                    singlePassHasUnanchoredAtmNode =
                        singlePassHasUnanchoredAtmNode || Math.Abs(calculated - expectedVol) > 1.0e-8;
                }
            }

            Assert.True(singlePassHasUnanchoredAtmNode);

            if (atmVols.VolatilityType == VolatilityType.ShiftedLognormal)
            {
                Matrix calibrationParameters = unanchoredSinglePassCube.DenseSabrParameters();
                double largestCalibrationError = 0.0;
                double largestAnchoredError = 0.0;
                for (int row = 0; row < parameters.Rows; ++row)
                {
                    double c = calibrationParameters[row, 7];
                    largestCalibrationError = largestCalibrationError < c ? c : largestCalibrationError;
                    double a = parameters[row, 7];
                    largestAnchoredError = largestAnchoredError < a ? a : largestAnchoredError;
                }

                Assert.True(largestAnchoredError > largestCalibrationError);
                double tolerance = 0.5 * (largestCalibrationError + largestAnchoredError);

                // BOOST_CHECK_NO_THROW: the cube calibrates when it is built.
                var toleranceCube = new SabrSwaptionVolatilityCube(
                    atmVols,
                    vars.Cube.Tenors.Options,
                    vars.Cube.Tenors.Swaps,
                    vars.Cube.StrikeSpreads,
                    volSpreads,
                    vars.SwapIndexBase,
                    vars.ShortSwapIndexBase,
                    false,
                    parametersGuess,
                    [false, true, false, false],
                    true,
                    vars.Context,
                    null,
                    tolerance,
                    null,
                    0.0020,
                    false,
                    50,
                    false,
                    0.0001,
                    true);
                _ = toleranceCube.DenseSabrParameters();
            }
        }

        var shifts = new Matrix(vars.Atm.Tenors.Options.Count, vars.Atm.Tenors.Swaps.Count);
        for (int i = 0; i < shifts.Rows; ++i)
        {
            for (int j = 0; j < shifts.Columns; ++j)
            {
                shifts[i, j] = 0.001 * (i + (2 * j));
            }
        }

        SwaptionVolatilityMatrix shiftedLognormalAtm = SwaptionVolatilityMatrix.Create(
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            vars.Atm.Vols,
            vars.Conventions.DayCounter,
            vars.Context,
            false,
            VolatilityType.ShiftedLognormal,
            shifts);
        CheckAtmRecovery(shiftedLognormalAtm, [0.045, 0.015, 0.0, 0.010, 0.030], 0.05);

        var normalAtmVols = new Matrix(vars.Atm.Tenors.Options.Count, vars.Atm.Tenors.Swaps.Count);
        for (int i = 0; i < normalAtmVols.Rows; ++i)
        {
            for (int j = 0; j < normalAtmVols.Columns; ++j)
            {
                normalAtmVols[i, j] = 0.05 * vars.Atm.Vols[i, j];
            }
        }

        SwaptionVolatilityMatrix normalAtm = SwaptionVolatilityMatrix.Create(
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            normalAtmVols,
            vars.Conventions.DayCounter,
            vars.Context,
            false,
            VolatilityType.Normal);
        CheckAtmRecovery(normalAtm, [0.00135, 0.00045, 0.0, 0.00030, 0.00090], 0.05);
    }

    // Adapted: the cube takes the context its sections' at-the-money strikes read.
    [Fact]
    public void TestAtmVols()
    {
        // Testing swaption volatility cube (atm vols)...

        var vars = new CommonVars();

        var volCube = new InterpolatedSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            vars.Context);

        double tolerance = 1.0e-16;
        vars.MakeAtmVolTest(volCube, tolerance);
    }

    // Adapted: the cube takes the context its sections' at-the-money strikes read.
    [Fact]
    public void TestSmile()
    {
        // Testing swaption volatility cube (smile)...

        var vars = new CommonVars();

        var volCube = new InterpolatedSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            vars.Context);

        double tolerance = 1.0e-16;
        vars.MakeVolSpreadsTest(volCube, tolerance);
    }

    // Adapted: the cube calibrates when it is built, under the context, which its at-the-money strikes take.
    [Fact]
    public void TestSabrVols()
    {
        // Testing swaption volatility cube (sabr interpolation)...

        var vars = new CommonVars();

        double[][] parametersGuess = Guesses(vars, 0.2, 0.5, 0.4, 0.0);
        bool[] isParameterFixed = [false, false, false, false];

        var volCube = new SabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);
        double tolerance = 3.0e-4;
        vars.MakeAtmVolTest(volCube, tolerance);

        tolerance = 12.0e-4;
        vars.MakeVolSpreadsTest(volCube, tolerance);
    }

    // Adapted: the cube calibrates when it is built, under the context; the spread is a value, so the observability
    // checks (lines 657-668), a notification from the cube and from a changed spread, have nothing to observe: a new
    // spread is a new structure.
    [Fact]
    public void TestSpreadedCube()
    {
        // Testing spreaded swaption volatility cube...

        var vars = new CommonVars();

        double[][] parametersGuess = Guesses(vars, 0.2, 0.5, 0.4, 0.0);
        bool[] isParameterFixed = [false, false, false, false];

        SwaptionVolatilityStructure volCube = new SabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        double spread = 0.0001;
        SwaptionVolatilityStructure spreadedVolCube = new SpreadedSwaptionVolatility(volCube, spread);
        var strikes = new List<double>();
        for (int k = 1; k < 100; k++)
        {
            strikes.Add(k * .01);
        }

        for (int i = 0; i < vars.Cube.Tenors.Options.Count; i++)
        {
            for (int j = 0; j < vars.Cube.Tenors.Swaps.Count; j++)
            {
                SmileSection smileSectionByCube = volCube.SmileSection(vars.Cube.Tenors.Options[i], vars.Cube.Tenors.Swaps[j]);
                SmileSection smileSectionBySpreadedCube =
                    spreadedVolCube.SmileSection(vars.Cube.Tenors.Options[i], vars.Cube.Tenors.Swaps[j]);
                foreach (double strike in strikes)
                {
                    double diff = spreadedVolCube.Volatility(vars.Cube.Tenors.Options[i], vars.Cube.Tenors.Swaps[j], strike)
                        - volCube.Volatility(vars.Cube.Tenors.Options[i], vars.Cube.Tenors.Swaps[j], strike);
                    Assert.True(
                        !(Math.Abs(diff - spread) > 1e-16),
                        $"diff!=spread in volatility method: expiry time = {vars.Cube.Tenors.Options[i]}, swap length = "
                        + $"{vars.Cube.Tenors.Swaps[j]}, atm strike = {strike}, diff = {diff}, spread = {spread}");

                    diff = smileSectionBySpreadedCube.Volatility(strike) - smileSectionByCube.Volatility(strike);
                    Assert.True(
                        !(Math.Abs(diff - spread) > 1e-16),
                        $"diff!=spread in smile section method: expiry time = {vars.Cube.Tenors.Options[i]}, swap length = "
                        + $"{vars.Cube.Tenors.Swaps[j]}, atm strike = {strike}, diff = {diff}, spread = {spread}");
                }
            }
        }
    }

    // Adapted: the cube calibrates when it is built, under the context; a section's SabrSmileSection cast is a type
    // check.
    [Fact]
    public void TestSabrParameters()
    {
        // Testing interpolation of SABR smile sections...

        var vars = new CommonVars();

        double[][] parametersGuess = Guesses(vars, 0.2, 0.5, 0.4, 0.0);
        bool[] isParameterFixed = [false, false, false, false];

        var volCube = new SabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        SwaptionVolatilityStructure volStructure = volCube;
        double tolerance = 1.0e-4;

        // Interpolating between two SmileSection objects

        // First section: maturity = 10Y, tenor = 2Y
        SmileSection smileSection1 = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(2, TimeUnit.Years));

        // Second section: maturity = 10Y, tenor = 4Y
        SmileSection smileSection2 = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(4, TimeUnit.Years));

        // Third section in the middle: maturity = 10Y, tenor = 3Y
        SmileSection smileSection3 = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(3, TimeUnit.Years));

        var sabr1 = Assert.IsType<SabrSmileSection>(smileSection1);
        var sabr2 = Assert.IsType<SabrSmileSection>(smileSection2);
        var sabr3 = Assert.IsType<SabrSmileSection>(smileSection3);

        // test alpha interpolation
        double alpha1 = sabr1.Alpha;
        double alpha2 = sabr2.Alpha;
        double alpha3 = sabr3.Alpha;
        double alpha12 = 0.5 * (alpha1 + alpha2);
        Assert.True(
            Math.Abs(alpha3 - alpha12) <= tolerance,
            $"Checking interpolation of alpha parameters: expected = {alpha12}, observed = {alpha3}");

        // test beta interpolation
        double beta1 = sabr1.Beta;
        double beta2 = sabr2.Beta;
        double beta3 = sabr3.Beta;
        double beta12 = 0.5 * (beta1 + beta2);
        Assert.True(
            Math.Abs(beta3 - beta12) <= tolerance,
            $"Checking interpolation of beta parameters: expected = {beta12}, observed = {beta3}");

        // test rho interpolation
        double rho1 = sabr1.Rho;
        double rho2 = sabr2.Rho;
        double rho3 = sabr3.Rho;
        double rho12 = 0.5 * (rho1 + rho2);
        Assert.True(
            Math.Abs(rho3 - rho12) <= tolerance,
            $"Checking interpolation of rho parameters: expected = {rho12}, observed = {rho3}");

        // test nu interpolation
        double nu1 = sabr1.Nu;
        double nu2 = sabr2.Nu;
        double nu3 = sabr3.Nu;
        double nu12 = 0.5 * (nu1 + nu2);
        Assert.True(
            Math.Abs(nu3 - nu12) <= tolerance,
            $"Checking interpolation of nu parameters: expected = {nu12}, observed = {nu3}");

        // test forward interpolation
        double forward1 = smileSection1.AtmLevel()!.Value;
        double forward2 = smileSection2.AtmLevel()!.Value;
        double forward3 = smileSection3.AtmLevel()!.Value;
        double forward12 = 0.5 * (forward1 + forward2);
        Assert.True(
            Math.Abs(forward3 - forward12) <= tolerance,
            $"Checking interpolation of forward parameters: expected = {forward12}, observed = {forward3}");
    }

    // Adapted: the cube calibrates when it is built, under the context, which its at-the-money strikes take;
    // BOOST_REQUIRE on a section is its non-null type.
    [Fact]
    public void TestZabrVols()
    {
        // Testing swaption volatility cube (ZABR interpolation)...

        var vars = new CommonVars();

        // ZABR requires 5 parameters: alpha, beta, nu, rho, gamma
        // gamma=1.0 gives SABR-like behaviour
        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 1.0);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;  // Fix beta (standard practice, as in SABR)
        isParameterFixed[4] = true;  // Fix gamma for stability

        var volCube = new ZabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        // ZABR should recover ATM vols with reasonable tolerance
        double tolerance = 5.0e-4;
        vars.MakeAtmVolTest(volCube, tolerance);

        // ZABR should also recover vol spreads with reasonable tolerance
        tolerance = 15.0e-4;
        vars.MakeVolSpreadsTest(volCube, tolerance);

        // Access via base class interface for smileSection
        SwaptionVolatilityStructure volStructure = volCube;

        // Test smile section retrieval
        SmileSection smileSection = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(2, TimeUnit.Years));
        Assert.NotNull(smileSection);

        // Test volatility at ATM
        double atmStrike = volCube.AtmStrike(new Period(10, TimeUnit.Years), new Period(2, TimeUnit.Years), vars.Context);
        double vol = volCube.Volatility(new Period(10, TimeUnit.Years), new Period(2, TimeUnit.Years), atmStrike, true);
        Assert.True(vol > 0.0);
    }

    // Adapted: the cube calibrates when it is built, under the context.
    [Fact]
    public void TestZabrSmileSection()
    {
        // Testing ZABR swaption volatility cube smile sections...

        var vars = new CommonVars();

        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 1.0);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;  // Fix beta (standard practice)
        isParameterFixed[4] = true;

        var volCube = new ZabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        // Access via base class interface
        SwaptionVolatilityStructure volStructure = volCube;

        // Get smile section and test its properties
        SmileSection smileSection = volStructure.SmileSection(new Period(5, TimeUnit.Years), new Period(5, TimeUnit.Years));

        Assert.NotNull(smileSection);
        Assert.True(smileSection.AtmLevel() > 0.0);

        // Test volatility at different strikes
        double atmStrike = smileSection.AtmLevel()!.Value;
        double[] strikes = [atmStrike * 0.8, atmStrike, atmStrike * 1.2];

        foreach (double strike in strikes)
        {
            double vol = smileSection.Volatility(strike);
            Assert.True(vol > 0.0, $"ZABR smile section volatility at strike {strike} should be positive");
        }
    }

    // Adapted: the cube calibrates when it is built, under the context; a section's ZabrSmileSection cast is a type
    // check.
    [Fact]
    public void TestZabrParameters()
    {
        // Testing interpolation of ZABR smile section parameters...

        var vars = new CommonVars();

        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 1.0);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;  // Fix beta (standard practice)
        isParameterFixed[4] = true;

        var volCube = new ZabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        SwaptionVolatilityStructure volStructure = volCube;

        // Test parameter interpolation by comparing volatilities at intermediate points
        // If parameters interpolate correctly, volatilities at intermediate tenors should
        // be smooth interpolations of neighboring volatilities

        // Section 1: maturity = 10Y, tenor = 2Y
        SmileSection smileSection1 = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(2, TimeUnit.Years));

        // Section 2: maturity = 10Y, tenor = 4Y
        SmileSection smileSection2 = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(4, TimeUnit.Years));

        // Section 3: maturity = 10Y, tenor = 3Y (intermediate)
        SmileSection smileSection3 = volStructure.SmileSection(new Period(10, TimeUnit.Years), new Period(3, TimeUnit.Years));

        // Test forward rate interpolation (which depends on parameter interpolation)
        double forward1 = smileSection1.AtmLevel()!.Value;
        double forward2 = smileSection2.AtmLevel()!.Value;
        double forward3 = smileSection3.AtmLevel()!.Value;
        double forward12 = 0.5 * (forward1 + forward2);

        double tolerance = 1.0e-4;
        Assert.True(
            !(Math.Abs(forward3 - forward12) > tolerance),
            $"Checking interpolation of ZABR forward parameters: forward at 2Y = {forward1}, forward at 4Y = {forward2}, "
            + $"expected at 3Y = {forward12}, observed at 3Y = {forward3}");

        // Test ATM volatility smoothness (indirect parameter interpolation test)
        double vol1 = smileSection1.Volatility(forward1);
        double vol2 = smileSection2.Volatility(forward2);
        double vol3 = smileSection3.Volatility(forward3);

        // Verify that interpolated section produces reasonable volatility
        // (between the two boundary sections or close to their average)
        double vol12 = 0.5 * (vol1 + vol2);
        double volTolerance = 5.0e-3;  // 50 bps tolerance for vol interpolation

        Assert.True(
            !(Math.Abs(vol3 - vol12) > volTolerance),
            $"Checking interpolation of ZABR ATM volatility: vol at 2Y = {vol1}, vol at 4Y = {vol2}, expected at 3Y = "
            + $"{vol12}, observed at 3Y = {vol3}");

        // Downcast to access individual calibrated ZABR parameters,
        // mirroring testSabrParameters which checks alpha/beta/rho/nu directly.
        // This catches bugs such as parameters stored in wrong order in params vector.
        var zs1 = Assert.IsType<ZabrSmileSection<ZabrShortMaturityLognormal>>(smileSection1);
        var zs2 = Assert.IsType<ZabrSmileSection<ZabrShortMaturityLognormal>>(smileSection2);
        var zs3 = Assert.IsType<ZabrSmileSection<ZabrShortMaturityLognormal>>(smileSection3);

        // alpha interpolation
        double alpha1 = zs1.Model.Alpha, alpha2 = zs2.Model.Alpha;
        double alpha3 = zs3.Model.Alpha;
        Assert.True(
            !(Math.Abs(alpha3 - (0.5 * (alpha1 + alpha2))) > tolerance),
            $"Checking interpolation of ZABR alpha: alpha at 2Y = {alpha1}, alpha at 4Y = {alpha2}, expected at 3Y = "
            + $"{0.5 * (alpha1 + alpha2)}, observed at 3Y = {alpha3}");

        // nu interpolation
        double nu1 = zs1.Model.Nu, nu2 = zs2.Model.Nu;
        double nu3 = zs3.Model.Nu;
        Assert.True(
            !(Math.Abs(nu3 - (0.5 * (nu1 + nu2))) > tolerance),
            $"Checking interpolation of ZABR nu: nu at 2Y = {nu1}, nu at 4Y = {nu2}, expected at 3Y = "
            + $"{0.5 * (nu1 + nu2)}, observed at 3Y = {nu3}");

        // rho interpolation
        double rho1 = zs1.Model.Rho, rho2 = zs2.Model.Rho;
        double rho3 = zs3.Model.Rho;
        Assert.True(
            !(Math.Abs(rho3 - (0.5 * (rho1 + rho2))) > tolerance),
            $"Checking interpolation of ZABR rho: rho at 2Y = {rho1}, rho at 4Y = {rho2}, expected at 3Y = "
            + $"{0.5 * (rho1 + rho2)}, observed at 3Y = {rho3}");

        // Note: beta and gamma interpolation are not tested here because both
        // parameters are fixed (isParameterFixed[1]=true, isParameterFixed[4]=true),
        // so their layer values do not change between sections.
    }

    // Adapted: the cube calibrates when it is built, under the context.
    [Fact]
    public void TestZabrWithNonUnitGamma()
    {
        // Testing ZABR swaption volatility cube with gamma != 1.0...

        var vars = new CommonVars();

        // ZABR with gamma = 0.75 (different from SABR-like gamma=1.0)
        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 0.75);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;  // Fix beta (standard practice)
        isParameterFixed[4] = true;  // Fix gamma to test non-unit value behavior

        var volCube = new ZabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        // With gamma != 1.0, ZABR should still produce valid volatilities
        // Tolerance may be slightly higher due to different smile shape
        double tolerance = 8.0e-4;
        vars.MakeAtmVolTest(volCube, tolerance);

        // Verify smile section works with non-unit gamma
        SwaptionVolatilityStructure volStructure = volCube;
        SmileSection smileSection = volStructure.SmileSection(new Period(5, TimeUnit.Years), new Period(5, TimeUnit.Years));

        Assert.NotNull(smileSection);

        // Test volatility smile shape - with gamma < 1, expect different curvature
        double atmStrike = smileSection.AtmLevel()!.Value;
        double atmVol = smileSection.Volatility(atmStrike);
        double otmVol = smileSection.Volatility(atmStrike * 1.2);
        double itmVol = smileSection.Volatility(atmStrike * 0.8);

        Assert.True(atmVol > 0.0);
        Assert.True(otmVol > 0.0);
        Assert.True(itmVol > 0.0);
    }

    // Adapted: the cube calibrates when it is built, under the context; a section's ZabrSmileSection cast is a type
    // check.
    [Fact]
    public void TestZabrWithFreeGamma()
    {
        // Testing ZABR cube with free gamma calibration...

        var vars = new CommonVars();

        // gamma=1.0 is only the initial guess; optimizer will calibrate it freely
        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 1.0);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;   // fix beta

        var cube = new ZabrSwaptionVolatilityCube(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        // Get a smile section and verify gamma was actually calibrated
        Period optionTenor = vars.Cube.Tenors.Options[0];
        Period swapTenor = vars.Cube.Tenors.Swaps[0];
        SmileSection section = cube.SmileSection(optionTenor, swapTenor);
        Assert.NotNull(section);

        // Downcast to ZabrSmileSection to access calibrated gamma
        var zabrSection = Assert.IsType<ZabrSmileSection<ZabrShortMaturityLognormal>>(section);

        double calibratedGamma = zabrSection.Model.Gamma;

        // Gamma should be within valid bounds: ZabrSpecs::direct() caps gamma at 1.9
        Assert.True(calibratedGamma > 0.1);
        Assert.True(calibratedGamma < 1.9);

        // Verify gamma actually moved from the initial guess of 1.0
        Assert.True(Math.Abs(calibratedGamma - 1.0) > 1e-6);

        // Verify ATM vol is positive
        double atmStrike = section.AtmLevel()!.Value;
        double atmVol = section.Volatility(atmStrike);
        Assert.True(atmVol > 0.0);
    }

    // Adapted: QuantLib's cube calibrates at its first query, so its refusal of the shifted structure comes there
    // (line 1258); nanoql's at-the-money-calibrated cube calibrates when it is built, so the refusal comes at
    // construction, under the context. The matrix with shifts floats, built by Create from the context.
    [Fact]
    public void TestZabrShiftedVolThrows()
    {
        // Testing that ZABR cube throws for non-zero ATM vol shift...

        var vars = new CommonVars();

        // Build an ATM vol matrix with a uniform 2% shift.
        // ZABR does not support shifted lognormal vols (QL_REQUIRE in createSmileSection).
        int nOpts = vars.Atm.Tenors.Options.Count;
        int nSwps = vars.Atm.Tenors.Swaps.Count;
        var shifts = new Matrix(nOpts, nSwps, 0.02);

        SwaptionVolatilityStructure shiftedAtmVol = SwaptionVolatilityMatrix.Create(
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            vars.Atm.Vols,
            vars.Conventions.DayCounter,
            vars.Context,
            false,
            VolatilityType.ShiftedLognormal,
            shifts);

        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 1.0);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;
        isParameterFixed[4] = true;

        // Building the cube calibrates it and must throw
        // because the ATM vol structure has non-zero shift
        Assert.Throws<ArgumentException>(() => new ZabrSwaptionVolatilityCube(
            shiftedAtmVol,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context));
    }

    // Adapted: the cube calibrates when it is built, under the context; QuantLib's typedef is the generic cube on the
    // normal kernel's model.
    [Fact]
    public void TestZabrAlternativeKernel()
    {
        // Testing ZABR cube with ZabrShortMaturityNormal kernel...

        var vars = new CommonVars();

        double[][] parametersGuess = vars.MakeZabrParametersGuess(0.2, 0.5, 0.4, 0.0, 1.0);
        bool[] isParameterFixed = new bool[5];
        isParameterFixed[1] = true;
        isParameterFixed[4] = true;

        // Instantiate with a non-default kernel — this exercises the kernel
        // template parameter added in zabrswaptionvolatilitycube.hpp.
        // Note: ZabrLocalVolatility and ZabrFullFd kernels are not covered here
        // due to the high cost of finite-difference evaluation.
        var volCube = new XabrSwaptionVolatilityCube<SwaptionVolCubeZabrModel<ZabrShortMaturityNormal>>(
            vars.AtmVolMatrix,
            vars.Cube.Tenors.Options,
            vars.Cube.Tenors.Swaps,
            vars.Cube.StrikeSpreads,
            vars.VolSpreads,
            vars.SwapIndexBase,
            vars.ShortSwapIndexBase,
            vars.VegaWeighedSmileFit,
            parametersGuess,
            isParameterFixed,
            true,
            vars.Context);

        // Smoke test: cube constructs and returns positive vol at ATM
        SmileSection section = volCube.SmileSection(new Period(5, TimeUnit.Years), new Period(5, TimeUnit.Years));
        Assert.NotNull(section);

        double atmStrike = section.AtmLevel()!.Value;
        Assert.True(atmStrike > 0.0);
        Assert.True(section.Volatility(atmStrike) > 0.0);
    }

    private static double[][] Guesses(CommonVars vars, double alpha, double beta, double nu, double rho)
    {
        int n = vars.Cube.Tenors.Options.Count * vars.Cube.Tenors.Swaps.Count;
        var parametersGuess = new double[n][];
        for (int i = 0; i < n; i++)
        {
            parametersGuess[i] = [alpha, beta, nu, rho];
        }

        return parametersGuess;
    }

    // QL_CHECK_SMALL: |x| <= tolerance.
    private static void CheckSmall(double x, double tolerance) =>
        Assert.True(Math.Abs(x) <= tolerance, $"|{x}| exceeds {tolerance}");

    // The fixture. The at-the-money matrices float with the evaluation date, so they are built by Create from the
    // context: the date QuantLib's test runner sets.
    private sealed class CommonVars
    {
        public CommonVars()
        {
            Conventions.SetConventions();

            // ATM swaptionvolmatrix
            Atm.SetMarketData();
            AtmVolMatrix = SwaptionVolatilityMatrix.Create(
                Conventions.Calendar,
                Conventions.OptionBdc,
                Atm.Tenors.Options,
                Atm.Tenors.Swaps,
                Atm.Vols,
                Conventions.DayCounter,
                Context);
            NormalVolMatrix = SwaptionVolatilityMatrix.Create(
                Conventions.Calendar,
                Conventions.OptionBdc,
                Atm.Tenors.Options,
                Atm.Tenors.Swaps,
                Atm.Vols,
                Conventions.DayCounter,
                Context,
                false,
                VolatilityType.Normal);

            // Swaptionvolcube
            Cube.SetMarketData();
            VolSpreads = new double[Cube.VolSpreads.Rows][];
            for (int i = 0; i < Cube.VolSpreads.Rows; i++)
            {
                VolSpreads[i] = [.. Cube.VolSpreads.Row(i)];
            }

            TermStructure = FlatForward.Create(0, new NullCalendar(), 0.05, new Actual365Fixed(), Context);

            SwapIndexBase = new EuriborSwapIsdaFixA(2 * TimeUnit.Years, TermStructure);
            ShortSwapIndexBase = new EuriborSwapIsdaFixA(1 * TimeUnit.Years, TermStructure);

            VegaWeighedSmileFit = false;
        }

        // global data
        public EvaluationContext Context { get; } = new() { EvaluationDate = new Date(16, Month.September, 2015) };

        public SwaptionMarketConventions Conventions { get; } = new();

        public AtmVolatility Atm { get; } = new();

        public SwaptionVolatilityStructure AtmVolMatrix { get; }

        public SwaptionVolatilityStructure NormalVolMatrix { get; }

        public VolatilityCube Cube { get; } = new();

        // The cube's spreads as the cubes take them: a row per option and swap tenor pair.
        public double[][] VolSpreads { get; }

        public YieldTermStructure TermStructure { get; }

        public SwapIndex SwapIndexBase { get; }

        public SwapIndex ShortSwapIndexBase { get; }

        public bool VegaWeighedSmileFit { get; }

        // utilities
        public double[][] MakeZabrParametersGuess(double alpha, double beta, double nu, double rho, double gamma)
        {
            int n = Cube.Tenors.Options.Count * Cube.Tenors.Swaps.Count;
            var guess = new double[n][];
            for (int i = 0; i < n; ++i)
            {
                guess[i] = [alpha, beta, nu, rho, gamma];
            }

            return guess;
        }

        public void MakeAtmVolTest(SwaptionVolatilityCube volCube, double tolerance)
        {
            foreach (Period option in Atm.Tenors.Options)
            {
                foreach (Period swap in Atm.Tenors.Swaps)
                {
                    double strike = volCube.AtmStrike(option, swap, Context);
                    double expVol = AtmVolMatrix.Volatility(option, swap, strike, true);
                    double actVol = volCube.Volatility(option, swap, strike, true);
                    double error = Math.Abs(expVol - actVol);
                    Assert.True(
                        !(error > tolerance),
                        $"recovery of atm vols failed: expiry time = {option}, swap length = {swap}, atm strike = "
                        + $"{strike}, exp. vol = {expVol}, actual vol = {actVol}, error = {error}, tolerance = {tolerance}");
                }
            }
        }

        public void MakeVolSpreadsTest(SwaptionVolatilityCube volCube, double tolerance)
        {
            for (int i = 0; i < Cube.Tenors.Options.Count; i++)
            {
                for (int j = 0; j < Cube.Tenors.Swaps.Count; j++)
                {
                    for (int k = 0; k < Cube.StrikeSpreads.Count; k++)
                    {
                        double atmStrike = volCube.AtmStrike(Cube.Tenors.Options[i], Cube.Tenors.Swaps[j], Context);
                        double atmVol = AtmVolMatrix.Volatility(Cube.Tenors.Options[i], Cube.Tenors.Swaps[j], atmStrike, true);
                        double vol = volCube.Volatility(
                            Cube.Tenors.Options[i], Cube.Tenors.Swaps[j], atmStrike + Cube.StrikeSpreads[k], true);
                        double spread = vol - atmVol;
                        double expVolSpread = Cube.VolSpreads[(i * Cube.Tenors.Swaps.Count) + j, k];
                        double error = Math.Abs(expVolSpread - spread);
                        Assert.True(
                            !(error > tolerance),
                            $"recovery of smile vol spreads failed: option tenor = {Cube.Tenors.Options[i]}, swap tenor = "
                            + $"{Cube.Tenors.Swaps[j]}, atm strike = {atmStrike}, strike spread = {Cube.StrikeSpreads[k]}, "
                            + $"atm vol = {atmVol}, smiled vol = {vol}, vol spread = {spread}, exp. vol spread = "
                            + $"{expVolSpread}, error = {error}, tolerance = {tolerance}");
                    }
                }
            }
        }
    }
}
