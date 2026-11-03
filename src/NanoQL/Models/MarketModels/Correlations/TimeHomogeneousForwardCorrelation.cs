// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.hpp ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A time-homogeneous forward correlation: in the k-th period, between rate times k and k + 1, the alive rates i and j
/// correlate as rates i − k and j − k do at time zero.
/// </summary>
/// <remarks>Immutable: a matrix read returns a copy.</remarks>
public sealed class TimeHomogeneousForwardCorrelation : PiecewiseConstantCorrelation
{
    private readonly Matrix[] correlations;

    /// <summary>Initializes a new instance of the <see cref="TimeHomogeneousForwardCorrelation"/> class.</summary>
    /// <param name="fwdCorrelation">The rates' correlation at time zero, one row and column per rate; copied.</param>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing and positive.</param>
    /// <exception cref="ArgumentException">The times aren't valid, or the matrix isn't one row and column per rate.</exception>
    public TimeHomogeneousForwardCorrelation(Matrix fwdCorrelation, ReadOnlySpan<double> rateTimes)
    {
        ArgumentNullException.ThrowIfNull(fwdCorrelation);
        int numberOfRates = rateTimes.IsEmpty ? 0 : rateTimes.Length - 1;
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(numberOfRates >= 1))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        if (!(numberOfRates == fwdCorrelation.Rows))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between number of rates ({numberOfRates}) and fwdCorrelation rows ({fwdCorrelation.Rows})"),
                nameof(fwdCorrelation));
        }

        if (!(numberOfRates == fwdCorrelation.Columns))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between number of rates ({numberOfRates}) and fwdCorrelation columns ({fwdCorrelation.Columns})"),
                nameof(fwdCorrelation));
        }

        NumberOfRates = numberOfRates;
        RateTimes = [.. rateTimes];
        Times = [.. rateTimes[..^1]];
        correlations = [.. EvolvedMatrices(fwdCorrelation)];
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> Times { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> RateTimes { get; }

    /// <inheritdoc/>
    public override int NumberOfRates { get; }

    /// <summary>
    /// The time-homogeneous correlations of a correlation at time zero: the k-th matrix holds 1 on the alive rates'
    /// diagonal and, between alive rates i and j, the time-zero correlation of rates i − k and j − k; zero elsewhere.
    /// </summary>
    /// <param name="fwdCorrelation">The rates' correlation at time zero, one row per rate.</param>
    /// <returns>New matrices, one per rate.</returns>
    /// <exception cref="ArgumentException">The matrix has fewer columns than its rows less one.</exception>
    /// <remarks>
    /// QuantLib reads each matrix's entries from the input's rows and columns alike, past its rows where it has fewer
    /// columns than its rows less one, which is undefined and refused (<c>Divergences/MarketModelUndefinedReadsTests.cs</c>).
    /// </remarks>
    public static ImmutableArray<Matrix> EvolvedMatrices(Matrix fwdCorrelation)
    {
        ArgumentNullException.ThrowIfNull(fwdCorrelation);
        int numberOfRates = fwdCorrelation.Rows;
        if (!(fwdCorrelation.Columns >= numberOfRates - 1))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"fwdCorrelation's columns ({fwdCorrelation.Columns}) fewer than its rows ({numberOfRates}) less one"),
                nameof(fwdCorrelation));
        }

        var correlations = new Matrix[numberOfRates];
        for (int k = 0; k < correlations.Length; ++k)
        {
            var m = new Matrix(numberOfRates, numberOfRates, 0.0);

            // proper diagonal values
            for (int i = k; i < numberOfRates; ++i)
            {
                m[i, i] = 1.0;
            }

            // copy only time homogeneous values
            for (int i = k; i < numberOfRates; ++i)
            {
                for (int j = k; j < i; ++j)
                {
                    double value = fwdCorrelation[i - k, j - k];
                    m[i, j] = value;
                    m[j, i] = value;
                }
            }

            correlations[k] = m;
        }

        return ImmutableArray.Create(correlations);
    }

    /// <inheritdoc/>
    public override ImmutableArray<Matrix> Correlations() => [.. correlations.Select(m => m.Clone())];

    /// <inheritdoc/>
    public override Matrix Correlation(int i)
    {
        CheckIndex(i, correlations.Length);
        return correlations[i].Clone();
    }
}
