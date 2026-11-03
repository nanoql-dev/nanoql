// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/ctsmmcapletcalibration.hpp ql/models/marketmodels/models/ctsmmcapletcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A coterminal swap market model caplet calibration's results: QuantLib's inspectors of a calibrated
/// <c>CTSMMCapletCalibration</c>, the fit's errors, the swap rates' pseudo-roots and the model's caplet and swaption
/// volatilities.
/// </summary>
/// <remarks>
/// Immutable: a pseudo-root read returns a copy. The deformation size is null where the calibration doesn't compute it,
/// where QuantLib returns its initializer's 987654321 (<c>ctsmmcapletcalibration.cpp:199</c>).
/// </remarks>
public sealed class CTSMMCapletCalibrationResults
{
    private readonly ImmutableArray<Matrix> swapPseudoRoots;
    private readonly ImmutableArray<ImmutableArray<double>> timeDependentCalibratedSwaptionVols;

    /// <summary>Initializes a new instance of the <see cref="CTSMMCapletCalibrationResults"/> class.</summary>
    /// <param name="failures">The last pass's failures.</param>
    /// <param name="capletRmsError">The caplet volatilities' root-mean-square error.</param>
    /// <param name="capletMaxError">The caplet volatilities' largest error.</param>
    /// <param name="swaptionRmsError">The swaption volatilities' root-mean-square error.</param>
    /// <param name="swaptionMaxError">The swaption volatilities' largest error.</param>
    /// <param name="swapPseudoRoots">Each step's swap-rate pseudo-root; copied.</param>
    /// <param name="mdlCapletVols">The model's caplet volatilities.</param>
    /// <param name="mktSwaptionVols">The market's swaption volatilities.</param>
    /// <param name="mdlSwaptionVols">The model's swaption volatilities.</param>
    /// <param name="timeDependentCalibratedSwaptionVols">Each swap rate's calibrated volatility over the steps.</param>
    /// <param name="deformationSize">The deformation size, or null where the calibration doesn't compute it.</param>
    /// <param name="alpha">The alpha form's α of each rate, or null for the other calibrations.</param>
    public CTSMMCapletCalibrationResults(
        int failures,
        double capletRmsError,
        double capletMaxError,
        double swaptionRmsError,
        double swaptionMaxError,
        IEnumerable<Matrix> swapPseudoRoots,
        ImmutableArray<double> mdlCapletVols,
        ImmutableArray<double> mktSwaptionVols,
        ImmutableArray<double> mdlSwaptionVols,
        ImmutableArray<ImmutableArray<double>> timeDependentCalibratedSwaptionVols,
        double? deformationSize,
        ImmutableArray<double>? alpha)
    {
        ArgumentNullException.ThrowIfNull(swapPseudoRoots);
        Failures = failures;
        CapletRmsError = capletRmsError;
        CapletMaxError = capletMaxError;
        SwaptionRmsError = swaptionRmsError;
        SwaptionMaxError = swaptionMaxError;
        this.swapPseudoRoots = [.. swapPseudoRoots.Select(m => m.Clone())];
        MdlCapletVols = mdlCapletVols;
        MktSwaptionVols = mktSwaptionVols;
        MdlSwaptionVols = mdlSwaptionVols;
        this.timeDependentCalibratedSwaptionVols = timeDependentCalibratedSwaptionVols;
        DeformationSize = deformationSize;
        Alpha = alpha;
    }

    /// <summary>Gets the last pass's failures.</summary>
    public int Failures { get; }

    /// <summary>Gets a value indicating whether the last pass had no failure, QuantLib's <c>calibrate</c>
    /// result.</summary>
    public bool Converged => Failures == 0;

    /// <summary>Gets the caplet volatilities' root-mean-square error.</summary>
    public double CapletRmsError { get; }

    /// <summary>Gets the caplet volatilities' largest error.</summary>
    public double CapletMaxError { get; }

    /// <summary>Gets the swaption volatilities' root-mean-square error.</summary>
    public double SwaptionRmsError { get; }

    /// <summary>Gets the swaption volatilities' largest error.</summary>
    public double SwaptionMaxError { get; }

    /// <summary>Gets the model's caplet volatilities.</summary>
    public ImmutableArray<double> MdlCapletVols { get; }

    /// <summary>Gets the market's swaption volatilities.</summary>
    public ImmutableArray<double> MktSwaptionVols { get; }

    /// <summary>Gets the model's swaption volatilities.</summary>
    public ImmutableArray<double> MdlSwaptionVols { get; }

    /// <summary>Gets the deformation size, null where the calibration doesn't compute it.</summary>
    public double? DeformationSize { get; }

    /// <summary>Gets the alpha form's α of each rate, null for the other calibrations.</summary>
    public ImmutableArray<double>? Alpha { get; }

    /// <summary>Each step's swap-rate pseudo-root.</summary>
    /// <returns>Copies of the pseudo-roots.</returns>
    public ImmutableArray<Matrix> SwapPseudoRoots() => [.. swapPseudoRoots.Select(m => m.Clone())];

    /// <summary>The <paramref name="i"/>-th step's swap-rate pseudo-root.</summary>
    /// <param name="i">The step.</param>
    /// <returns>A copy of the pseudo-root.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public Matrix SwapPseudoRoot(int i)
    {
        if (!(i >= 0 && i < swapPseudoRoots.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), i, string.Create(CultureInfo.InvariantCulture, $"{i}is an invalid index, must be less than {swapPseudoRoots.Length}"));
        }

        return swapPseudoRoots[i].Clone();
    }

    /// <summary>The <paramref name="i"/>-th swap rate's calibrated volatility over the steps.</summary>
    /// <param name="i">The rate.</param>
    /// <returns>The volatilities.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a rate.</exception>
    public ImmutableArray<double> TimeDependentCalibratedSwaptionVols(int i)
    {
        if (!(i >= 0 && i < timeDependentCalibratedSwaptionVols.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                i,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"index ({i}) must less than number of rates ({timeDependentCalibratedSwaptionVols.Length})"));
        }

        return timeDependentCalibratedSwaptionVols[i];
    }
}
