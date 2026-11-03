// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/capletcoterminalswaptioncalibration.hpp ql/models/marketmodels/models/capletcoterminalswaptioncalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The original coterminal swap market model caplet calibration: the swap variances made time-inhomogeneous by
/// 1/(1 + α s)² and rescaled to their totals, then each swap rate's variance before the previous rate's reset scaled by
/// a² and after it by b², a and b solving the caplet approximation's quadratic.
/// </summary>
/// <remarks>
/// Immutable. <see cref="CalibrationFunction"/> returns its pass's pseudo-roots and failures in place of QuantLib's
/// out-parameter; α shorter than the rates, which QuantLib's function reads unchecked, is refused.
/// </remarks>
public sealed class CTSMMCapletOriginalCalibration : CTSMMCapletCalibration
{
    private readonly ImmutableArray<double> alpha;
    private readonly bool lowestRoot;
    private readonly bool useFullApprox;

    /// <summary>Initializes a new instance of the <see cref="CTSMMCapletOriginalCalibration"/> class.</summary>
    /// <param name="evolution">The evolution: its times the correlation's and the rate times but the last.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance, one per rate.</param>
    /// <param name="mktCapletVols">The market's caplet volatilities, the last the last swaption's.</param>
    /// <param name="cs">The curve state; copied.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="alpha">Each rate's α, one per rate.</param>
    /// <param name="lowestRoot">Whether the quadratic's lowest root is taken.</param>
    /// <param name="useFullApprox">Whether the approximation's terms from later rates are kept.</param>
    /// <exception cref="ArgumentException">The inputs don't fit, or α isn't one per rate.</exception>
    public CTSMMCapletOriginalCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> mktCapletVols,
        CurveState cs,
        double displacement,
        ReadOnlySpan<double> alpha,
        bool lowestRoot,
        bool useFullApprox)
        : base(evolution, corr, displacedSwapVariances, mktCapletVols, cs, displacement)
    {
        CheckAlpha(NumberOfRates, alpha);
        this.alpha = [.. alpha];
        this.lowestRoot = lowestRoot;
        this.useFullApprox = useFullApprox;
    }

    /// <summary>One pass of the original calibration.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance.</param>
    /// <param name="capletVols">The caplet volatilities to fit.</param>
    /// <param name="cs">The curve state.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="alpha">Each rate's α.</param>
    /// <param name="lowestRoot">Whether the quadratic's lowest root is taken.</param>
    /// <param name="useFullApprox">Whether the approximation's terms from later rates are kept.</param>
    /// <param name="numberOfFactors">The factors, from 1 to the rates.</param>
    /// <returns>The swap rates' pseudo-roots and the failures, a quadratic with no root or a variance left below
    /// 0.</returns>
    /// <exception cref="ArgumentException">The inputs don't fit, α isn't one per rate, or the factors aren't from 1 to
    /// the rates.</exception>
    /// <exception cref="NanoQLException">A root is negative.</exception>
    public static CTSMMCapletCalibrationStep CalibrationFunction(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> capletVols,
        CurveState cs,
        double displacement,
        ReadOnlySpan<double> alpha,
        bool lowestRoot,
        bool useFullApprox,
        int numberOfFactors)
    {
        PerformChecks(evolution, corr, displacedSwapVariances, capletVols, cs);

        int numberOfSteps = evolution.NumberOfSteps;
        int numberOfRates = evolution.NumberOfRates;
        ImmutableArray<double> rateTimes = evolution.RateTimes;

        CheckFactors(numberOfFactors, numberOfRates);

        // QuantLib's function reads α unchecked; its constructor checks it
        CheckAlpha(numberOfRates, alpha);

        int failures = 0;
        double extraMultiplier = useFullApprox ? 1.0 : 0.0;

        // factor reduction
        var corrPseudo = new Matrix[corr.Times.Length];
        for (int i = 0; i < corrPseudo.Length; ++i)
        {
            corrPseudo[i] = PseudoSqrtFunctions.RankReducedSqrt(corr.Correlation(i), numberOfFactors, 1.0, SalvagingAlgorithmType.None);
        }

        Matrix zedMatrix = SwapForwardMappings.CoterminalSwapZedMatrix(cs, displacement);
        Matrix invertedZedMatrix = MatrixFunctions.Inverse(zedMatrix);

        // do alpha part
        // first modify variances to take account of alpha
        // then rescale so total variance is unchanged
        var swapTimeInhomogeneousVariances = new Matrix(numberOfSteps, numberOfRates, 0.0);
        double[] originalVariances = new double[numberOfRates];
        double[] modifiedVariances = new double[numberOfRates];
        ImmutableArray<double> evolutionTimes = evolution.EvolutionTimes;
        for (int i = 0; i < numberOfSteps; ++i)
        {
            double s = i == 0 ? 0.0 : evolutionTimes[i - 1];
            for (int j = i; j < numberOfRates; ++j)
            {
                ImmutableArray<double> var = displacedSwapVariances[j].Variances;
                originalVariances[j] += var[i];
                swapTimeInhomogeneousVariances[i, j] = var[i] / ((1.0 + (alpha[j] * s)) * (1.0 + (alpha[j] * s)));
                modifiedVariances[j] += swapTimeInhomogeneousVariances[i, j];
            }
        }

        for (int i = 0; i < numberOfSteps; ++i)
        {
            for (int j = i; j < numberOfRates; ++j)
            {
                swapTimeInhomogeneousVariances[i, j] *= originalVariances[j] / modifiedVariances[j];
            }
        }

        // compute swap covariances for caplet approximation formula
        // without taking into account A and B
        var covarianceSwapPseudos = new Matrix[numberOfSteps];
        var covarianceSwapCovs = new Matrix[numberOfSteps]; // this is total cov
        var covarianceSwapMarginalCovs = new Matrix[numberOfSteps]; // this is cov for one step

        for (int i = 0; i < numberOfSteps; ++i)
        {
            covarianceSwapPseudos[i] = corrPseudo[i].Clone();
            for (int j = 0; j < numberOfRates; ++j)
            {
                for (int k = 0; k < covarianceSwapPseudos[i].Columns; ++k)
                {
                    covarianceSwapPseudos[i][j, k] *= Math.Sqrt(swapTimeInhomogeneousVariances[i, j]);
                }
            }

            covarianceSwapMarginalCovs[i] = covarianceSwapPseudos[i] * MatrixFunctions.Transpose(covarianceSwapPseudos[i]);

            covarianceSwapCovs[i] = covarianceSwapMarginalCovs[i].Clone();
            if (i > 0)
            {
                covarianceSwapCovs[i] += covarianceSwapCovs[i - 1];
            }
        }

        // compute partial variances and covariances which will take A and B coefficients
        double[] totVariance = new double[numberOfRates];
        double[] almostTotVariance = new double[numberOfRates];
        double[] almostTotCovariance = new double[numberOfRates];
        double[] leftCovariance = new double[numberOfRates];
        for (int i = 0; i < numberOfRates; ++i)
        {
            for (int jj = 0; jj <= i; ++jj)
            {
                totVariance[i] += displacedSwapVariances[i].Variances[jj];
            }

            int j;
            for (j = 0; j <= i - 1; ++j)
            {
                almostTotVariance[i] += swapTimeInhomogeneousVariances[j, i];
            }

            for (j = 0; j <= i - 2; ++j)
            {
                Matrix thisPseudo = corrPseudo[j];
                double correlation = 0.0;
                for (int k = 0; k < numberOfFactors; ++k)
                {
                    correlation += thisPseudo[i - 1, k] * thisPseudo[i, k];
                }

                almostTotCovariance[i] += correlation
                    * Math.Sqrt(swapTimeInhomogeneousVariances[j, i] * swapTimeInhomogeneousVariances[j, i - 1]);
            }

            if (i > 0)
            {
                Matrix thisPseudo = corrPseudo[j];
                double correlation = 0.0;
                for (int k = 0; k < numberOfFactors; ++k)
                {
                    correlation += thisPseudo[i - 1, k] * thisPseudo[i, k];
                }

                leftCovariance[i] = correlation
                    * Math.Sqrt(swapTimeInhomogeneousVariances[j, i] * swapTimeInhomogeneousVariances[j, i - 1]);
            }
        }

        // multiplier up to rate reset previous time
        // the first element is not used
        double[] a = [.. Enumerable.Repeat(1.0, numberOfSteps)];

        // multiplier afterward
        double[] b = new double[numberOfSteps];

        b[0] = displacedSwapVariances[0].Variances[0] / swapTimeInhomogeneousVariances[0, 0];

        // main loop where work is done
        for (int i = 1; i < numberOfSteps; ++i)
        {
            int j = 0;

            // up date variances to take account of last a and b computed
            for (; j <= i - 2; j++)
            {
                swapTimeInhomogeneousVariances[j, i - 1] *= a[i - 1] * a[i - 1];
            }

            swapTimeInhomogeneousVariances[j, i - 1] *= b[i - 1] * b[i - 1];

            double w0 = invertedZedMatrix[i - 1, i - 1];
            double w1 = -invertedZedMatrix[i - 1, i];
            double v1t1 = capletVols[i - 1] * capletVols[i - 1] * rateTimes[i - 1];

            // now compute contribution from lower right corner
            double extraConstantPart = 0.0;

            // part of caplet approximation formula coming from later rates
            for (int k = i + 1; k < numberOfSteps; ++k)
            {
                for (int l = i + 1; l < numberOfSteps; ++l)
                {
                    extraConstantPart += invertedZedMatrix[i - 1, k] * covarianceSwapCovs[i - 1][k, l] * invertedZedMatrix[i - 1, l];
                }
            }

            // now compute contribution from top row excluding first two columns and its transpose
            // we divide into two parts as one part is multiplied by a[i-1] and the other by b[i-1]
            // a lot could be precomputed when we need to optimize
            for (int k = i + 1; k < numberOfSteps; ++k)
            {
                if (i > 1)
                {
                    extraConstantPart += invertedZedMatrix[i - 1, i - 1] * covarianceSwapCovs[i - 2][i - 1, k]
                        * invertedZedMatrix[i - 1, k] * a[i - 1];

                    extraConstantPart += invertedZedMatrix[i - 1, k] * covarianceSwapCovs[i - 2][k, i - 1]
                        * invertedZedMatrix[i - 1, i - 1] * a[i - 1];
                }

                extraConstantPart += invertedZedMatrix[i - 1, i - 1] * covarianceSwapMarginalCovs[i - 1][i - 1, k]
                    * invertedZedMatrix[i - 1, k] * b[i - 1];

                extraConstantPart += invertedZedMatrix[i - 1, k] * covarianceSwapCovs[i - 1][k, i - 1]
                    * invertedZedMatrix[i - 1, i - 1] * b[i - 1];
            }

            // we also get an extra linear part, this corresponds to row i, and columns j>i+1, and transpose
            double extraLinearPart = 0.0;
            for (int k = i + 1; k < numberOfSteps; ++k)
            {
                extraLinearPart += invertedZedMatrix[i - 1, k] * covarianceSwapCovs[i - 1][k, i] * invertedZedMatrix[i - 1, i];

                extraLinearPart += invertedZedMatrix[i - 1, i] * covarianceSwapCovs[i - 1][i, k] * invertedZedMatrix[i - 1, k];
            }

            double constantPart = (w0 * w0 * totVariance[i - 1]) + (extraConstantPart * extraMultiplier) - v1t1;
            double linearPart = (-2 * w0 * w1 * ((a[i - 1] * almostTotCovariance[i]) + (b[i - 1] * leftCovariance[i])))
                + (extraLinearPart * extraMultiplier);
            double quadraticPart = w1 * w1 * almostTotVariance[i];
            double disc = (linearPart * linearPart) - (4.0 * constantPart * quadraticPart);

            double root, minimum = -linearPart / (2.0 * quadraticPart);
            bool rightUsed = false;
            if (disc < 0.0)
            {
                ++failures;

                // pick up the minimum vol for the caplet
                root = minimum;
            }
            else if (lowestRoot)
            {
                root = (-linearPart - Math.Sqrt(disc)) / (2.0 * quadraticPart);
            }
            else
            {
                if (minimum > 1.0)
                {
                    root = (-linearPart - Math.Sqrt(disc)) / (2.0 * quadraticPart);
                }
                else
                {
                    rightUsed = true;
                    root = (-linearPart + Math.Sqrt(disc)) / (2.0 * quadraticPart);
                }
            }

            double varianceFound = root * root * almostTotVariance[i];
            double varianceToFind = totVariance[i] - varianceFound;
            double mult = varianceToFind / swapTimeInhomogeneousVariances[i, i];
            if (mult <= 0.0 && rightUsed)
            {
                root = (-linearPart - Math.Sqrt(disc)) / (2.0 * quadraticPart);
                varianceFound = root * root * almostTotVariance[i];
                varianceToFind = totVariance[i] - varianceFound;
                mult = varianceToFind / swapTimeInhomogeneousVariances[i, i];
            }

            if (mult < 0.0)
            {
                // no solution...
                ++failures;
                a[i] = root;
                b[i] = 0.0;
            }
            else
            {
                a[i] = root;
                b[i] = Math.Sqrt(mult);
            }

            if (!(root >= 0.0))
            {
                throw new NanoQLException("negative root -- it should have not happened");
            }
        }

        {
            int i = numberOfSteps;
            int j = 0;
            for (; j <= i - 2; j++)
            {
                swapTimeInhomogeneousVariances[j, i - 1] *= a[i - 1] * a[i - 1];
            }

            swapTimeInhomogeneousVariances[j, i - 1] *= b[i - 1] * b[i - 1];
        }

        // compute the results
        var swapCovariancePseudoRoots = new Matrix[numberOfSteps];
        for (int k = 0; k < numberOfSteps; ++k)
        {
            swapCovariancePseudoRoots[k] = corrPseudo[k].Clone();
            for (int j = 0; j < numberOfRates; ++j)
            {
                double coeff = Math.Sqrt(swapTimeInhomogeneousVariances[k, j]);
                for (int i = 0; i < numberOfFactors; ++i)
                {
                    swapCovariancePseudoRoots[k][j, i] *= coeff;
                }
            }

            CheckPseudoRoot(k, swapCovariancePseudoRoots[k], numberOfRates, numberOfFactors);
        }

        return new CTSMMCapletCalibrationStep(failures, swapCovariancePseudoRoots);
    }

    /// <inheritdoc/>
    protected override CTSMMCapletCalibrationStep CalibrationImpl(
        int numberOfFactors, int innerMaxIterations, double innerTolerance, ReadOnlySpan<double> usedCapletVols) =>
        CalibrationFunction(
            Evolution,
            Correlation,
            DisplacedSwapVariances.AsSpan(),

            // not mktCapletVols_ but...
            usedCapletVols,
            CurveState(),
            Displacement,
            alpha.AsSpan(),
            lowestRoot,
            useFullApprox,
            numberOfFactors);

    private static void CheckAlpha(int numberOfRates, ReadOnlySpan<double> alpha)
    {
        if (!(numberOfRates == alpha.Length))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"mismatch between number of rates ({numberOfRates}) and alpha ({alpha.Length})"),
                nameof(alpha));
        }
    }
}
