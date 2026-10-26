// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/householder.hpp ql/math/matrixutilities/householder.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;

namespace NanoQL.Numerics;

/// <summary>The Householder transformation of a vector v: x ↦ x − 2(v·x)v.</summary>
/// <remarks>
/// <para>Immutable and safe to share across threads: the vector is copied.</para>
/// <para>
/// The matrix of the zero vector is the identity, the transformation it defines, where QuantLib's divides by the
/// vector's norm and returns NaNs (<c>Divergences/HouseholderZeroVectorTests.cs</c>).
/// </para>
/// </remarks>
public sealed class HouseholderTransformation
{
    private readonly Vector v;

    /// <summary>Initializes a new instance of the <see cref="HouseholderTransformation"/> class.</summary>
    /// <param name="v">The vector, copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="v"/> is <see langword="null"/>.</exception>
    public HouseholderTransformation(Vector v)
    {
        ArgumentNullException.ThrowIfNull(v);
        this.v = v.Clone();
    }

    /// <summary>The matrix of the reflection through the hyperplane normal to v: I − 2yyᵀ with y = v/|v|.</summary>
    /// <returns>A new matrix; the identity for the zero vector.</returns>
    public Matrix GetMatrix()
    {
        int n = v.Size;
        var m = new Matrix(n, n);
        double norm = Norm2(v);
        if (norm == 0.0)
        {
            // the zero vector transforms nothing, where QuantLib's v_ / Norm2(v_) is NaN
            for (int i = 0; i < n; ++i)
            {
                m[i, i] = 1.0;
            }

            return m;
        }

        Vector y = v / norm;
        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                m[i, j] = (i == j ? 1.0 : 0.0) - (2 * y[i] * y[j]);
            }
        }

        return m;
    }

    /// <summary>The transformed vector, x − 2(v·x)v.</summary>
    /// <param name="x">The vector.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="x"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="x"/>'s size isn't v's.</exception>
    public Vector Evaluate(Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return x - ((2.0 * DotProduct(v, x)) * v);
    }
}
