// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/bernsteinpolynomial.hpp ql/math/bernsteinpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Bernstein polynomials B_{i,n}(x) = C(n, i)·x^i·(1 − x)^(n−i). See Weisstein, Eric W. "Bernstein Polynomial."
/// From MathWorld, a Wolfram Web Resource, http://mathworld.wolfram.com/BernsteinPolynomial.html.
/// </summary>
/// <remarks>
/// The powers are <see cref="Math.Pow"/>'s, as QuantLib's are <c>std::pow</c>'s, which agree within a few units in the
/// last place across runtimes.
/// </remarks>
public static class BernsteinPolynomial
{
    /// <summary>The Bernstein polynomial B_{i,n} at a point.</summary>
    /// <param name="i">The index, QuantLib's <c>Natural</c>, at most <paramref name="n"/>.</param>
    /// <param name="n">The degree, QuantLib's <c>Natural</c>.</param>
    /// <param name="x">The point.</param>
    /// <returns>B_{i,n}(x).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> or <paramref name="n"/> is negative, or <paramref name="i"/> is greater than
    /// <paramref name="n"/>, where QuantLib's unsigned <c>n − i</c> wraps.
    /// </exception>
    public static double Get(int i, int n, double x)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(i, n);
        double coeff = Factorial.Get(n) / (Factorial.Get(n - i) * Factorial.Get(i));

        return coeff * Math.Pow(x, i) * Math.Pow(1.0 - x, n - i);
    }
}
