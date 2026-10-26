// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/householder.hpp ql/math/matrixutilities/householder.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;
using static NanoQL.NumericLimits;

namespace NanoQL.Numerics;

/// <summary>
/// The Householder reflection onto a unit vector e: the transformation that maps a vector a to |a|e.
/// </summary>
/// <remarks>
/// <para>Immutable and safe to share across threads: the vector is copied.</para>
/// <para>
/// Where a·e is negative, the reflection vector is (a − |a|e)/|a − |a|e|, QuantLib's general form, so a vector
/// opposite e maps to |a|e; QuantLib's near-parallel branches assume a·e positive, so its zero vector for a ∥ −e maps a
/// to −|a|e and its series branch near −e to neither (<c>Divergences/HouseholderOppositeTests.cs</c>).
/// </para>
/// </remarks>
public sealed class HouseholderReflection
{
    private readonly Vector e;

    /// <summary>Initializes a new instance of the <see cref="HouseholderReflection"/> class.</summary>
    /// <param name="e">The unit vector reflected onto, copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="null"/>.</exception>
    public HouseholderReflection(Vector e)
    {
        ArgumentNullException.ThrowIfNull(e);
        this.e = e.Clone();
    }

    /// <summary>The reflected vector, |a|e.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="a"/> is the zero vector, or its size isn't e's.</exception>
    public Vector Evaluate(Vector a)
    {
        Vector v = ReflectionVector(a);
        return new HouseholderTransformation(v).Evaluate(a);
    }

    /// <summary>The vector v whose Householder transformation maps a to |a|e.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>A new vector: the zero vector where a is already |a|e to machine precision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="a"/> is the zero vector, or its size isn't e's.</exception>
    public Vector ReflectionVector(Vector a)
    {
        ArgumentNullException.ThrowIfNull(a);
        double na = Norm2(a);
        if (!(na > 0))
        {
            throw new ArgumentException("vector of length zero given", nameof(a));
        }

        double aDotE = DotProduct(a, e);
        if (aDotE < 0.0)
        {
            // a points away from e: no cancellation in a - |a|e, QuantLib's general branch
            Vector g = a - (na * e);
            return g / Norm2(g);
        }

        Vector a1 = aDotE * e;
        Vector a2 = a - a1;

        double eps = DotProduct(a2, a2) / (aDotE * aDotE);
        if (eps < QL_EPSILON * QL_EPSILON)
        {
            return new Vector(a.Size, 0.0);
        }
        else if (eps < 1e-4)
        {
            double eps2 = eps * eps;
            double eps3 = eps * eps2;
            double eps4 = eps2 * eps2;
            Vector v =
                (a2 - (a1 * ((eps / 2.0) - (eps2 / 8.0) + (eps3 / 16.0) - (5 / 128.0 * eps4))))
                / (aDotE * Math.Sqrt(eps + (eps2 / 4.0) - (eps3 / 8.0) + (5 / 64.0 * eps4)));
            return v;
        }
        else
        {
            Vector c = a - (na * e);
            return c / Norm2(c);
        }
    }
}
