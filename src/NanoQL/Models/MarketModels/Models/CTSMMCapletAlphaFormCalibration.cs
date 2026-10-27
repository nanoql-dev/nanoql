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
/// As in QuantLib, a caplet no α fits throws ("alpha form failure"), where the other calibrations count a failure.
/// Two departures (<c>Divergences/CTSMMCapletAlphaFormCalibrationTests.cs</c>): a step whose final part fails, which
/// QuantLib's solve returns as solved with the swap rate's last volatility left as it was, counts as a failure, so the
/// calibration's <c>Converged</c> reads false where QuantLib's returns true beside its swaption misfit (MR3 (viii), as
/// the user ruled it on 2026-10-27); and at maximum homogeneity, where the fixed minimization (MR3 (x)) leaves a caplet
/// no α, the pass runs again on QuantLib's test of the objective's value, as QuantLib does, before it throws (the
/// user's ruling of the same day). α bounds shorter than the rates, which QuantLib's function reads unchecked, are
/// refused (MR3 (xx)).
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
    /// The swap rates' pseudo-roots, each rate's α, a and b (b NaN where the step's final part fails), and the
    /// failures: the steps whose final part fails.
    /// </returns>
    /// <exception cref="ArgumentException">The inputs don't fit, an α vector isn't one per rate, or the factors aren't
    /// from 1 to the rates.</exception>
    /// <exception cref="NanoQLException">No α fits a caplet.</exception>
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
        double toleranceForAlphaSolving)
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

                if (!success)
                {
                    passed = false;
                    break;
                }

                // a step whose final part fails, which QuantLib's solve returns as solved: counted, where QuantLib's
                // count is commented out beside its throw (capletcoterminalalphacalibration.cpp:202-203; MR3 (viii) as
                // the user ruled it on 2026-10-27)
                if (double.IsNaN(b[i + 1]))
                {
                    ++failures;
                }

                newVols.Add((double[])theseNewVols.Clone());
                firstRateVols = (double[])theseNewVols.Clone();
            }

            if (passed)
            {
                break;
            }

            // the fixed minimization's α at one caplet can leave a later one none where QuantLib's first trial leaves
            // room: the pass again on QuantLib's test (MR3 (x) as the user ruled it on 2026-10-27)
            if (!(maximizeHomogeneity && attempt == 0))
            {
                throw new NanoQLException("alpha form failure");
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

    /// <inheritdoc/>
    protected override CTSMMCapletCalibrationStep CalibrationImpl(
        int numberOfFactors, int innerMaxIterations, double innerTolerance, ReadOnlySpan<double> usedCapletVols) =>
        CapletAlphaFormCalibration(
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
            innerTolerance);

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
