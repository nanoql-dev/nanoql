// SPDX-FileCopyrightText: (C) Copyright John Maddock 2007.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/detail/polynomial_horner2_20.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's polynomial evaluation by the second-order Horner rule (<c>polynomial_horner2_20.hpp</c>), the method
/// Boost's configuration selects under MSVC (<c>BOOST_MATH_POLY_METHOD</c> 2), for the sizes the error functions use,
/// 5 to 11 coefficients. Each form keeps Boost's order of operations, so the results are Boost's bit for bit; plain
/// Horner differs from them by up to 2 ulp.
/// </summary>
internal static class PolynomialHorner2Functions
{
    /// <summary>
    /// <c>tools::evaluate_polynomial</c>: a[0] + a[1]·x + … + a[n−1]·x^(n−1). Boost's <c>rational.hpp</c> picks the
    /// form by the array's size at compile time; this picks it by the span's length.
    /// </summary>
    /// <param name="a">The coefficients, lowest order first: 5 to 11 of them.</param>
    /// <param name="x">The point.</param>
    /// <returns>The polynomial's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The span doesn't hold 5 to 11 coefficients.</exception>
    internal static double EvaluatePolynomial(ReadOnlySpan<double> a, double x) => a.Length switch
    {
        5 => EvaluatePolynomialCImp5(a, x),
        6 => EvaluatePolynomialCImp6(a, x),
        7 => EvaluatePolynomialCImp7(a, x),
        8 => EvaluatePolynomialCImp8(a, x),
        9 => EvaluatePolynomialCImp9(a, x),
        10 => EvaluatePolynomialCImp10(a, x),
        11 => EvaluatePolynomialCImp11(a, x),
        _ => throw new ArgumentOutOfRangeException(
            nameof(a), a.Length, "the second-order Horner forms ported take 5 to 11 coefficients"),
    };

    private static double EvaluatePolynomialCImp5(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (a[4] * x2 + a[2]) * x2 + a[0] + (a[3] * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp6(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((a[5] * x2 + a[3]) * x2 + a[1]) * x + (a[4] * x2 + a[2]) * x2 + a[0];
    }

    private static double EvaluatePolynomialCImp7(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((a[6] * x2 + a[4]) * x2 + a[2]) * x2 + a[0] + ((a[5] * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp8(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((a[7] * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x + ((a[6] * x2 + a[4]) * x2 + a[2]) * x2 + a[0];
    }

    private static double EvaluatePolynomialCImp9(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((a[8] * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2 + a[0]
            + (((a[7] * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp10(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((a[9] * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + (((a[8] * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2 + a[0];
    }

    private static double EvaluatePolynomialCImp11(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((a[10] * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2 + a[0]
            + ((((a[9] * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }
}
