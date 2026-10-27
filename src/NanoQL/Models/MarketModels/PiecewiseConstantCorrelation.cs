// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/piecewiseconstantcorrelation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A piecewise-constant correlation of the rates: one correlation matrix per period between its times, which must
/// include every rate time but the last.
/// </summary>
/// <remarks>
/// Immutable (the S-mm plan's MR1 (i)): a matrix read returns a copy. An extension point, as in QuantLib.
/// </remarks>
public abstract class PiecewiseConstantCorrelation
{
    /// <summary>Initializes a new instance of the <see cref="PiecewiseConstantCorrelation"/> class.</summary>
    protected PiecewiseConstantCorrelation()
    {
    }

    /// <summary>Gets the times ending each period, one per correlation matrix.</summary>
    public abstract ImmutableArray<double> Times { get; }

    /// <summary>Gets the rate times.</summary>
    public abstract ImmutableArray<double> RateTimes { get; }

    /// <summary>Gets the number of rates.</summary>
    public abstract int NumberOfRates { get; }

    /// <summary>The correlation matrices, one per period.</summary>
    /// <returns>Copies of the matrices.</returns>
    public abstract ImmutableArray<Matrix> Correlations();

    /// <summary>The <paramref name="i"/>-th period's correlation matrix.</summary>
    /// <param name="i">The period, below the number of matrices.</param>
    /// <returns>A copy of the matrix.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a period.</exception>
    public virtual Matrix Correlation(int i)
    {
        ImmutableArray<Matrix> results = Correlations();
        CheckIndex(i, results.Length);
        return results[i];
    }

    /// <summary>Checks a period's index, with QuantLib's text.</summary>
    /// <param name="i">The period's index.</param>
    /// <param name="size">The number of periods.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't below <paramref name="size"/>.</exception>
    private protected static void CheckIndex(int i, int size)
    {
        if (!(i >= 0 && i < size))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(CultureInfo.InvariantCulture, $"index ({i}) must be less than correlations vector size ({size})"));
        }
    }
}
