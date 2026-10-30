// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/capletcoterminalalphacalibration.hpp ql/models/marketmodels/models/capletcoterminalalphacalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The alpha-form coterminal swap market model caplet calibration: each caplet fitted by scaling the next swap rate's
/// volatilities before the caplet's reset by a times the alpha form at an α the solver finds, and after it by b, its
/// swaption variance kept.
/// </summary>
/// <remarks>
/// <para>
/// Immutable (the S-mm plan's MR2 (a), (b)): it holds a copy of the alpha form given, and each pass solves on a copy of
/// that, so a <see cref="CTSMMCapletCalibration.Calibrate"/> leaves the next one's form as it was.
/// <see cref="CapletAlphaFormCalibration"/> returns its pass's pseudo-roots and α, a and b in place of QuantLib's
/// out-parameters, and solves on a copy of the form given.
/// </para>
/// <para>
/// As in QuantLib, a caplet no α fits throws ("alpha form failure"), where the other calibrations count a failure; and,
/// as in QuantLib's master after 1.44 (#2910, the upstream sweep's SWR2 (a), which re-rules MR3 (viii)), so does a step
/// whose final part fails, which QuantLib 1.44's solve returns as solved with the swap rate's last volatility left as
/// it was, its swaptions missed (<c>Divergences/CTSMMCapletAlphaFormCalibrationTests.cs</c>). Two departures from
/// master: the calibration refuses such a step in the pass it returns, and counts it in an earlier pass, whose
/// successor re-targets the caplets and can fit, where master throws in any pass (the sweep's ruling SWI4 (b)), so a
/// calibration whose returned pass fits keeps QuantLib 1.44's numbers; and at maximum homogeneity, where the fixed
/// minimization (MR3 (x)) leaves a caplet no α, the pass runs again on QuantLib 1.44's test of the objective's value
/// before it throws (the user's ruling of 2026-10-27), where master throws. <see cref="CapletAlphaFormCalibration"/>,
/// one pass alone, refuses a failed final part as master does. α bounds shorter than the rates, which QuantLib's
/// function reads unchecked, are refused (MR3 (xx)).
/// </para>
/// </remarks>
public sealed class CTSMMCapletAlphaFormCalibration : CTSMMCapletCalibration
{
    private readonly ImmutableArray<double> alphaInitial;
    private readonly ImmutableArray<double> alphaMax;
    private readonly ImmutableArray<double> alphaMin;
    private readonly bool maximizeHomogeneity;
    private readonly AlphaForm parametricForm;

