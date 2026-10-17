// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrix.hpp ql/math/matrix.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The free functions of QuantLib's <c>matrix.hpp</c> over a <see cref="Matrix"/>: the transpose, the outer product,
/// and the inverse and determinant through Boost uBLAS's LU factorization with partial pivoting.
/// </summary>
/// <remarks>
/// QuantLib's free <c>swap</c> and <c>operator&lt;&lt;</c> are <see cref="Matrix.Swap"/> and
/// <see cref="Matrix.ToString"/>.
/// </remarks>
public static class MatrixFunctions
{
    /// <summary>The transpose.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>A new matrix, whose row i is the matrix's column i.</returns>
    public static Matrix Transpose(Matrix m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var result = new Matrix(m.Columns, m.Rows);
        for (int i = 0; i < m.Rows; i++)
        {
            Span<double> row = m.Row(i);
            for (int j = 0; j < row.Length; ++j)
            {
                result[j, i] = row[j];
            }
        }

        return result;
    }

    /// <summary>The outer product: element (i, j) is <c>v2[j] * v1[i]</c>.</summary>
    /// <param name="v1">The first vector, giving the rows.</param>
    /// <param name="v2">The second vector, giving the columns.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">A vector is empty.</exception>
    public static Matrix OuterProduct(Vector v1, Vector v2)
    {
        ArgumentNullException.ThrowIfNull(v1);
        ArgumentNullException.ThrowIfNull(v2);
        return OuterProduct(v1.AsSpan(), v2.AsSpan());
    }

    /// <summary>
    /// The outer product of two ranges: element (i, j) is <c>v2[j] * v1[i]</c>. QuantLib's iterator-range form.
    /// </summary>
    /// <param name="v1">The first range, giving the rows.</param>
    /// <param name="v2">The second range, giving the columns.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">A range is empty.</exception>
    public static Matrix OuterProduct(ReadOnlySpan<double> v1, ReadOnlySpan<double> v2)
    {
        int size1 = v1.Length;
        if (!(size1 > 0))
        {
            throw new ArgumentException("null first vector", nameof(v1));
        }

        int size2 = v2.Length;
        if (!(size2 > 0))
        {
            throw new ArgumentException("null second vector", nameof(v2));
        }

        var result = new Matrix(size1, size2);
        for (int i = 0; i < size1; i++)
        {
            Span<double> row = result.Row(i);
            for (int j = 0; j < size2; ++j)
            {
                row[j] = v2[j] * v1[i];
            }
        }

        return result;
    }

    /// <summary>
    /// The inverse, by uBLAS's LU factorization with partial pivoting and its substitution into the identity, in
    /// uBLAS's order of operations.
    /// </summary>
    /// <param name="m">The matrix.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">The matrix isn't square, or is singular: a pivot is zero.</exception>
    public static Matrix Inverse(Matrix m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!(m.Rows == m.Columns))
        {
            throw new ArgumentException("matrix is not square", nameof(m));
        }

        int n = m.Rows;

        // uBLAS's matrix a, a copy of m by rows. QuantLib's catches of uBLAS's internal_logic and external_logic
        // aren't ported: uBLAS raises them only in checks a build with NDEBUG, QuantLib's, leaves out.
        double[] a = m.AsSpan().ToArray();
        int[] pert = LuFunctions.PermutationMatrix(n);

        // lu decomposition
        int singular = LuFunctions.LuFactorize(a, n, n, pert);
        if (!(singular == 0))
        {
            throw new ArgumentException("singular matrix given", nameof(m));
        }

        var inverse = new double[n * n];
        for (int i = 0; i < n; ++i)
        {
            inverse[(i * n) + i] = 1.0;
        }

        // backsubstitution
        LuFunctions.LuSubstitute(a, pert, inverse, n, n);

        return new Matrix(n, n, inverse);
    }

    /// <summary>
    /// The determinant: the product of the diagonal of uBLAS's LU factorization with partial pivoting, each factor
    /// negated where its row was swapped. A singular matrix gives the product of a zero pivot.
    /// </summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The determinant; 1 for a matrix with no rows.</returns>
    /// <exception cref="ArgumentException">The matrix isn't square.</exception>
    public static double Determinant(Matrix m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!(m.Rows == m.Columns))
        {
            throw new ArgumentException("matrix is not square", nameof(m));
        }

        int n = m.Rows;
        double[] a = m.AsSpan().ToArray();

        // lu decomposition
        int[] pert = LuFunctions.PermutationMatrix(n);
        _ = LuFunctions.LuFactorize(a, n, n, pert);

        double retVal = 1.0;
        for (int i = 0; i < n; ++i)
        {
            if (pert[i] != i)
            {
                retVal *= -a[(i * n) + i];
            }
            else
            {
                retVal *= a[(i * n) + i];
            }
        }

        return retVal;
    }
}
