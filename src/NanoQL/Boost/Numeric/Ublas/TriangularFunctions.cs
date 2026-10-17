// SPDX-FileCopyrightText: Copyright (c) 2000-2002 Joerg Walter, Mathias Koch
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/numeric/ublas/triangular.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// uBLAS's in-place triangular solves with a matrix right-hand side (<c>triangular.hpp</c>), the dense case, on
/// matrices stored by rows: the n × n factor e1 at <c>i * n + j</c>, the n × size2 right-hand side e2 at
/// <c>i * size2 + j</c>.
/// </summary>
/// <remarks>
/// The code path QuantLib's build executes, which defines <c>NDEBUG</c>: the checks of the sizes and of a zero
/// diagonal (<c>BOOST_UBLAS_CHECK</c>) compile to nothing. A right-hand side element that becomes zero is skipped, as
/// uBLAS skips it.
/// </remarks>
internal static class TriangularFunctions
{
    // The diagonal of triangular_adaptor<E1, unit_lower>, which the unit-lower solve divides by.
    private const double UnitDiagonal = 1.0;

    /// <summary>
    /// inplace_solve (e1, e2, unit_lower_tag): solves L X = E2 forward, where L is e1's strictly lower part with a
    /// unit diagonal (the unit-lower adaptor over e1), dispatched to the lower, dense-proxy case.
    /// </summary>
    /// <param name="e1">The factor, by rows; only its strictly lower part is read.</param>
    /// <param name="e2">The right-hand side, by rows; on return the solution.</param>
    /// <param name="size1">The order of the factor and the right-hand side's number of rows.</param>
    /// <param name="size2">The right-hand side's number of columns.</param>
    internal static void InplaceSolveUnitLower(ReadOnlySpan<double> e1, Span<double> e2, int size1, int size2)
    {
        for (int n = 0; n < size1; ++n)
        {
            for (int l = 0; l < size2; ++l)
            {
                double t = e2[(n * size2) + l] /= UnitDiagonal;
                if (t != 0.0)
                {
                    for (int m = n + 1; m < size1; ++m)
                    {
                        e2[(m * size2) + l] = e2[(m * size2) + l] - (e1[(m * size1) + n] * t);
                    }
                }
            }
        }
    }

    /// <summary>
    /// inplace_solve (e1, e2, upper_tag): solves U X = E2 backward, where U is e1's upper part, diagonal included:
    /// the upper, dense-proxy case.
    /// </summary>
    /// <param name="e1">The factor, by rows; only its upper part is read.</param>
    /// <param name="e2">The right-hand side, by rows; on return the solution.</param>
    /// <param name="size1">The order of the factor and the right-hand side's number of rows.</param>
    /// <param name="size2">The right-hand side's number of columns.</param>
    internal static void InplaceSolveUpper(ReadOnlySpan<double> e1, Span<double> e2, int size1, int size2)
    {
        for (int n = size1 - 1; n >= 0; --n)
        {
            for (int l = size2 - 1; l >= 0; --l)
            {
                double t = e2[(n * size2) + l] /= e1[(n * size1) + n];
                if (t != 0.0)
                {
                    for (int m = n - 1; m >= 0; --m)
                    {
                        e2[(m * size2) + l] = e2[(m * size2) + l] - (e1[(m * size1) + n] * t);
                    }
                }
            }
        }
    }
}