    /// <summary>Initializes a new instance of the <see cref="CTSMMCapletAlphaFormCalibration"/> class.</summary>
    /// <param name="evolution">The evolution: its times the correlation's and the rate times but the last.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance, one per rate.</param>
    /// <param name="mktCapletVols">The market's caplet volatilities, the last the last swaption's.</param>
    /// <param name="cs">The curve state; copied.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="alphaInitial">Each rate's initial α, one per rate.</param>
    /// <param name="alphaMax">Each rate's highest α, one per rate.</param>
    /// <param name="alphaMin">Each rate's lowest α, one per rate.</param>
    /// <param name="maximizeHomogeneity">Whether the α nearest homogeneity is sought, rather than the first that
    /// fits.</param>
    /// <param name="parametricForm">The alpha form, copied; null for <see cref="AlphaFormLinearHyperbolic"/> on the
    /// rate times.</param>
    /// <exception cref="ArgumentException">The inputs don't fit, or an α vector isn't one per rate.</exception>
    public CTSMMCapletAlphaFormCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> mktCapletVols,
        CurveState cs,
        double displacement,
        ReadOnlySpan<double> alphaInitial,
        ReadOnlySpan<double> alphaMax,
        ReadOnlySpan<double> alphaMin,
        bool maximizeHomogeneity,
        AlphaForm? parametricForm = null)
        : base(evolution, corr, displacedSwapVariances, mktCapletVols, cs, displacement)
    {
        this.parametricForm = parametricForm?.Clone() ?? new AlphaFormLinearHyperbolic(evolution.RateTimes.AsSpan());
        CheckAlphas(NumberOfRates, alphaInitial, alphaMax, alphaMin);
        this.alphaInitial = [.. alphaInitial];
        this.alphaMax = [.. alphaMax];
        this.alphaMin = [.. alphaMin];
        this.maximizeHomogeneity = maximizeHomogeneity;
    }

    /// <summary>One pass of the alpha-form calibration.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance.</param>
    /// <param name="capletVols">The caplet volatilities to fit.</param>
    /// <param name="cs">The curve state.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <param name="alphaInitial">Each rate's initial α.</param>
    /// <param name="alphaMax">Each rate's highest α.</param>
    /// <param name="alphaMin">Each rate's lowest α.</param>
    /// <param name="maximizeHomogeneity">Whether the α nearest homogeneity is sought, rather than the first that
    /// fits.</param>
    /// <param name="parametricForm">The alpha form; the solves run on a copy.</param>
    /// <param name="numberOfFactors">The factors, from 1 to the rates.</param>
    /// <param name="steps">The solver's grid points.</param>
    /// <param name="toleranceForAlphaSolving">The solver's tolerance.</param>
    /// <returns>
    /// The swap rates' pseudo-roots, each rate's α, a and b, and the failures, 0 as in QuantLib, which throws where a
    /// step fails.
    /// </returns>
    /// <exception cref="ArgumentException">The inputs don't fit, an α vector isn't one per rate, or the factors aren't
    /// from 1 to the rates.</exception>
    /// <exception cref="NanoQLException">No α fits a caplet, or a step's final part fails at its α.</exception>
    public static CTSMMCapletCalibrationStep CapletAlphaFormCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> capletVols,
        CurveState cs,
        double displacement,
        ReadOnlySpan<double> alphaInitial,
        ReadOnlySpan<double> alphaMax,
        ReadOnlySpan<double> alphaMin,
        bool maximizeHomogeneity,
        AlphaForm parametricForm,
        int numberOfFactors,
        int steps,
        double toleranceForAlphaSolving) =>
        Pass(
            evolution,
            corr,
            displacedSwapVariances,
            capletVols,
            cs,
            displacement,
            alphaInitial,
            alphaMax,
            alphaMin,
            maximizeHomogeneity,
            parametricForm,
            numberOfFactors,
            steps,
            toleranceForAlphaSolving,
            false);

    /// <inheritdoc/>
    protected override CTSMMCapletCalibrationStep CalibrationImpl(
        int numberOfFactors, int innerMaxIterations, double innerTolerance, ReadOnlySpan<double> usedCapletVols) =>
        Pass(
            Evolution,
            Correlation,
            DisplacedSwapVariances.AsSpan(),

            // not mktCapletVols_ but...
            usedCapletVols,
            CurveState(),
            Displacement,
            alphaInitial.AsSpan(),
            alphaMax.AsSpan(),
            alphaMin.AsSpan(),
            maximizeHomogeneity,
            parametricForm,
            numberOfFactors,
            innerMaxIterations,
            innerTolerance,
            true);

    // The calibration's pass counts a step whose final part fails, and refuses it in the pass it returns
    // (CheckReturnedStep, SWI4 (b)); the pass alone refuses it at once, as QuantLib's master does.
    private protected override void CheckReturnedStep(CTSMMCapletCalibrationStep step)
    {
        if (step.Failures > 0)
        {
            throw new NanoQLException("alpha form failure");
        }
    }

    private static CTSMMCapletCalibrationStep Pass(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> capletVols,
        CurveState cs,
        double displacement,
        ReadOnlySpan<double> alphaInitial,
        ReadOnlySpan<double> alphaMax,
        ReadOnlySpan<double> alphaMin,
        bool maximizeHomogeneity,
        AlphaForm parametricForm,
        int numberOfFactors,
        int steps,
        double toleranceForAlphaSolving,
        bool countFailedFinalParts)
    {
        ArgumentNullException.ThrowIfNull(parametricForm);
        PerformChecks(evolution, corr, displacedSwapVariances, capletVols, cs);

        int numberOfSteps = evolution.NumberOfSteps;
        int numberOfRates = evolution.NumberOfRates;
        ImmutableArray<double> rateTimes = evolution.RateTimes;

        CheckFactors(numberOfFactors, numberOfRates);

        // QuantLib's function reads the α vectors unchecked; its constructor checks them (MR3 (xx))
        CheckAlphas(numberOfRates, alphaInitial, alphaMax, alphaMin);

        int failures = 0;

        double[] alpha = new double[numberOfRates];
        double[] a = new double[numberOfRates];
        double[] b = new double[numberOfRates];

        // factor reduction
        var corrPseudo = new Matrix[corr.Times.Length];
        for (int i = 0; i < corrPseudo.Length; ++i)
        {
            corrPseudo[i] = PseudoSqrtFunctions.RankReducedSqrt(corr.Correlation(i), numberOfFactors, 1.0, SalvagingAlgorithmType.None);
        }

        // get Zinverse, we can get wj later
        Matrix zedMatrix = SwapForwardMappings.CoterminalSwapZedMatrix(cs, displacement);
        Matrix invertedZedMatrix = MatrixFunctions.Inverse(zedMatrix);

        List<double[]> newVols;
        for (int attempt = 0; ; ++attempt)
        {
            // vectors for new vol
            newVols = [];
            double[] theseNewVols = new double[numberOfRates];
            double[] firstRateVols = new double[numberOfRates];
            firstRateVols[0] = Math.Sqrt(displacedSwapVariances[0].Variances[0]);
            double[] secondRateVols = new double[numberOfRates];
            double[] correlations = new double[numberOfRates];
            newVols.Add((double[])firstRateVols.Clone());

            alpha[0] = alphaInitial[0]; // has no effect on anything in any case
            a[0] = b[0] = 1.0; // no modifications to swap vol for first rate

            // the form's α is the solver's to set: it solves on a copy (MR2 (b)); a second attempt minimizes on
            // QuantLib's test of the objective's value
            var solver = new AlphaFinder(parametricForm.Clone(), attempt == 1);
            failures = 0;
            bool passed = true;

            // final caplet and swaption are the same, so we skip that case
            for (int i = 0; i < numberOfRates - 1; ++i)
            {
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

                double targetVariance = capletVols[i] * capletVols[i] * rateTimes[i];

                bool success;
                if (maximizeHomogeneity)
                {
                    success = solver.SolveWithMaxHomogeneity(
                        alphaInitial[i + 1],
                        i,
                        firstRateVols,
                        secondRateVols,
                        correlations,
                        w0,
                        w1,
                        targetVariance,
                        toleranceForAlphaSolving,
                        alphaMax[i + 1],
                        alphaMin[i + 1],
                        steps,
                        out alpha[i + 1],
                        out a[i + 1],
                        out b[i + 1],
                        theseNewVols);
                }
                else
                {
                    success = solver.Solve(
                        alphaInitial[i + 1],
                        i,
                        firstRateVols,
                        secondRateVols,
                        correlations,
                        w0,
                        w1,
                        targetVariance,
                        toleranceForAlphaSolving,
                        alphaMax[i + 1],
                        alphaMin[i + 1],
                        steps,
                        out alpha[i + 1],
                        out a[i + 1],
                        out b[i + 1],
                        theseNewVols);
                }

                // a caplet no α fits: QuantLib's refusal, at maximum homogeneity after the pass again on 1.44's test
                // (MR3 (x))
                if (!success && double.IsNaN(alpha[i + 1]))
                {
                    if (!(maximizeHomogeneity && attempt == 0))
                    {
                        throw new NanoQLException("alpha form failure");
                    }

                    passed = false;
                    break;
                }

                // a step whose final part fails, its α found: QuantLib's master's refusal (#2910; SWR2 (a)), where
                // its failure count is commented out beside its throw (capletcoterminalalphacalibration.cpp:202-203);
                // a calibration's pass counts it, refused where the calibration returns this pass (SWI4 (b))
                if (!success)
                {
                    if (!countFailedFinalParts)
                    {
                        throw new NanoQLException("alpha form failure");
                    }

                    ++failures;
                }

                newVols.Add((double[])theseNewVols.Clone());
                firstRateVols = (double[])theseNewVols.Clone();
            }

            // the fixed minimization's α at one caplet can leave a later one none where QuantLib's first trial leaves
            // room: the pass again on QuantLib's test (MR3 (x) as the user ruled it on 2026-10-27), unless this one
            // passed
            if (passed)
            {
                break;
            }
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
            failures,
            swapCovariancePseudoRoots,
            alpha: ImmutableArray.Create(alpha),
            a: ImmutableArray.Create(a),
            b: ImmutableArray.Create(b));
    }

    private static void CheckAlphas(
        int numberOfRates, ReadOnlySpan<double> alphaInitial, ReadOnlySpan<double> alphaMax, ReadOnlySpan<double> alphaMin)
    {
        CheckAlpha(numberOfRates, alphaInitial.Length, "alphaInitial", nameof(alphaInitial));
        CheckAlpha(numberOfRates, alphaMax.Length, "alphaMax", nameof(alphaMax));
        CheckAlpha(numberOfRates, alphaMin.Length, "alphaMin", nameof(alphaMin));
    }

    private static void CheckAlpha(int numberOfRates, int length, string name, string paramName)
    {
        if (!(numberOfRates == length))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"mismatch between number of rates ({numberOfRates}) and {name} ({length})"),
                paramName);
        }
    }
}
