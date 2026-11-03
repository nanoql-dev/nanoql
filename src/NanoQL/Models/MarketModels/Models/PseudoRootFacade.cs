// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/pseudorootfacade.hpp ql/models/marketmodels/models/pseudorootfacade.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A market model given by its pseudo-roots, one per rate time but the last: its evolution the rate times', its
/// initial rates and displacements given; the coterminal swap market model a caplet calibration builds.
/// </summary>
/// <remarks>
/// Immutable: it copies the pseudo-roots given, and a read returns a copy. No pseudo-roots, where QuantLib reads the
/// first of none, are refused.
/// </remarks>
public sealed class PseudoRootFacade : MarketModel
{
    private readonly Matrix[] covariancePseudoRoots;

    /// <summary>Initializes a new instance of the <see cref="PseudoRootFacade"/> class from a calibration's
    /// results.</summary>
    /// <param name="calibration">The calibration: its curve state's coterminal swap rates and its
    /// displacements.</param>
    /// <param name="results">The calibration's results: its swap rates' pseudo-roots.</param>
    /// <exception cref="ArgumentException">The results' pseudo-roots don't fit the calibration's rates.</exception>
    public PseudoRootFacade(CTSMMCapletCalibration calibration, CTSMMCapletCalibrationResults results)
        : this(Roots(results), RateTimes(calibration), CoterminalSwapRates(calibration), calibration.Displacements.AsSpan())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PseudoRootFacade"/> class.</summary>
    /// <param name="covariancePseudoRoots">Each step's pseudo-root, one per rate, of the same factors; copied.</param>
    /// <param name="rateTimes">The rate times, at least two and increasing.</param>
    /// <param name="initialRates">The initial rates.</param>
    /// <param name="displacements">The displacements, one per rate.</param>
    /// <exception cref="ArgumentException">
    /// There are no pseudo-roots, the rate times are fewer than two or not increasing, or the pseudo-roots, rates and
    /// displacements don't fit.
    /// </exception>
    public PseudoRootFacade(
        IReadOnlyList<Matrix> covariancePseudoRoots,
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> initialRates,
        ReadOnlySpan<double> displacements)
    {
        ArgumentNullException.ThrowIfNull(covariancePseudoRoots);

        // QuantLib reads the first pseudo-root's size before its checks
        if (covariancePseudoRoots.Count == 0)
        {
            throw new ArgumentException("no pseudo-roots given", nameof(covariancePseudoRoots));
        }

        NumberOfFactors = covariancePseudoRoots[0].Columns;
        NumberOfRates = covariancePseudoRoots[0].Rows;
        NumberOfSteps = covariancePseudoRoots.Count;
        InitialRates = [.. initialRates];
        Displacements = [.. displacements];

        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(rateTimes.Length > 1))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        if (!(NumberOfRates == rateTimes.Length - 1))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"mismatch between number of rates ({NumberOfRates}) and rate times"),
                nameof(rateTimes));
        }

        if (!(NumberOfRates == displacements.Length))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture, $"mismatch between number of rates ({NumberOfRates}) and displacements ({displacements.Length})"),
                nameof(displacements));
        }

        if (!(NumberOfRates <= NumberOfFactors * NumberOfSteps))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"number of rates ({NumberOfRates}) greater than number of factors ({NumberOfFactors}) times number of steps ({NumberOfSteps})"),
                nameof(covariancePseudoRoots));
        }

        // evolutionTimes are not given for the time being
        if (!(NumberOfRates == covariancePseudoRoots.Count))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"number of rates ({NumberOfRates}) must be equal to covariancePseudoRoots.size() ({covariancePseudoRoots.Count})"),
                nameof(covariancePseudoRoots));
        }

        Evolution = new EvolutionDescription(rateTimes);
        this.covariancePseudoRoots = new Matrix[NumberOfSteps];
        for (int k = 0; k < NumberOfSteps; ++k)
        {
            Matrix pseudoRoot = covariancePseudoRoots[k];
            ArgumentNullException.ThrowIfNull(pseudoRoot, nameof(covariancePseudoRoots));
            if (!(pseudoRoot.Rows == NumberOfRates))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"step {k}: pseudoRoot has wrong number of rows: {pseudoRoot.Rows} instead of {NumberOfRates}"),
                    nameof(covariancePseudoRoots));
            }

            // not really mandatory
            if (!(pseudoRoot.Columns == NumberOfFactors))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"step {k}: pseudoRoot has wrong number of columns: {pseudoRoot.Columns} instead of {NumberOfFactors}"),
                    nameof(covariancePseudoRoots));
            }

            this.covariancePseudoRoots[k] = pseudoRoot.Clone();
        }
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> InitialRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> Displacements { get; }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution { get; }

    /// <inheritdoc/>
    public override int NumberOfRates { get; }

    /// <inheritdoc/>
    public override int NumberOfFactors { get; }

    /// <inheritdoc/>
    public override int NumberOfSteps { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public override Matrix PseudoRoot(int i)
    {
        if (!(i >= 0 && i < NumberOfSteps))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(
                    CultureInfo.InvariantCulture, $"the index {i} is invalid: it must be less than number of steps ({NumberOfSteps})"));
        }

        return covariancePseudoRoots[i].Clone();
    }

    private static ImmutableArray<Matrix> Roots(CTSMMCapletCalibrationResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.SwapPseudoRoots();
    }

    private static ReadOnlySpan<double> RateTimes(CTSMMCapletCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        return calibration.CurveState().RateTimes.AsSpan();
    }

    private static ReadOnlySpan<double> CoterminalSwapRates(CTSMMCapletCalibration calibration) =>
        calibration.CurveState().CoterminalSwapRates();
}
