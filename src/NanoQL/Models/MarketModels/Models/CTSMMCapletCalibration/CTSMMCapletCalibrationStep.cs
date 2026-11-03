// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/ctsmmcapletcalibration.hpp ql/models/marketmodels/models/ctsmmcapletcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// One pass of a coterminal swap market model's caplet calibration: the swap rates' pseudo-roots and the failures, and
/// what the calibration computes besides (the alpha form's α, a and b; the max-homogeneity calibration's deformation
/// size and swaption error). <see cref="CTSMMCapletCalibration"/>'s hook returns it, and the calibrations' static
/// functions return it in place of QuantLib's out-parameters.
/// </summary>
/// <remarks>Immutable: a pseudo-root read returns a copy.</remarks>
public sealed class CTSMMCapletCalibrationStep
{
    private readonly ImmutableArray<Matrix> swapCovariancePseudoRoots;

    /// <summary>Initializes a new instance of the <see cref="CTSMMCapletCalibrationStep"/> class.</summary>
    /// <param name="failures">The pass's failures.</param>
    /// <param name="swapCovariancePseudoRoots">Each step's swap-rate pseudo-root; copied.</param>
    /// <param name="alpha">The alpha form's α of each rate, or null.</param>
    /// <param name="a">The alpha form's a of each rate, or null.</param>
    /// <param name="b">The alpha form's b of each rate, or null.</param>
    /// <param name="deformationSize">The squared deformation of the volatilities from the homogeneous ones, or
    /// null.</param>
    /// <param name="totalSwaptionError">The sum of the squared swaption errors, or null.</param>
    public CTSMMCapletCalibrationStep(
        int failures,
        IEnumerable<Matrix> swapCovariancePseudoRoots,
        ImmutableArray<double>? alpha = null,
        ImmutableArray<double>? a = null,
        ImmutableArray<double>? b = null,
        double? deformationSize = null,
        double? totalSwaptionError = null)
    {
        ArgumentNullException.ThrowIfNull(swapCovariancePseudoRoots);
        Failures = failures;
        this.swapCovariancePseudoRoots = [.. swapCovariancePseudoRoots.Select(m => m.Clone())];
        Alpha = alpha;
        A = a;
        B = b;
        DeformationSize = deformationSize;
        TotalSwaptionError = totalSwaptionError;
    }

    /// <summary>Gets the pass's failures.</summary>
    public int Failures { get; }

    /// <summary>Gets the alpha form's α of each rate, null for the other calibrations.</summary>
    public ImmutableArray<double>? Alpha { get; }

    /// <summary>Gets the alpha form's a of each rate, null for the other calibrations.</summary>
    public ImmutableArray<double>? A { get; }

    /// <summary>Gets the alpha form's b of each rate, null for the other calibrations.</summary>
    public ImmutableArray<double>? B { get; }

    /// <summary>Gets the squared deformation from the homogeneous volatilities, null where the calibration doesn't
    /// compute it.</summary>
    public double? DeformationSize { get; }

    /// <summary>Gets the sum of the squared swaption errors, null where the calibration doesn't compute it.</summary>
    public double? TotalSwaptionError { get; }

    /// <summary>Each step's swap-rate pseudo-root.</summary>
    /// <returns>Copies of the pseudo-roots.</returns>
    public ImmutableArray<Matrix> SwapCovariancePseudoRoots() => [.. swapCovariancePseudoRoots.Select(m => m.Clone())];
}
