// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/capletcoterminalmaxhomogeneity.hpp ql/models/marketmodels/models/capletcoterminalmaxhomogeneity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The maximally homogeneous coterminal swap market model caplet calibration: each caplet fitted by moving the next
/// swap rate's volatilities to the point closest to its given ones on the sphere of its swaption variance and the
/// cylinder of the caplet's, the caplet or the swaption favoured where they don't meet.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. <see cref="CapletMaxHomogeneityCalibration"/> returns its pass's pseudo-roots, failures, deformation size
/// and swaption error in place of QuantLib's out-parameters.
/// </para>
/// <para>
/// One departure from QuantLib (<c>Divergences/CTSMMCapletMaxHomogeneityDeformationTests.cs</c>): the deformation size
/// sums each moved volatility's squared change, where QuantLib's loop adds the step index's own change once per
/// volatility (<c>capletcoterminalmaxhomogeneity.cpp:370-371</c>).
/// </para>
/// </remarks>
public sealed class CTSMMCapletMaxHomogeneityCalibration : CTSMMCapletCalibration
{
    private readonly double caplet0Swaption1Priority;

    /// <summary>Initializes a new instance of the <see cref="CTSMMCapletMaxHomogeneityCalibration"/> class.</summary>
    /// <param name="evolution">The evolution: its times the correlation's and the rate times but the last.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance, one per rate.</param>
    /// <param name="mktCapletVols">The market's caplet volatilities, the last the last swaption's.</param>
    /// <param name="cs">The curve state; copied.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="caplet0Swaption1Priority">
    /// Where the sphere and cylinder don't meet, the weight on the swaption: 0 fits the caplet, 1 the swaption.
    /// </param>
    /// <exception cref="ArgumentException">The inputs don't fit, or the priority isn't in [0, 1].</exception>
    public CTSMMCapletMaxHomogeneityCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> mktCapletVols,
        CurveState cs,
        double displacement,
        double caplet0Swaption1Priority = 1.0)
        : base(evolution, corr, displacedSwapVariances, mktCapletVols, cs, displacement)
    {
        if (!(caplet0Swaption1Priority >= 0.0 && caplet0Swaption1Priority <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"caplet0Swaption1Priority ({caplet0Swaption1Priority}) must be in [0.0, 1.0]"),
                nameof(caplet0Swaption1Priority));
        }

        this.caplet0Swaption1Priority = caplet0Swaption1Priority;
    }

    /// <summary>One pass of the maximally homogeneous calibration.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance.</param>
    /// <param name="capletVols">The caplet volatilities to fit.</param>
    /// <param name="cs">The curve state.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="caplet0Swaption1Priority">Where the sphere and cylinder don't meet, the weight on the
    /// swaption.</param>
    /// <param name="numberOfFactors">The factors, from 1 to the rates.</param>
    /// <param name="maxIterations">Each closest-point search's iterations; 0 projects instead.</param>
    /// <param name="tolerance">Each closest-point search's tolerance.</param>
    /// <returns>
    /// The swap rates' pseudo-roots, the failures (the caplets where the sphere and cylinder don't meet), the
    /// deformation size and the sum of the squared swaption errors.
    /// </returns>
    /// <exception cref="ArgumentException">The inputs don't fit, or the factors aren't from 1 to the rates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    public static CTSMMCapletCalibrationStep CapletMaxHomogeneityCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> capletVols,
        CurveState cs,
        double displacement,
        double caplet0Swaption1Priority,
        int numberOfFactors,
        int maxIterations,
        double tolerance)
    {
        PerformChecks(evolution, corr, displacedSwapVariances, capletVols, cs);

        int numberOfSteps = evolution.NumberOfSteps;
        int numberOfRates = evolution.NumberOfRates;
        ImmutableArray<double> rateTimes = evolution.RateTimes;

        CheckFactors(numberOfFactors, numberOfRates);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);

        int failures = 0;

        double totalSwaptionError = 0.0;
        double deformationSize = 0.0;

        // factor reduction
        var corrPseudo = new Matrix[corr.Times.Length];
        for (int i = 0; i < corrPseudo.Length; ++i)
        {
            corrPseudo[i] = PseudoSqrtFunctions.RankReducedSqrt(corr.Correlation(i), numberOfFactors, 1.0, SalvagingAlgorithmType.None);
        }

        // get Zinverse, we can get wj later
        Matrix zedMatrix = SwapForwardMappings.CoterminalSwapZedMatrix(cs, displacement);
        Matrix invertedZedMatrix = MatrixFunctions.Inverse(zedMatrix);

        // vectors for the new vol of all swap rates
        var newVols = new List<double[]>();
        double[] theseNewVols = new double[numberOfRates];
        double[] firstRateVols = new double[numberOfRates];
        firstRateVols[0] = Math.Sqrt(displacedSwapVariances[0].Variances[0]);
        double[] secondRateVols = new double[numberOfRates];
        double[] correlations = new double[numberOfRates];
        newVols.Add((double[])firstRateVols.Clone());

        // final caplet and swaption are the same, so we skip that case
        for (int i = 0; i < numberOfRates - 1; ++i)
        {
            // final weight dont do anything when i < 2
            double thisFinalWeight = i > 1 ? (i - 1) / 2.0 : 1.0;

            // we will calibrate caplet on forward rate i,
            // we will do this by modifying the vol of swap rate i+1
            ImmutableArray<double> var = displacedSwapVariances[i + 1].Variances;

            for (int j = 0; j < i + 2; ++j)
            {
                secondRateVols[j] = Math.Sqrt(var[j]);
            }

            for (int k = 0; k < i + 1; k++)
            {
                double correlation = 0.0;
                for (int l = 0; l < numberOfFactors; ++l)
                {
                    double term1 = corrPseudo[k][i, l];
                    double term2 = corrPseudo[k][i + 1, l];
                    correlation += term1 * term2;
                }

                correlations[k] = correlation;
            }

            double w0 = invertedZedMatrix[i, i];
            double w1 = invertedZedMatrix[i, i + 1];

            // w0 adjustment
            for (int k = i + 2; k < invertedZedMatrix.Columns; ++k)
            {
                w0 += invertedZedMatrix[i, k];
            }

            double targetCapletVariance = capletVols[i] * capletVols[i] * rateTimes[i];

            bool success = SingleRateClosestPointFinder(
                i,
                secondRateVols,
                firstRateVols,
                targetCapletVariance,
                correlations,
                w0,
                w1,
                caplet0Swaption1Priority,
                maxIterations,
                tolerance,
                theseNewVols,
                thisFinalWeight,
                out double thisSwaptionError,
                out _);

            totalSwaptionError += thisSwaptionError * thisSwaptionError;

            if (!success)
            {
                ++failures;
            }

            // each moved volatility's change, where QuantLib adds the i-th's i + 2 times
            for (int j = 0; j < i + 2; ++j)
            {
                deformationSize += (theseNewVols[j] - secondRateVols[j]) * (theseNewVols[j] - secondRateVols[j]);
            }

            newVols.Add((double[])theseNewVols.Clone());
            firstRateVols = (double[])theseNewVols.Clone();
        }

        var swapCovariancePseudoRoots = new Matrix[numberOfSteps];
        for (int k = 0; k < numberOfSteps; ++k)
        {
            swapCovariancePseudoRoots[k] = corrPseudo[k].Clone();
            for (int j = 0; j < numberOfRates; ++j)
            {
                double coeff = newVols[j][k];
                for (int i = 0; i < numberOfFactors; ++i)
                {
                    swapCovariancePseudoRoots[k][j, i] *= coeff;
                }
            }

            CheckPseudoRoot(k, swapCovariancePseudoRoots[k], numberOfRates, numberOfFactors);
        }

        return new CTSMMCapletCalibrationStep(
            failures, swapCovariancePseudoRoots, deformationSize: deformationSize, totalSwaptionError: totalSwaptionError);
    }

    /// <inheritdoc/>
    protected override CTSMMCapletCalibrationStep CalibrationImpl(
        int numberOfFactors, int innerMaxIterations, double innerTolerance, ReadOnlySpan<double> usedCapletVols) =>
        CapletMaxHomogeneityCalibration(
            Evolution,
            Correlation,
            DisplacedSwapVariances.AsSpan(),

            // not mktCapletVols_ but...
            usedCapletVols,
            CurveState(),
            Displacement,
            caplet0Swaption1Priority,
            numberOfFactors,
            innerMaxIterations,
            innerTolerance);

    private static bool SingleRateClosestPointFinder(
        int capletNumber,
        ReadOnlySpan<double> homogeneousSolution,
        ReadOnlySpan<double> previousRateSolution,
        double capletVariance,
        ReadOnlySpan<double> correlations,
        double w0,
        double w1,
        double capletSwaptionPriority,
        int maxIterations,
        double tolerance,
        Span<double> solution,
        double finalWeight,
        out double swaptionError,
        out double capletError)
    {
        if (capletNumber == 0)
        {
            // there only is one point so to go through everything would be silly
            double previousSwapVariance0 = previousRateSolution[0] * previousRateSolution[0];
            double thisSwapVariance0 = (homogeneousSolution[0] * homogeneousSolution[0]) + (homogeneousSolution[1] * homogeneousSolution[1]);
            double crossTerm = 2 * w0 * w1 * correlations[0] * previousRateSolution[0];
            double constantTerm = (w0 * w0 * previousSwapVariance0) - capletVariance;
            double theta0 = w1 * w1;

            var q = new Quadratic(theta0, crossTerm, constantTerm);
            bool capSuccess = q.Roots(out double volminus, out _);
            double residual = thisSwapVariance0 - (volminus * volminus);
            bool swapSuccess = residual >= 0;
            bool success0 = capSuccess && swapSuccess;

            if (success0)
            {
                solution[0] = volminus;
                solution[1] = Math.Sqrt(residual);
                swaptionError = 0.0;
                capletError = 0.0;
                return success0;
            }

            bool prioritizeCaplet = capletSwaptionPriority < 0.5;

            if (capSuccess && prioritizeCaplet)
            {
                solution[0] = volminus;
                solution[1] = 0; // residual is negative or we'd have totally succeeded
                swaptionError = Math.Sqrt(thisSwapVariance0) - volminus;
                capletError = 0.0;
                return success0;
            }

            if (capSuccess && !prioritizeCaplet)
            {
                solution[0] = Math.Sqrt(thisSwapVariance0);
                solution[1] = 0.0;
                swaptionError = 0.0;
                capletError = Math.Sqrt(q.Evaluate(solution[0]) + capletVariance) - Math.Sqrt(capletVariance);
                return success0;
            }

            // ok caplets have failed
            if (swapSuccess)
            {
                solution[0] = volminus;
                solution[1] = Math.Sqrt(residual);
                swaptionError = 0.0;
                capletError = Math.Sqrt(q.Evaluate(solution[0]) + capletVariance) - Math.Sqrt(capletVariance);
                return success0;
            }

            // ok caplets have failed and swaps fail with optimal caplet solution
            if (prioritizeCaplet)
            {
                solution[0] = volminus;
                solution[1] = 0; // residual is negative or we'd have totally succeeded
                swaptionError = Math.Sqrt(thisSwapVariance0) - volminus;
                capletError = 0.0;
            }
            else
            {
                solution[0] = Math.Sqrt(thisSwapVariance0);
                solution[1] = 0.0;
                swaptionError = 0.0;
                capletError = Math.Sqrt(q.Evaluate(solution[0]) + capletVariance) - Math.Sqrt(capletVariance);
            }

            return success0;
        }

        // first get in correct format
        double previousSwapVariance = 0.0;
        double thisSwapVariance = 0.0;
        {
            int i = 0;
            for (; i < capletNumber + 1; ++i)
            {
                previousSwapVariance += previousRateSolution[i] * previousRateSolution[i];
                thisSwapVariance += homogeneousSolution[i] * homogeneousSolution[i];
            }

            thisSwapVariance += homogeneousSolution[i] * homogeneousSolution[i];
        }

        double theta = w1 * w1;
        double[] b = new double[capletNumber + 1];
        var cylinderCentre = new Vector(capletNumber + 1);
        var targetArray = new Vector(capletNumber + 2);
        var targetArrayRestricted = new Vector(capletNumber + 1);

        double bsq = 0.0;
        for (int i = 0; i < capletNumber + 1; ++i)
        {
            b[i] = 2 * w0 * w1 * correlations[i] * previousRateSolution[i] / theta;
            cylinderCentre[i] = -0.5 * b[i];
            targetArray[i] = homogeneousSolution[i];
            targetArrayRestricted[i] = targetArray[i];
            bsq += b[i] * b[i];
        }

        targetArray[capletNumber + 1] = homogeneousSolution[capletNumber + 1];

        double a = previousSwapVariance * w0 * w0 / theta;
        double constQuadraticTerm = a - (0.25 * bsq);
        double s2 = (capletVariance / theta) - constQuadraticTerm;

        // if S2 < 0, there are no solutions so we take the best we can.
        double s = s2 > 0 ? Math.Sqrt(s2) : 0;

        double r = Math.Sqrt(thisSwapVariance);

        var basis = new BasisIncompleteOrdered(capletNumber + 1);
        basis.AddVector(cylinderCentre.AsSpan());
        basis.AddVector(targetArrayRestricted.AsSpan());
        for (int i = 0; i < capletNumber + 1; ++i)
        {
            var ei = new Vector(capletNumber + 1, 0.0);
            ei[i] = 1.0;
            basis.AddVector(ei.AsSpan());
        }

        Matrix orthTransformationRestricted = basis.GetBasisAsRowsInMatrix();
        var orthTransformation = new Matrix(capletNumber + 2, capletNumber + 2, 0.0);

        orthTransformation[capletNumber + 1, capletNumber + 1] = 1.0;
        for (int k = 0; k < capletNumber + 1; ++k)
        {
            for (int l = 0; l < capletNumber + 1; ++l)
            {
                orthTransformation[k, l] = orthTransformationRestricted[k, l];
            }
        }

        Vector movedCentre = orthTransformationRestricted * cylinderCentre;
        double alpha = movedCentre[0];
        Vector movedTarget = orthTransformation * targetArray;

        double z1 = 0.0, z2 = 0.0, z3 = 0.0;

        var optimizer = new SphereCylinderOptimizer(r, s, alpha, movedTarget[0], movedTarget[1], movedTarget[movedTarget.Size - 1], finalWeight);

        bool success = false;

        if (!optimizer.IsIntersectionNonEmpty())
        {
            z1 = (r * capletSwaptionPriority) + ((1 - capletSwaptionPriority) * (alpha - s));
            z2 = 0.0;
            z3 = 0.0;
            swaptionError = z1 - r;
            capletError = alpha - s - z1;
        }
        else
        {
            success = true;
            capletError = 0.0;
            swaptionError = 0.0;

            if (maxIterations > 0.0)
            {
                optimizer.FindClosest(maxIterations, tolerance, out z1, out z2, out z3);
            }
            else
            {
                optimizer.FindByProjection(out z1, out z2, out z3);
            }
        }

        var rotatedSolution = new Vector(capletNumber + 2, 0.0);
        rotatedSolution[0] = z1;
        rotatedSolution[1] = z2;
        rotatedSolution[capletNumber + 1] = z3;

        Vector arraySolution = MatrixFunctions.Transpose(orthTransformation) * rotatedSolution;
        {
            int i = 0;
            for (; i < arraySolution.Size; ++i)
            {
                solution[i] = arraySolution[i];
            }

            for (; i < solution.Length; ++i)
            {
                solution[i] = 0.0;
            }
        }

        return success;
    }
}
