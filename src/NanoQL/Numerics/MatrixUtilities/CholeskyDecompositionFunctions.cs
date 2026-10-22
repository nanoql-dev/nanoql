// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/choleskydecomposition.hpp ql/math/matrixutilities/choleskydecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.Numerics;

/// <summary>The Cholesky decomposition of a symmetric matrix, and the solve it gives.</summary>
public static class CholeskyDecompositionFunctions
{
    /// <summary>
    /// The lower-triangular L of the Cholesky decomposition S = L·Lᵀ of a symmetric positive definite matrix; with
    /// <paramref name="flexible"/>, of a positive semi-definite one too, a non-positive pivot read as 0.
    /// </summary>
    /// <param name="m">The matrix, symmetric.</param>
    /// <param name="flexible">Whether a positive semi-definite matrix is accepted.</param>
    /// <returns>A new lower-triangular matrix.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="m"/> isn't square, or isn't positive definite and <paramref name="flexible"/> is false.
    /// </exception>
    public static Matrix CholeskyDecomposition(Matrix m, bool flexible = false)
    {
        ArgumentNullException.ThrowIfNull(m);
        Matrix S = m;
        int i, j, size = S.Rows;

        if (!(size == S.Columns))
        {
            throw new ArgumentException("input matrix is not a square matrix", nameof(m));
        }

        var result = new Matrix(size, size, 0.0);
        double sum;
        for (i = 0; i < size; i++)
        {
            for (j = i; j < size; j++)
            {
                sum = S[i, j];
                for (int k = 0; k <= i - 1; k++)
                {
                    sum -= result[i, k] * result[j, k];
                }

                if (i == j)
                {
                    if (!(flexible || sum > 0.0))
                    {
                        throw new ArgumentException("input matrix is not positive definite", nameof(m));
                    }

                    // To handle positive semi-definite matrices take the
                    // square root of sum if positive, else zero.
                    result[i, i] = Math.Sqrt(sum < 0.0 ? 0.0 : sum);
                }
                else
                {
                    // With positive semi-definite matrices is possible
                    // to have result[i][i]==0.0
                    // In this case sum happens to be zero as well
                    result[j, i] = CloseEnough(result[i, i], 0.0) ? 0.0 : sum / result[i, i];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The solution x of L·Lᵀ·x = b, for the lower-triangular L of a Cholesky decomposition: forward, then backward
    /// substitution.
    /// </summary>
    /// <param name="L">The lower-triangular matrix.</param>
    /// <param name="b">The right-hand side, of L's size.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">L isn't square of b's size.</exception>
    public static Vector CholeskySolveFor(Matrix L, Vector b)
    {
        ArgumentNullException.ThrowIfNull(L);
        ArgumentNullException.ThrowIfNull(b);
        int n = b.Size;

        if (!(L.Columns == n && L.Rows == n))
        {
            throw new ArgumentException("Size of input matrix and vector does not match.", nameof(L));
        }

        var x = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            // -std::inner_product(L.row_begin(i), L.row_begin(i)+i, x.begin(), Real(-b[i]))
            double acc = -b[i];
            for (int k = 0; k < i; k++)
            {
                acc += L[i, k] * x[k];
            }

            x[i] = -acc;
            x[i] /= L[i, i];
        }

        for (int i = n - 1; i >= 0; --i)
        {
            // -std::inner_product(L.column_begin(i)+i+1, L.column_end(i), x.begin()+i+1, Real(-x[i]))
            double acc = -x[i];
            for (int k = i + 1; k < n; k++)
            {
                acc += L[k, i] * x[k];
            }

            x[i] = -acc;
            x[i] /= L[i, i];
        }

        return x;
    }
}
