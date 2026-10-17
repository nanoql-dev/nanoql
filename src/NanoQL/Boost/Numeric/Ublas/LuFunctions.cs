// SPDX-FileCopyrightText: Copyright (c) 2000-2002 Joerg Walter, Mathias Koch
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/numeric/ublas/lu.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// uBLAS's LU factorization with partial pivoting and its substitution (<c>lu.hpp</c>), on a dense matrix stored by
/// rows, as <c>ublas::matrix&lt;double&gt;</c> is: element (i, j) of a size1 × size2 matrix is at
/// <c>i * size2 + j</c>.
/// </summary>
/// <remarks>
/// The code path QuantLib's build executes, which defines <c>NDEBUG</c>: uBLAS's <c>BOOST_UBLAS_CHECK</c> and its
/// type checks compile to nothing, so nothing here throws. The operations keep uBLAS's order, one multiply and one
/// subtract each, so the results are uBLAS's bit for bit.
/// </remarks>
internal static class LuFunctions
{
    /// <summary>permutation_matrix (size): the identity permutation, row i at i.</summary>
    /// <param name="size">The size.</param>
    /// <returns>The permutation.</returns>
    internal static int[] PermutationMatrix(int size)
    {
        var pm = new int[size];
        for (int i = 0; i < size; ++i)
        {
            pm[i] = i;
        }

        return pm;
    }

    /// <summary>swap_rows (pm, mv), a matrix: for each row i in order, swaps rows i and pm (i) where they differ.</summary>
    /// <param name="pm">The permutation, as lu_factorize records it.</param>
    /// <param name="mv">The matrix, by rows.</param>
    /// <param name="size2">The matrix's number of columns.</param>
    internal static void SwapRows(ReadOnlySpan<int> pm, Span<double> mv, int size2)
    {
        int size = pm.Length;
        for (int i = 0; i < size; ++i)
        {
            if (i != pm[i])
            {
                SwapRow(mv, size2, i, pm[i]);
            }
        }
    }

    /// <summary>
    /// lu_factorize (m, pm): the LU factorization with partial pivoting, in place. Column i's pivot is the first row
    /// from i on whose magnitude is greatest (<see cref="VectorIndexNormInf"/>); its whole row is swapped with row i
    /// and recorded in pm (i). A zero pivot records the 1-based column in the result and the elimination goes on.
    /// </summary>
    /// <param name="m">The matrix, by rows; on return its strictly lower part holds L, unit diagonal implied, and its
    /// upper part U.</param>
    /// <param name="size1">The number of rows.</param>
    /// <param name="size2">The number of columns.</param>
    /// <param name="pm">The permutation, the identity on entry.</param>
    /// <returns>0, or the 1-based column of the first zero pivot.</returns>
    internal static int LuFactorize(Span<double> m, int size1, int size2, Span<int> pm)
    {
        int singular = 0;
        int size = Math.Min(size1, size2);
        for (int i = 0; i < size; ++i)
        {
            // mci is column (m, i) and mri row (m, i).
            int iNormInf = i + VectorIndexNormInf.Apply(m[((i * size2) + i)..], size1 - i, size2);
            if (m[(iNormInf * size2) + i] != 0.0)
            {
                if (iNormInf != i)
                {
                    pm[i] = iNormInf;
                    SwapRow(m, size2, iNormInf, i);
                }

                double mInv = 1.0 / m[(i * size2) + i];

                // project (mci, range (i + 1, size1)) *= m_inv
                for (int r = i + 1; r < size1; ++r)
                {
                    m[(r * size2) + i] = m[(r * size2) + i] * mInv;
                }
            }
            else if (singular == 0)
            {
                singular = i + 1;
            }

            // project (m, range (i + 1, size1), range (i + 1, size2)).minus_assign (
            //     outer_prod (project (mci, range (i + 1, size1)), project (mri, range (i + 1, size2))))
            for (int r = i + 1; r < size1; ++r)
            {
                for (int c = i + 1; c < size2; ++c)
                {
                    m[(r * size2) + c] = m[(r * size2) + c] - (m[(r * size2) + i] * m[(i * size2) + c]);
                }
            }
        }

        return singular;
    }

    /// <summary>
    /// lu_substitute (m, e), a matrix right-hand side: solves L U X = E in place, by the unit-lower forward solve and
    /// then the upper backward solve.
    /// </summary>
    /// <param name="m">The LU factors, size1 × size1, by rows, as lu_factorize leaves them.</param>
    /// <param name="e">The right-hand side, size1 × size2, by rows; on return the solution.</param>
    /// <param name="size1">The order of the factors.</param>
    /// <param name="size2">The right-hand side's number of columns.</param>
    internal static void LuSubstitute(ReadOnlySpan<double> m, Span<double> e, int size1, int size2)
    {
        TriangularFunctions.InplaceSolveUnitLower(m, e, size1, size2);
        TriangularFunctions.InplaceSolveUpper(m, e, size1, size2);
    }

    /// <summary>
    /// lu_substitute (m, pm, mv), a matrix right-hand side: permutes mv's rows by pm (<see cref="SwapRows"/>), then
    /// solves.
    /// </summary>
    /// <param name="m">The LU factors, size1 × size1, by rows.</param>
    /// <param name="pm">The permutation lu_factorize recorded.</param>
    /// <param name="mv">The right-hand side, size1 × size2, by rows; on return the solution.</param>
    /// <param name="size1">The order of the factors.</param>
    /// <param name="size2">The right-hand side's number of columns.</param>
    internal static void LuSubstitute(ReadOnlySpan<double> m, ReadOnlySpan<int> pm, Span<double> mv, int size1, int size2)
    {
        SwapRows(pm, mv, size2);
        LuSubstitute(m, mv, size1, size2);
    }

    // row (m, a).swap (row (m, b)): the whole rows, element by element.
    private static void SwapRow(Span<double> m, int size2, int a, int b)
    {
        Span<double> ra = m.Slice(a * size2, size2), rb = m.Slice(b * size2, size2);
        for (int c = 0; c < size2; ++c)
        {
            (ra[c], rb[c]) = (rb[c], ra[c]);
        }
    }
}
