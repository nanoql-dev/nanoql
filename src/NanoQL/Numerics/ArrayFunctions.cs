// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/array.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The free functions of QuantLib's <c>array.hpp</c> over a <see cref="Vector"/>: the dot product, the norm, and the
/// element-by-element mathematical functions, each returning a new vector.
/// </summary>
/// <remarks>
/// QuantLib's free <c>swap</c> and <c>operator&lt;&lt;</c> are <see cref="Vector.Swap"/> and
/// <see cref="Vector.ToString"/>.
/// </remarks>
public static class ArrayFunctions
{
    /// <summary>The dot product, summed in order from zero.</summary>
    /// <param name="v1">The first vector.</param>
    /// <param name="v2">The second vector.</param>
    /// <returns>The sum of the element-by-element products.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static double DotProduct(Vector v1, Vector v2)
    {
        ArgumentNullException.ThrowIfNull(v1);
        ArgumentNullException.ThrowIfNull(v2);
        if (!(v1.Size == v2.Size))
        {
            throw new ArgumentException(
                $"arrays with different sizes ({v1.Size}, {v2.Size}) cannot be multiplied",
                nameof(v2));
        }

        ReadOnlySpan<double> a = v1.AsSpan(), b = v2.AsSpan();
        double result = 0.0;
        for (int i = 0; i < a.Length; ++i)
        {
            result = result + (a[i] * b[i]);
        }

        return result;
    }

    /// <summary>The Euclidean norm, the square root of the vector's dot product with itself.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>The norm.</returns>
    public static double Norm2(Vector v) => Math.Sqrt(DotProduct(v, v));

    /// <summary>The absolute value of each element.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector Abs(Vector v) => Map(v, Math.Abs);

    /// <summary>The square root of each element.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector Sqrt(Vector v) => Map(v, Math.Sqrt);

    /// <summary>The natural logarithm of each element.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector Log(Vector v) => Map(v, Math.Log);

    /// <summary>The exponential of each element.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector Exp(Vector v) => Map(v, Math.Exp);

    /// <summary>Each element raised to a power.</summary>
    /// <param name="v">The vector.</param>
    /// <param name="alpha">The exponent.</param>
    /// <returns>A new vector.</returns>
    public static Vector Pow(Vector v, double alpha)
    {
        ArgumentNullException.ThrowIfNull(v);
        var result = new Vector(v.Size);
        ReadOnlySpan<double> x = v.AsSpan();
        Span<double> r = result.AsSpan();
        for (int i = 0; i < x.Length; ++i)
        {
            r[i] = Math.Pow(x[i], alpha);
        }

        return result;
    }

    private static Vector Map(Vector v, Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(v);
        var result = new Vector(v.Size);
        ReadOnlySpan<double> x = v.AsSpan();
        Span<double> r = result.AsSpan();
        for (int i = 0; i < x.Length; ++i)
        {
            r[i] = f(x[i]);
        }

        return result;
    }
}
