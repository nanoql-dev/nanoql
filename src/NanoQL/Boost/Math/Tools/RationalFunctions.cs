// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/rational.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's runtime polynomial and rational evaluation (<c>tools/rational.hpp</c>), the forms its fixed-size
/// arrays fall back to above <c>BOOST_MATH_MAX_POLY_ORDER</c>, 20 under MSVC: Horner's rule from the highest
/// coefficient down, and for a rational function in 1/z above 1. The second-order Horner forms
/// (<see cref="PolynomialHorner2Functions"/>, <see cref="RationalHorner2Functions"/>) take 1 to 20 coefficients and
/// dispatch here above them.
/// </summary>
internal static class RationalFunctions
{
    /// <summary><c>evaluate_polynomial(poly, z, count)</c>: poly[0] + poly[1]·z + … by Horner's rule.</summary>
    /// <param name="poly">The coefficients, lowest order first: at least one.</param>
    /// <param name="z">The point.</param>
    /// <returns>The polynomial's value.</returns>
    internal static double EvaluatePolynomial(ReadOnlySpan<double> poly, double z)
    {
        int count = poly.Length;
        double sum = poly[count - 1];
        for (int i = count - 2; i >= 0; --i)
        {
            sum *= z;
            sum += poly[i];
        }

        return sum;
    }

    /// <summary>
    /// <c>evaluate_rational(num, denom, z, count)</c>: the ratio of two polynomials of one size, by Horner's rule in z
    /// up to 1 and in 1/z above it.
    /// </summary>
    /// <param name="num">The numerator's coefficients, lowest order first: at least one.</param>
    /// <param name="denom">The denominator's coefficients, as many.</param>
    /// <param name="z">The point.</param>
    /// <returns>The rational function's value.</returns>
    internal static double EvaluateRational(ReadOnlySpan<double> num, ReadOnlySpan<double> denom, double z)
    {
        int count = num.Length;
        double s1, s2;
        if (z <= 1)
        {
            s1 = num[count - 1];
            s2 = denom[count - 1];
            for (int i = count - 2; i >= 0; --i)
            {
                s1 *= z;
                s2 *= z;
                s1 += num[i];
                s2 += denom[i];
            }
        }
        else
        {
            z = 1 / z;
            s1 = num[0];
            s2 = denom[0];
            for (int i = 1; i < count; ++i)
            {
                s1 *= z;
                s2 *= z;
                s1 += num[i];
                s2 += denom[i];
            }
        }

        return s1 / s2;
    }
}
