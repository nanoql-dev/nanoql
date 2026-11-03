// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/capletcoterminalperiodic.hpp ql/models/marketmodels/models/capletcoterminalperiodic.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The periodic caplet and swaption calibration's results, QuantLib's return value and out-parameters.
/// </summary>
/// <remarks>
/// Immutable: a matrix read returns a copy. The failures are the last pass's inner calibration's and the iterations the
/// passes run, where QuantLib returns the inner calibration's <c>bool</c> and counts one pass short when it converges.
/// The deformation size, which QuantLib never sets, is null.
/// </remarks>
public sealed class CapletSwaptionPeriodicCalibrationResults
{
    private readonly ImmutableArray<Matrix> swapCovariancePseudoRoots;
    private readonly Matrix modelSwaptionVolsMatrix;

    /// <summary>Initializes a new instance of the <see cref="CapletSwaptionPeriodicCalibrationResults"/>
    /// class.</summary>
    /// <param name="failures">The last pass's inner calibration's failures.</param>
    /// <param name="totalSwaptionError">The last pass's sum of the squared long swaption errors.</param>
    /// <param name="swapCovariancePseudoRoots">The last pass's swap-rate pseudo-roots; copied.</param>
    /// <param name="finalScales">The long swaptions' scaling factors after the last pass.</param>
    /// <param name="iterationsDone">The passes run.</param>
    /// <param name="errorImprovement">The last pass's improvement in the long swaptions' root-mean-square
    /// error.</param>
    /// <param name="modelSwaptionVolsMatrix">Each pass's model long swaption volatilities, a row each; copied.</param>
    public CapletSwaptionPeriodicCalibrationResults(
        int failures,
        double totalSwaptionError,
        IEnumerable<Matrix> swapCovariancePseudoRoots,
        ImmutableArray<double> finalScales,
        int iterationsDone,
        double errorImprovement,
        Matrix modelSwaptionVolsMatrix)
    {
        ArgumentNullException.ThrowIfNull(swapCovariancePseudoRoots);
        ArgumentNullException.ThrowIfNull(modelSwaptionVolsMatrix);
        Failures = failures;
        TotalSwaptionError = totalSwaptionError;
        this.swapCovariancePseudoRoots = [.. swapCovariancePseudoRoots.Select(m => m.Clone())];
        FinalScales = finalScales;
        IterationsDone = iterationsDone;
        ErrorImprovement = errorImprovement;
        this.modelSwaptionVolsMatrix = modelSwaptionVolsMatrix.Clone();
    }

    /// <summary>Gets the last pass's inner calibration's failures.</summary>
    public int Failures { get; }

    /// <summary>Gets the deformation size: null, as QuantLib never computes it.</summary>
    public double? DeformationSize => null;

    /// <summary>Gets the last pass's sum of the squared long swaption errors.</summary>
    public double TotalSwaptionError { get; }

    /// <summary>Gets the long swaptions' scaling factors after the last pass.</summary>
    public ImmutableArray<double> FinalScales { get; }

    /// <summary>Gets the passes run.</summary>
    public int IterationsDone { get; }

    /// <summary>Gets the last pass's improvement in the long swaptions' root-mean-square error.</summary>
    public double ErrorImprovement { get; }

    /// <summary>The last pass's swap-rate pseudo-roots.</summary>
    /// <returns>Copies of the pseudo-roots.</returns>
    public ImmutableArray<Matrix> SwapCovariancePseudoRoots() => [.. swapCovariancePseudoRoots.Select(m => m.Clone())];

    /// <summary>Each pass's model long swaption volatilities, a row each, the rows past the passes zero.</summary>
    /// <returns>A copy of the matrix.</returns>
    public Matrix ModelSwaptionVolsMatrix() => modelSwaptionVolsMatrix.Clone();
}
