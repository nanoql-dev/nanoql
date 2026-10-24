// SPDX-FileCopyrightText: (C) Copyright John Maddock 2007.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/detail/polynomial_horner2_20.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's polynomial evaluation by the second-order Horner rule (<c>polynomial_horner2_20.hpp</c>), the method
/// Boost's configuration selects under MSVC (<c>BOOST_MATH_POLY_METHOD</c> 2), for 1 to 20 coefficients, the sizes
/// Boost's fixed-size arrays dispatch to; above 20 (<c>BOOST_MATH_MAX_POLY_ORDER</c>), <c>rational.hpp</c>'s
/// runtime Horner loop (<see cref="RationalFunctions.EvaluatePolynomial"/>), where Boost's arrays fall back to it. Each form keeps Boost's order of operations,
/// so the results are Boost's bit for bit; plain Horner differs from the second-order forms by up to 2 ulp.
/// </summary>
internal static class PolynomialHorner2Functions
{
    /// <summary>
    /// <c>tools::evaluate_polynomial</c>: a[0] + a[1]·x + … + a[n−1]·x^(n−1). Boost's <c>rational.hpp</c> picks the
    /// form by the array's size at compile time; this picks it by the span's length.
    /// </summary>
    /// <param name="a">The coefficients, lowest order first: at least one.</param>
    /// <param name="x">The point.</param>
    /// <returns>The polynomial's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The span is empty.</exception>
    internal static double EvaluatePolynomial(ReadOnlySpan<double> a, double x) => a.Length switch
    {
        1 => EvaluatePolynomialCImp1(a, x),
        2 => EvaluatePolynomialCImp2(a, x),
        3 => EvaluatePolynomialCImp3(a, x),
        4 => EvaluatePolynomialCImp4(a, x),
        5 => EvaluatePolynomialCImp5(a, x),
        6 => EvaluatePolynomialCImp6(a, x),
        7 => EvaluatePolynomialCImp7(a, x),
        8 => EvaluatePolynomialCImp8(a, x),
        9 => EvaluatePolynomialCImp9(a, x),
        10 => EvaluatePolynomialCImp10(a, x),
        11 => EvaluatePolynomialCImp11(a, x),
        12 => EvaluatePolynomialCImp12(a, x),
        13 => EvaluatePolynomialCImp13(a, x),
        14 => EvaluatePolynomialCImp14(a, x),
        15 => EvaluatePolynomialCImp15(a, x),
        16 => EvaluatePolynomialCImp16(a, x),
        17 => EvaluatePolynomialCImp17(a, x),
        18 => EvaluatePolynomialCImp18(a, x),
        19 => EvaluatePolynomialCImp19(a, x),
        20 => EvaluatePolynomialCImp20(a, x),
        > 20 => RationalFunctions.EvaluatePolynomial(a, x),
        _ => throw new ArgumentOutOfRangeException(nameof(a), a.Length, "a polynomial needs a coefficient"),
    };

    private static double EvaluatePolynomialCImp1(ReadOnlySpan<double> a, double x)
    {
        _ = x;
        return a[0];
    }

    private static double EvaluatePolynomialCImp2(ReadOnlySpan<double> a, double x)
    {
        return a[1] * x + a[0];
    }

    private static double EvaluatePolynomialCImp3(ReadOnlySpan<double> a, double x)
    {
        return (a[2] * x + a[1]) * x + a[0];
    }

    private static double EvaluatePolynomialCImp4(ReadOnlySpan<double> a, double x)
    {
        return ((a[3] * x + a[2]) * x + a[1]) * x + a[0];
    }

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
        return (((a[8] * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0]
            + (((a[7] * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp10(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((a[9] * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + (((a[8] * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0];
    }

    private static double EvaluatePolynomialCImp11(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((a[10] * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0]
            + ((((a[9] * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp12(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((((a[11] * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + ((((a[10] * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0];
    }

    private static double EvaluatePolynomialCImp13(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((((a[12] * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0]
            + (((((a[11] * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp14(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((((a[13] * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + (((((a[12] * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0];
    }

    private static double EvaluatePolynomialCImp15(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((((a[14] * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0]
            + ((((((a[13] * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp16(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((((((a[15] * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + ((((((a[14] * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0];
    }

    private static double EvaluatePolynomialCImp17(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((((((a[16] * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0]
            + (((((((a[15] * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp18(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((((((a[17] * x2 + a[15]) * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + (((((((a[16] * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0];
    }

    private static double EvaluatePolynomialCImp19(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return ((((((((a[18] * x2 + a[16]) * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0]
            + ((((((((a[17] * x2 + a[15]) * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x;
    }

    private static double EvaluatePolynomialCImp20(ReadOnlySpan<double> a, double x)
    {
        double x2 = x * x;
        return (((((((((a[19] * x2 + a[17]) * x2 + a[15]) * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
            + ((((((((a[18] * x2 + a[16]) * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
            + a[0];
    }
}
