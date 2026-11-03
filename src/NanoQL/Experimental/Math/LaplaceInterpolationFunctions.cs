// SPDX-FileCopyrightText: Copyright (C) 2015, 2024 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/laplaceinterpolation.hpp ql/experimental/math/laplaceinterpolation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The Laplace interpolation of a matrix's missing values: QuantLib's <c>laplaceInterpolation</c>.</summary>
[Experimental("NQL0001")]
public static class LaplaceInterpolationFunctions
{
    /// <summary>
    /// A copy of a matrix with its missing values Laplace-interpolated, the rows on the y grid and the columns on the x
    /// grid: QuantLib's <c>laplaceInterpolation</c>, which fills the matrix in place.
    /// </summary>
    /// <remarks>A missing value is NaN, where QuantLib's matrix holds <c>Null&lt;Real&gt;()</c>.</remarks>
    /// <param name="A">The matrix.</param>
    /// <param name="x">The columns' grid; empty for 0, 1, 2, ..., an equidistant one.</param>
    /// <param name="y">The rows' grid; empty for 0, 1, 2, ..., an equidistant one.</param>
    /// <param name="relTol">BiCGstab's relative tolerance.</param>
    /// <param name="maxIterMultiplier">BiCGstab's iterations, per grid point.</param>
    /// <returns>The filled copy.</returns>
    /// <exception cref="NanoQLException">BiCGstab fails to converge, with its text.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> or <paramref name="y"/> is given with another length than <paramref name="A"/>'s columns or
    /// rows, where QuantLib reads past one or the other.
    /// </exception>
    public static Matrix LaplaceInterpolation(
        Matrix A,
        IReadOnlyList<double>? x = null,
        IReadOnlyList<double>? y = null,
        double relTol = 1E-6,
        int maxIterMultiplier = 10)
    {
        ArgumentNullException.ThrowIfNull(A);
        IReadOnlyList<double> rows = y is null || y.Count == 0 ? [.. Enumerable.Range(0, A.Rows).Select(i => (double)i)] : y;
        IReadOnlyList<double> columns = x is null || x.Count == 0 ? [.. Enumerable.Range(0, A.Columns).Select(i => (double)i)] : x;

        if (!(rows.Count == A.Rows))
        {
            throw new ArgumentException($"{rows.Count} y values given for {A.Rows} rows", nameof(y));
        }

        if (!(columns.Count == A.Columns))
        {
            throw new ArgumentException($"{columns.Count} x values given for {A.Columns} columns", nameof(x));
        }

        Matrix copy = A.Clone();
        var interpolation = new LaplaceInterpolation(
            coordinates => double.IsNaN(A[coordinates[0], coordinates[1]]) ? null : A[coordinates[0], coordinates[1]],
            [rows, columns],
            relTol,
            maxIterMultiplier);

        for (int i = 0; i < A.Rows; ++i)
        {
            for (int j = 0; j < A.Columns; ++j)
            {
                if (double.IsNaN(A[i, j]))
                {
                    copy[i, j] = interpolation.Value([i, j]);
                }
            }
        }

        return copy;
    }
}
