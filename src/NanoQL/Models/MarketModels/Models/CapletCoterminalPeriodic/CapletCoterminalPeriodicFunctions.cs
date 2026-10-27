// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/capletcoterminalperiodic.hpp ql/models/marketmodels/models/capletcoterminalperiodic.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The periodic caplet and swaption calibration: a coterminal swap market model on short rates fitted to the short
/// caplets and to the swaptions on long rates, each spanning a period of the short ones.
/// </summary>
public static class CapletCoterminalPeriodicFunctions
{
    /// <summary>
    /// Calibrates to the short caplets and the long swaptions: each pass runs the maximally homogeneous calibration on
    /// the specifier's short variances at the current scales, then rescales each long swaption's by the market's over
    /// the model's volatility, until the long swaptions' root-mean-square error is within the tolerance, stops
    /// improving by a tenth of it, or the passes run out.
    /// </summary>
    /// <param name="evolution">The evolution: to the reset of each short rate.</param>
    /// <param name="corr">The short swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">
    /// The specifier of the short variances from the long ones; unchanged, the passes working on copies (the S-mm
    /// plan's MR2 (c)), where QuantLib's is left at the last pass's scales.
    /// </param>
    /// <param name="capletVols">The short caplet volatilities, one per short rate.</param>
    /// <param name="cs">The curve state.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="caplet0Swaption1Priority">The inner calibration's priority.</param>
    /// <param name="numberOfFactors">The factors.</param>
    /// <param name="period">The short rates each long one spans, at least 1.</param>
    /// <param name="max1dIterations">Each closest-point search's iterations.</param>
    /// <param name="tolerance1d">Each closest-point search's tolerance.</param>
    /// <param name="maxUnperiodicIterations">The inner calibration's passes.</param>
    /// <param name="toleranceUnperiodic">The inner calibration's tolerance.</param>
    /// <param name="maxPeriodIterations">The passes, at least 1.</param>
    /// <param name="periodTolerance">The long swaptions' root-mean-square error at which the passes stop.</param>
    /// <returns>The results.</returns>
    /// <exception cref="ArgumentException">
    /// The evolution isn't to each rate's reset, the specifier's long rates or the caplet volatilities don't fit, or
    /// the inner calibration's inputs don't.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="period"/> or <paramref name="maxPeriodIterations"/> is below 1, or an iteration count negative.
    /// </exception>
    public static CapletSwaptionPeriodicCalibrationResults CapletSwaptionPeriodicCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        VolatilityInterpolationSpecifier displacedSwapVariances,
        ReadOnlySpan<double> capletVols,
        CurveState cs,
        double displacement,
        double caplet0Swaption1Priority,
        int numberOfFactors,
        int period,
        int max1dIterations,
        double tolerance1d,
        int maxUnperiodicIterations,
        double toleranceUnperiodic,
        int maxPeriodIterations,
        double periodTolerance)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentNullException.ThrowIfNull(corr);
        ArgumentNullException.ThrowIfNull(displacedSwapVariances);
        ArgumentNullException.ThrowIfNull(cs);

        // QuantLib divides by the period, and writes the first pass's row of a matrix of maxPeriodIterations rows
        // (MR3 (xx))
        if (!(period >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "period must be at least 1");
        }

        if (!(maxPeriodIterations >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(maxPeriodIterations), maxPeriodIterations, "maxPeriodIterations must be at least 1");
        }

        int numberSmallRates = evolution.NumberOfRates;
        int numberSmallSteps = evolution.NumberOfSteps;

        if (!(numberSmallSteps == numberSmallRates))
        {
            throw new ArgumentException("periodic calibration class requires evolution to the reset of each rate", nameof(evolution));
        }

        int numberBigRates = numberSmallRates / period;
        int offset = numberSmallRates % period;

        double[] newDisplacements = [.. Enumerable.Repeat(displacement, numberBigRates)];

        if (!(displacedSwapVariances.NoBigRates == numberBigRates))
        {
            throw new ArgumentException(
                "mismatch between number of swap variances given and number of rates and period", nameof(displacedSwapVariances));
        }

        // QuantLib reads the last caplet volatility before its inner calibration checks their number, the back of none
        // undefined (MR3 (xx)); the check's text is the inner calibration's
        if (!(capletVols.Length == numberSmallRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between EvolutionDescription number of rates ({numberSmallRates}) and mktCapletVols size ({capletVols.Length})"),
                nameof(capletVols));
        }

        int failures = 0;

        double[] scalingFactors = [.. Enumerable.Repeat(1.0, numberBigRates)];

        // the caller's specifier is left unchanged: the passes read copies (MR2 (c))
        VolatilityInterpolationSpecifier specifier = displacedSwapVariances.WithLastCapletVol(capletVols[^1]);

        double[] marketSwaptionVols = new double[numberBigRates];
        for (int i = 0; i < numberBigRates; ++i)
        {
            marketSwaptionVols[i] = specifier.OriginalVariances[i].TotalVolatility(i);
        }

        double[] modelSwaptionVols = new double[numberBigRates];

        double periodSwaptionRmsError;
        double totalSwaptionError;
        double errorImprovement;
        ImmutableArray<Matrix> swapCovariancePseudoRoots;

        // the passes run, where QuantLib's count stops one short when the loop ends on its errors (MR3 (xi))
        int iterationsDone = 0;

        double previousError = 1.0e+10; // very large number

        var modelSwaptionVolsMatrix = new Matrix(maxPeriodIterations, numberBigRates, 0.0);
        double[] smallDisplacements = [.. Enumerable.Repeat(displacement, numberSmallRates)];

        do
        {
            VolatilityInterpolationSpecifier scaled = specifier.WithScalingFactors(scalingFactors);

            var unperiodicCalibrator = new CTSMMCapletMaxHomogeneityCalibration(
                evolution, corr, scaled.InterpolatedVariances.AsSpan(), capletVols, cs, displacement, caplet0Swaption1Priority);

            // the inner calibration's failures, where QuantLib returns its bool (MR3 (xi))
            CTSMMCapletCalibrationResults results = unperiodicCalibrator.Calibrate(
                numberOfFactors, maxUnperiodicIterations, toleranceUnperiodic, max1dIterations, tolerance1d);
            failures = results.Failures;

            swapCovariancePseudoRoots = results.SwapPseudoRoots();

            var smm = new PseudoRootFacade(swapCovariancePseudoRoots, evolution.RateTimes.AsSpan(), cs.CoterminalSwapRates(), smallDisplacements);

            var flmm = new CotSwapToFwdAdapter(smm);

            var periodflmm = new FwdPeriodAdapter(flmm, period, offset, newDisplacements);

            var periodsmm = new FwdToCotSwapAdapter(periodflmm);

            Matrix swaptionTotCovariance = periodsmm.TotalCovariance(periodsmm.NumberOfSteps - 1);

            totalSwaptionError = 0.0;

            ImmutableArray<double> periodRateTimes = periodsmm.Evolution.RateTimes;
            for (int i = 0; i < numberBigRates; ++i)
            {
                modelSwaptionVols[i] = Math.Sqrt(swaptionTotCovariance[i, i] / periodRateTimes[i]);
                double scale = marketSwaptionVols[i] / modelSwaptionVols[i];
                scalingFactors[i] *= scale; // since applied to vol

                totalSwaptionError += (marketSwaptionVols[i] - modelSwaptionVols[i]) * (marketSwaptionVols[i] - modelSwaptionVols[i]);
            }

            for (int i = 0; i < numberBigRates; ++i)
            {
                modelSwaptionVolsMatrix[iterationsDone, i] = modelSwaptionVols[i];
            }

            periodSwaptionRmsError = Math.Sqrt(totalSwaptionError / numberBigRates);
            errorImprovement = previousError - periodSwaptionRmsError;
            previousError = periodSwaptionRmsError;
            ++iterationsDone;
        }
        while (errorImprovement > periodTolerance / 10.0 && periodSwaptionRmsError > periodTolerance && iterationsDone < maxPeriodIterations);

        return new CapletSwaptionPeriodicCalibrationResults(
            failures,
            totalSwaptionError,
            swapCovariancePseudoRoots,
            ImmutableArray.Create(scalingFactors),
            iterationsDone,
            errorImprovement,
            modelSwaptionVolsMatrix);
    }
}
