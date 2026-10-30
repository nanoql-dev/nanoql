// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/ctsmmcapletcalibration.hpp ql/models/marketmodels/models/ctsmmcapletcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>
/// A coterminal swap market model's calibration to caplet volatilities, the swaption volatilities given: each pass
/// fits the swap rates' pseudo-roots to the caplet volatilities in use, then rescales those by the market's over the
/// model's, until the caplets' root-mean-square error is within the tolerance or the passes run out.
/// </summary>
/// <remarks>
/// <para>
/// Immutable (the S-mm plan's MR2 (a)): it holds the inputs, a copy of the curve state given, and
/// <see cref="Calibrate"/> returns the results, where QuantLib's calibration changes itself; QuantLib's "not
/// successfully calibrated yet" has no state to guard. Each pass's fit is the derived calibration's
/// <see cref="CalibrationImpl"/>, QuantLib's <c>calibrationImpl_</c>. An extension point, as in QuantLib.
/// </para>
/// <para>
/// A count below zero, which QuantLib's unsigned <c>Natural</c> wraps, is refused.
/// </para>
/// </remarks>
public abstract class CTSMMCapletCalibration
{
    private readonly CurveState cs;
    private readonly ImmutableArray<double> coterminalSwapRates;

    /// <summary>Initializes a new instance of the <see cref="CTSMMCapletCalibration"/> class.</summary>
    /// <param name="evolution">The evolution: its times the correlation's and the rate times but the last.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance, one per rate.</param>
    /// <param name="mktCapletVols">The market's caplet volatilities, the last the last swaption's.</param>
    /// <param name="cs">The curve state; copied.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <exception cref="ArgumentException">The inputs don't fit (<see cref="PerformChecks"/>).</exception>
    protected CTSMMCapletCalibration(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> mktCapletVols,
        CurveState cs,
        double displacement)
    {
        PerformChecks(evolution, corr, displacedSwapVariances, mktCapletVols, cs);
        Evolution = evolution;
        Correlation = corr;
        DisplacedSwapVariances = [.. displacedSwapVariances];
        MktCapletVols = [.. mktCapletVols];
        this.cs = cs.Clone();
        Displacement = displacement;
        NumberOfRates = evolution.NumberOfRates;

        // read once, before the copy is shared: a curve state computes its reads into buffers of its own
        coterminalSwapRates = [.. this.cs.CoterminalSwapRates()];
        Displacements = [.. Enumerable.Repeat(displacement, NumberOfRates)];
    }

    /// <summary>Gets the market's caplet volatilities.</summary>
    public ImmutableArray<double> MktCapletVols { get; }

    /// <summary>Gets each rate's displacement, the displacement given.</summary>
    public ImmutableArray<double> Displacements { get; }

    /// <summary>Gets the evolution.</summary>
    protected EvolutionDescription Evolution { get; }

    /// <summary>Gets the swap rates' correlation.</summary>
    protected PiecewiseConstantCorrelation Correlation { get; }

    /// <summary>Gets each swap rate's displaced variance.</summary>
    protected ImmutableArray<PiecewiseConstantVariance> DisplacedSwapVariances { get; }

    /// <summary>Gets the rates' displacement.</summary>
    protected double Displacement { get; }

    /// <summary>Gets the number of rates.</summary>
    protected int NumberOfRates { get; }

