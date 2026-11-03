// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/moorepenroseinverse.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The Moore-Penrose inverse of a real matrix: QuantLib's <c>moorePenroseInverse</c>.</summary>
/// <remarks>
/// References: <see href="http://de.mathworks.com/help/matlab/ref/pinv.html"/>,
/// <see href="https://en.wikipedia.org/wiki/Moore%E2%80%93Penrose_pseudoinverse"/>.
/// </remarks>
[Experimental("NQL0001")]
public static class MoorePenroseInverseFunctions
{
    /// <summary>
    /// The Moore-Penrose inverse A⁺ of a matrix, V Σ⁺ Uᵀ from its singular value decomposition, the singular values at
    /// or below the tolerance taken as zero: QuantLib's <c>moorePenroseInverse</c>.
    /// </summary>
    /// <remarks>
    /// A wide matrix, with fewer rows than columns, is inverted through its transpose, A⁺ = ((Aᵀ)⁺)ᵀ, Penrose's
    /// identity: QuantLib's reads its singular values past their end and then throws "matrices with different sizes
    /// (3x2, 3x3) cannot be multiplied" on a 2 × 3 matrix (<c>Divergences/MoorePenroseWideTests.cs</c>). A tall or
    /// square matrix is QuantLib's.
    /// </remarks>
    /// <param name="A">The m × n matrix.</param>
    /// <param name="tol">
    /// The tolerance on the singular values; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// max(m, n) · ε · |σ₀|.
    /// </param>
    /// <returns>The n × m inverse.</returns>
    public static Matrix MoorePenroseInverse(Matrix A, double? tol = null)
    {
        ArgumentNullException.ThrowIfNull(A);
        if (A.Rows < A.Columns)
        {
            return MatrixFunctions.Transpose(MoorePenroseInverse(MatrixFunctions.Transpose(A), tol));
        }

        int m = A.Rows;
        int n = A.Columns;

        var svd = new SVD(A);
        Vector singularValues = svd.SingularValues();

        double tol0 = tol ?? (Math.Max(m, n) * NumericLimits.QL_EPSILON * Math.Abs(singularValues[0]));

        var sp = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            if (Math.Abs(singularValues[i]) > tol0)
            {
                sp[i, i] = 1.0 / singularValues[i];
            }
        }

        return svd.V() * sp * MatrixFunctions.Transpose(svd.U());
    }
}
