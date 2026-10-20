// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/sparsematrix.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The product of a sparse matrix and a vector.</summary>
public static class SparseMatrixFunctions
{
    /// <summary>The product A·x, one element per row of A.</summary>
    /// <param name="A">The sparse matrix.</param>
    /// <param name="x">The vector, as long as A has columns.</param>
    /// <returns>A new vector with A's number of rows; zero in each row beyond the last row written to.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="x"/>'s size isn't A's number of columns.</exception>
    /// <remarks>
    /// QuantLib sizes the result by the columns and writes it by the rows (<c>sparsematrix.hpp:51-69</c>), so a matrix
    /// with more rows than columns writes past the result, undefined, and one with fewer pads it with zeros: an 8×4
    /// matrix's product is a vector of 4. Here the result has a row's length; each row QuantLib computes is its value.
    /// A documented divergence.
    /// </remarks>
    public static Vector Prod(SparseMatrix A, Vector x)
    {
        ArgumentNullException.ThrowIfNull(A);
        ArgumentNullException.ThrowIfNull(x);
        if (!(x.Size == A.Size2))
        {
            throw new ArgumentException(
                $"vectors and sparse matrices with different sizes ({x.Size}, {A.Size1}x{A.Size2}) cannot be multiplied",
                nameof(x));
        }

        var b = new Vector(A.Size1, 0.0);

        ReadOnlySpan<int> index1 = A.Index1Data, index2 = A.Index2Data;
        ReadOnlySpan<double> values = A.ValueData;
        for (int i = 0; i < A.Filled1 - 1; ++i)
        {
            int begin = index1[i];
            int end = index1[i + 1];
            double t = 0;
            for (int j = begin; j < end; ++j)
            {
                t += values[j] * x[index2[j]];
            }

            b[i] = t;
        }

        return b;
    }
}