    /// <summary>
    /// Checks a calibration's inputs: the evolution's times the correlation's and the rate times but the last, its
    /// rate times the curve state's, a variance, a correlation rate and a caplet volatility per rate, and the last
    /// caplet volatility the last swaption's; and each variance holding its rate's steps, which QuantLib's calibrations
    /// read unchecked (the S-mm plan's MR3 (xx)).
    /// </summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="corr">The swap rates' correlation.</param>
    /// <param name="displacedSwapVariances">Each swap rate's displaced variance.</param>
    /// <param name="mktCapletVols">The market's caplet volatilities.</param>
    /// <param name="cs">The curve state.</param>
    /// <exception cref="ArgumentException">The inputs don't fit.</exception>
    public static void PerformChecks(
        EvolutionDescription evolution,
        PiecewiseConstantCorrelation corr,
        ReadOnlySpan<PiecewiseConstantVariance> displacedSwapVariances,
        ReadOnlySpan<double> mktCapletVols,
        CurveState cs)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentNullException.ThrowIfNull(corr);
        ArgumentNullException.ThrowIfNull(cs);
        ImmutableArray<double> evolutionTimes = evolution.EvolutionTimes;
        if (!evolutionTimes.SequenceEqual(corr.Times))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"evolutionTimes {DataFormattersFunctions.Sequence(evolutionTimes)} not equal to correlation times {DataFormattersFunctions.Sequence(corr.Times)}"),
                nameof(corr));
        }

        ImmutableArray<double> rateTimes = evolution.RateTimes;
        if (!rateTimes.SequenceEqual(cs.RateTimes))
        {
            throw new ArgumentException("mismatch between EvolutionDescription and CurveState rate times", nameof(cs));
        }

        int numberOfRates = evolution.NumberOfRates;
        if (!(numberOfRates == displacedSwapVariances.Length))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between EvolutionDescription number of rates ({numberOfRates}) and displacedSwapVariances size ({displacedSwapVariances.Length})"),
                nameof(displacedSwapVariances));
        }

        if (!(numberOfRates == corr.NumberOfRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between EvolutionDescription number of rates ({numberOfRates}) and corr number of rates ({corr.NumberOfRates})"),
                nameof(corr));
        }

        if (!(numberOfRates == mktCapletVols.Length))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between EvolutionDescription number of rates ({numberOfRates}) and mktCapletVols size ({mktCapletVols.Length})"),
                nameof(mktCapletVols));
        }

        if (!(numberOfRates == cs.NumberOfRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between EvolutionDescription number of rates ({numberOfRates}) and CurveState  number of rates ({cs.NumberOfRates})"),
                nameof(cs));
        }

        if (!rateTimes.AsSpan(0, rateTimes.Length - 1).SequenceEqual(evolutionTimes.AsSpan()))
        {
            throw new ArgumentException("mismatch between evolutionTimes and rateTimes", nameof(evolution));
        }

        foreach (PiecewiseConstantVariance variance in displacedSwapVariances)
        {
            ArgumentNullException.ThrowIfNull(variance, nameof(displacedSwapVariances));
        }

        double lastSwaptionVol = displacedSwapVariances[^1].TotalVolatility(numberOfRates - 1);
        if (!ComparisonFunctions.Close(lastSwaptionVol, mktCapletVols[numberOfRates - 1]))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"last caplet vol ({G16(mktCapletVols[numberOfRates - 1])}) must be equal to last swaption vol ({G16(lastSwaptionVol)}); discrepancy is {G16(lastSwaptionVol - mktCapletVols[numberOfRates - 1])}"),
                nameof(mktCapletVols));
        }

        // the calibrations read each rate's variances to its own step, unchecked (MR3 (xx))
        for (int i = 0; i < numberOfRates; ++i)
        {
            int length = displacedSwapVariances[i].Variances.Length;
            if (!(length > i))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the {DataFormattersFunctions.Ordinal(i + 1)} displaced swap variance has {length} variances, fewer than its {i + 1} steps"),
                    nameof(displacedSwapVariances));
            }
        }
    }

    /// <summary>The <paramref name="i"/>-th swap rate's volatility over the steps, as given.</summary>
    /// <param name="i">The rate.</param>
    /// <returns>The volatilities.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a rate.</exception>
    public ImmutableArray<double> TimeDependentUnCalibratedSwaptionVols(int i)
    {
        if (!(i >= 0 && i < NumberOfRates))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), i, string.Create(CultureInfo.InvariantCulture, $"index ({i}) must less than number of rates ({NumberOfRates})"));
        }

        return DisplacedSwapVariances[i].Volatilities;
    }

    /// <summary>The curve state.</summary>
    /// <returns>A copy of the calibration's.</returns>
    public CurveState CurveState() => cs.Clone();

    /// <summary>
    /// Calibrates: each pass fits the swap rates' pseudo-roots to the caplet volatilities in use, the market's at the
    /// first, then rescales each but the last by the market's over the model's, until the caplets' root-mean-square
    /// error is within the tolerance or the passes run out.
    /// </summary>
    /// <param name="numberOfFactors">The factors.</param>
    /// <param name="maxIterations">The passes; one runs at 0, as in QuantLib's <c>do</c>-<c>while</c>.</param>
    /// <param name="tolerance">The caplets' root-mean-square error at which the passes stop.</param>
    /// <param name="innerMaxIterations">Each fit's iterations.</param>
    /// <param name="innerTolerance">Each fit's tolerance.</param>
    /// <returns>The results.</returns>
    /// <exception cref="ArgumentException">The factors aren't from 1 to the rates.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An iteration count is negative.</exception>
    public CTSMMCapletCalibrationResults Calibrate(
        int numberOfFactors,
        int maxIterations,
        double tolerance,
        int innerMaxIterations = 100,
        double innerTolerance = 1e-8)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(innerMaxIterations);

        // initialize working variables
        double[] usedCapletVols = MktCapletVols.ToArray();
        double[] mktSwaptionVols = new double[NumberOfRates];
        for (int i = 0; i < NumberOfRates; ++i)
        {
            mktSwaptionVols[i] = DisplacedSwapVariances[i].TotalVolatility(i);
        }

        ImmutableArray<double> rateTimes = Evolution.RateTimes;
        double[] mdlCapletVols = new double[NumberOfRates];
        double[] mdlSwaptionVols = new double[NumberOfRates];
        double capletRmsError, capletMaxError, swaptionRmsError, swaptionMaxError;
        CTSMMCapletCalibrationStep step;
        int iterations = 0;

        // calibration loop
        do
        {
            step = CalibrationImpl(numberOfFactors, innerMaxIterations, innerTolerance, usedCapletVols);

            var ctsmm = new PseudoRootFacade(step.SwapCovariancePseudoRoots(), rateTimes.AsSpan(), coterminalSwapRates.AsSpan(), Displacements.AsSpan());
            Matrix swaptionTotCovariance = ctsmm.TotalCovariance(NumberOfRates - 1);

            var flmm = new CotSwapToFwdAdapter(ctsmm);
            Matrix capletTotCovariance = flmm.TotalCovariance(NumberOfRates - 1);

            // check fit
            capletRmsError = swaptionRmsError = 0.0;
            capletMaxError = swaptionMaxError = -1.0;

            for (int i = 0; i < NumberOfRates; ++i)
            {
                mdlSwaptionVols[i] = Math.Sqrt(swaptionTotCovariance[i, i] / rateTimes[i]);
                double swaptionError = Math.Abs(mktSwaptionVols[i] - mdlSwaptionVols[i]);
                swaptionRmsError += swaptionError * swaptionError;
                swaptionMaxError = Math.Max(swaptionMaxError, swaptionError);

                mdlCapletVols[i] = Math.Sqrt(capletTotCovariance[i, i] / rateTimes[i]);
                double capletError = Math.Abs(MktCapletVols[i] - mdlCapletVols[i]);
                capletRmsError += capletError * capletError;
                capletMaxError = Math.Max(capletMaxError, capletError);

                if (i < NumberOfRates - 1)
                {
                    usedCapletVols[i] *= MktCapletVols[i] / mdlCapletVols[i];
                }
            }

            swaptionRmsError = Math.Sqrt(swaptionRmsError / NumberOfRates);
            capletRmsError = Math.Sqrt(capletRmsError / NumberOfRates);
            ++iterations;
        }
        while (iterations < maxIterations && capletRmsError > tolerance);

        CheckReturnedStep(step);
        ImmutableArray<Matrix> swapPseudoRoots = step.SwapCovariancePseudoRoots();
        var model = new PseudoRootFacade(swapPseudoRoots, rateTimes.AsSpan(), coterminalSwapRates.AsSpan(), Displacements.AsSpan());
        var timeDependentCalibratedSwaptionVols = new ImmutableArray<double>[NumberOfRates];
        for (int i = 0; i < NumberOfRates; ++i)
        {
            timeDependentCalibratedSwaptionVols[i] = model.TimeDependentVolatility(i);
        }

        return new CTSMMCapletCalibrationResults(
            step.Failures,
            capletRmsError,
            capletMaxError,
            swaptionRmsError,
            swaptionMaxError,
            swapPseudoRoots,
            ImmutableArray.Create(mdlCapletVols),
            ImmutableArray.Create(mktSwaptionVols),
            ImmutableArray.Create(mdlSwaptionVols),
            ImmutableArray.Create(timeDependentCalibratedSwaptionVols),
            step.DeformationSize,
            step.Alpha);
    }

    /// <summary>One pass's fit of the swap rates' pseudo-roots to the caplet volatilities in use.</summary>
    /// <param name="numberOfFactors">The factors.</param>
    /// <param name="innerMaxIterations">The fit's iterations.</param>
    /// <param name="innerTolerance">The fit's tolerance.</param>
    /// <param name="usedCapletVols">The caplet volatilities in use.</param>
    /// <returns>The pass's pseudo-roots and failures, and what the calibration computes besides.</returns>
    protected abstract CTSMMCapletCalibrationStep CalibrationImpl(
        int numberOfFactors, int innerMaxIterations, double innerTolerance, ReadOnlySpan<double> usedCapletVols);

    /// <summary>A refusal of the pass the calibration returns, after its last pass; none by default.</summary>
    /// <param name="step">The pass the calibration returns.</param>
    private protected virtual void CheckReturnedStep(CTSMMCapletCalibrationStep step)
    {
    }

    /// <summary>The calibrations' check of the factors: from 1 to the rates.</summary>
    /// <param name="numberOfFactors">The factors.</param>
    /// <param name="numberOfRates">The rates.</param>
    /// <exception cref="ArgumentException">The factors aren't from 1 to the rates.</exception>
    private protected static void CheckFactors(int numberOfFactors, int numberOfRates)
    {
        if (!(numberOfFactors <= numberOfRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture, $"number of factors ({numberOfFactors}) cannot be greater than numberOfRates ({numberOfRates})"),
                nameof(numberOfFactors));
        }

        if (!(numberOfFactors > 0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"number of factors ({numberOfFactors}) must be greater than zero"),
                nameof(numberOfFactors));
        }
    }

    /// <summary>The calibrations' check of a step's pseudo-root: a row per rate and a column per factor.</summary>
    /// <param name="k">The step.</param>
    /// <param name="pseudoRoot">The pseudo-root.</param>
    /// <param name="numberOfRates">The rates.</param>
    /// <param name="numberOfFactors">The factors.</param>
    /// <exception cref="NanoQLException">The pseudo-root's size is wrong.</exception>
    private protected static void CheckPseudoRoot(int k, Matrix pseudoRoot, int numberOfRates, int numberOfFactors)
    {
        if (!(pseudoRoot.Rows == numberOfRates))
        {
            throw new NanoQLException(
                string.Create(
                    CultureInfo.InvariantCulture, $"step {k} abcd vol wrong number of rows: {pseudoRoot.Rows} instead of {numberOfRates}"));
        }

        if (!(pseudoRoot.Columns == numberOfFactors))
        {
            throw new NanoQLException(
                string.Create(
                    CultureInfo.InvariantCulture, $"step {k} abcd vol wrong number of columns: {pseudoRoot.Columns} instead of {numberOfFactors}"));
        }
    }

    // A C++ stream after setprecision(16).
    private static string G16(double x) => DataFormattersFunctions.General(x, 16);
}
