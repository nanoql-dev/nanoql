// SPDX-FileCopyrightText: (C) Copyright John Maddock 2007.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/detail/rational_horner2_20.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's rational evaluation by the second-order Horner rule (<c>rational_horner2_20.hpp</c>), the method
/// Boost's configuration selects under MSVC (<c>BOOST_MATH_RATIONAL_METHOD</c> 2), for 1 to 20 coefficients; above
/// 20, <c>rational.hpp</c>'s runtime loop (<see cref="RationalFunctions.EvaluateRational"/>), where Boost's arrays fall
/// back to it. From 5 coefficients on, a point outside [−1, 1] is evaluated in 1/x, as Boost's forms do (the runtime
/// loop above 1). Each form keeps Boost's order of operations, so the results are Boost's bit for bit.
/// </summary>
internal static class RationalHorner2Functions
{
    /// <summary>
    /// <c>tools::evaluate_rational</c>: (a[0] + … + a[n−1]·x^(n−1)) / (b[0] + … + b[n−1]·x^(n−1)). Boost's
    /// <c>rational.hpp</c> picks the form by the arrays' size at compile time; this picks it by the spans' length.
    /// </summary>
    /// <param name="a">The numerator's coefficients, lowest order first: at least one.</param>
    /// <param name="b">The denominator's coefficients, as many.</param>
    /// <param name="x">The point.</param>
    /// <returns>The rational function's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The spans are empty, or differ in length.</exception>
    internal static double EvaluateRational(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(b), b.Length, "the numerator and denominator differ in size");
        }

        return a.Length switch
        {
            1 => EvaluateRationalCImp1(a, b, x),
            2 => EvaluateRationalCImp2(a, b, x),
            3 => EvaluateRationalCImp3(a, b, x),
            4 => EvaluateRationalCImp4(a, b, x),
            5 => EvaluateRationalCImp5(a, b, x),
            6 => EvaluateRationalCImp6(a, b, x),
            7 => EvaluateRationalCImp7(a, b, x),
            8 => EvaluateRationalCImp8(a, b, x),
            9 => EvaluateRationalCImp9(a, b, x),
            10 => EvaluateRationalCImp10(a, b, x),
            11 => EvaluateRationalCImp11(a, b, x),
            12 => EvaluateRationalCImp12(a, b, x),
            13 => EvaluateRationalCImp13(a, b, x),
            14 => EvaluateRationalCImp14(a, b, x),
            15 => EvaluateRationalCImp15(a, b, x),
            16 => EvaluateRationalCImp16(a, b, x),
            17 => EvaluateRationalCImp17(a, b, x),
            18 => EvaluateRationalCImp18(a, b, x),
            19 => EvaluateRationalCImp19(a, b, x),
            20 => EvaluateRationalCImp20(a, b, x),
            > 20 => RationalFunctions.EvaluateRational(a, b, x),
            _ => throw new ArgumentOutOfRangeException(nameof(a), a.Length, "a rational function needs a coefficient"),
        };
    }

    private static double EvaluateRationalCImp1(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        _ = x;
        return a[0] / b[0];
    }

    private static double EvaluateRationalCImp2(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        return (a[1] * x + a[0]) / (b[1] * x + b[0]);
    }

    private static double EvaluateRationalCImp3(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        return ((a[2] * x + a[1]) * x + a[0]) / ((b[2] * x + b[1]) * x + b[0]);
    }

    private static double EvaluateRationalCImp4(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        return (((a[3] * x + a[2]) * x + a[1]) * x + a[0]) / (((b[3] * x + b[2]) * x + b[1]) * x + b[0]);
    }

    private static double EvaluateRationalCImp5(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((a[4] * x2 + a[2]) * x2 + a[0] + (a[3] * x2 + a[1]) * x)
                / ((b[4] * x2 + b[2]) * x2 + b[0] + (b[3] * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((a[0] * z2 + a[2]) * z2 + a[4] + (a[1] * z2 + a[3]) * z)
                / ((b[0] * z2 + b[2]) * z2 + b[4] + (b[1] * z2 + b[3]) * z);
        }
    }

    private static double EvaluateRationalCImp6(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((a[5] * x2 + a[3]) * x2 + a[1]) * x + (a[4] * x2 + a[2]) * x2 + a[0])
                / (((b[5] * x2 + b[3]) * x2 + b[1]) * x + (b[4] * x2 + b[2]) * x2 + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((a[0] * z2 + a[2]) * z2 + a[4]) * z + (a[1] * z2 + a[3]) * z2 + a[5])
                / (((b[0] * z2 + b[2]) * z2 + b[4]) * z + (b[1] * z2 + b[3]) * z2 + b[5]);
        }
    }

    private static double EvaluateRationalCImp7(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((a[6] * x2 + a[4]) * x2 + a[2]) * x2 + a[0] + ((a[5] * x2 + a[3]) * x2 + a[1]) * x)
                / (((b[6] * x2 + b[4]) * x2 + b[2]) * x2 + b[0] + ((b[5] * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6] + ((a[1] * z2 + a[3]) * z2 + a[5]) * z)
                / (((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6] + ((b[1] * z2 + b[3]) * z2 + b[5]) * z);
        }
    }

    private static double EvaluateRationalCImp8(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((a[7] * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x + ((a[6] * x2 + a[4]) * x2 + a[2]) * x2 + a[0])
                / ((((b[7] * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x + ((b[6] * x2 + b[4]) * x2 + b[2]) * x2 + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z + ((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7])
                / ((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z + ((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]);
        }
    }

    private static double EvaluateRationalCImp9(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((a[8] * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0]
                    + (((a[7] * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x)
                / ((((b[8] * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]
                    + (((b[7] * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2
                    + a[8]
                    + (((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z)
                / ((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2
                    + b[8]
                    + (((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z);
        }
    }

    private static double EvaluateRationalCImp10(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((((a[9] * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
                    + (((a[8] * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0])
                / (((((b[9] * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x
                    + (((b[8] * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z
                    + (((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2
                    + a[9])
                / (((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z
                    + (((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2
                    + b[9]);
        }
    }

    private static double EvaluateRationalCImp11(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((((a[10] * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0]
                    + ((((a[9] * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x)
                / (((((b[10] * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]
                    + ((((b[9] * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2
                    + a[10]
                    + ((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z)
                / (((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2
                    + b[10]
                    + ((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z);
        }
    }

    private static double EvaluateRationalCImp12(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((((a[11] * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
                    + ((((a[10] * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0])
                / ((((((b[11] * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x
                    + ((((b[10] * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z
                    + ((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2
                    + a[11])
                / ((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z
                    + ((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2
                    + b[11]);
        }
    }

    private static double EvaluateRationalCImp13(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((((a[12] * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0]
                    + (((((a[11] * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x)
                / ((((((b[12] * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]
                    + (((((b[11] * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2
                    + a[12]
                    + (((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z)
                / ((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2
                    + b[12]
                    + (((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z);
        }
    }

    private static double EvaluateRationalCImp14(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((((((a[13] * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
                    + (((((a[12] * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0])
                / (((((((b[13] * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x
                    + (((((b[12] * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z
                    + (((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2
                    + a[13])
                / (((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z
                    + (((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2
                    + b[13]);
        }
    }

    private static double EvaluateRationalCImp15(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((((((a[14] * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0]
                    + ((((((a[13] * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x)
                / (((((((b[14] * x2 + b[12]) * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]
                    + ((((((b[13] * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z2
                    + a[14]
                    + ((((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2 + a[13]) * z)
                / (((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z2
                    + b[14]
                    + ((((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2 + b[13]) * z);
        }
    }

    private static double EvaluateRationalCImp16(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((((((a[15] * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
                    + ((((((a[14] * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0])
                / ((((((((b[15] * x2 + b[13]) * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x
                    + ((((((b[14] * x2 + b[12]) * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z2 + a[14]) * z
                    + ((((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2 + a[13]) * z2
                    + a[15])
                / ((((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z2 + b[14]) * z
                    + ((((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2 + b[13]) * z2
                    + b[15]);
        }
    }

    private static double EvaluateRationalCImp17(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((((((a[16] * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0]
                    + (((((((a[15] * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x)
                / ((((((((b[16] * x2 + b[14]) * x2 + b[12]) * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]
                    + (((((((b[15] * x2 + b[13]) * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z2 + a[14]) * z2
                    + a[16]
                    + (((((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2 + a[13]) * z2 + a[15]) * z)
                / ((((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z2 + b[14]) * z2
                    + b[16]
                    + (((((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2 + b[13]) * z2 + b[15]) * z);
        }
    }

    private static double EvaluateRationalCImp18(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((((((((a[17] * x2 + a[15]) * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
                    + (((((((a[16] * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0])
                / (((((((((b[17] * x2 + b[15]) * x2 + b[13]) * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x
                    + (((((((b[16] * x2 + b[14]) * x2 + b[12]) * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z2 + a[14]) * z2 + a[16]) * z
                    + (((((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2 + a[13]) * z2 + a[15]) * z2
                    + a[17])
                / (((((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z2 + b[14]) * z2 + b[16]) * z
                    + (((((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2 + b[13]) * z2 + b[15]) * z2
                    + b[17]);
        }
    }

    private static double EvaluateRationalCImp19(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return (((((((((a[18] * x2 + a[16]) * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0]
                    + ((((((((a[17] * x2 + a[15]) * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x)
                / (((((((((b[18] * x2 + b[16]) * x2 + b[14]) * x2 + b[12]) * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]
                    + ((((((((b[17] * x2 + b[15]) * x2 + b[13]) * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return (((((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z2 + a[14]) * z2 + a[16]) * z2
                    + a[18]
                    + ((((((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2 + a[13]) * z2 + a[15]) * z2 + a[17]) * z)
                / (((((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z2 + b[14]) * z2 + b[16]) * z2
                    + b[18]
                    + ((((((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2 + b[13]) * z2 + b[15]) * z2 + b[17]) * z);
        }
    }

    private static double EvaluateRationalCImp20(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double x)
    {
        if ((-1 <= x) && (x <= 1))
        {
            double x2 = x * x;
            return ((((((((((a[19] * x2 + a[17]) * x2 + a[15]) * x2 + a[13]) * x2 + a[11]) * x2 + a[9]) * x2 + a[7]) * x2 + a[5]) * x2 + a[3]) * x2 + a[1]) * x
                    + ((((((((a[18] * x2 + a[16]) * x2 + a[14]) * x2 + a[12]) * x2 + a[10]) * x2 + a[8]) * x2 + a[6]) * x2 + a[4]) * x2 + a[2]) * x2
                    + a[0])
                / ((((((((((b[19] * x2 + b[17]) * x2 + b[15]) * x2 + b[13]) * x2 + b[11]) * x2 + b[9]) * x2 + b[7]) * x2 + b[5]) * x2 + b[3]) * x2 + b[1]) * x
                    + ((((((((b[18] * x2 + b[16]) * x2 + b[14]) * x2 + b[12]) * x2 + b[10]) * x2 + b[8]) * x2 + b[6]) * x2 + b[4]) * x2 + b[2]) * x2
                    + b[0]);
        }
        else
        {
            double z = 1 / x;
            double z2 = 1 / (x * x);
            return ((((((((((a[0] * z2 + a[2]) * z2 + a[4]) * z2 + a[6]) * z2 + a[8]) * z2 + a[10]) * z2 + a[12]) * z2 + a[14]) * z2 + a[16]) * z2 + a[18]) * z
                    + ((((((((a[1] * z2 + a[3]) * z2 + a[5]) * z2 + a[7]) * z2 + a[9]) * z2 + a[11]) * z2 + a[13]) * z2 + a[15]) * z2 + a[17]) * z2
                    + a[19])
                / ((((((((((b[0] * z2 + b[2]) * z2 + b[4]) * z2 + b[6]) * z2 + b[8]) * z2 + b[10]) * z2 + b[12]) * z2 + b[14]) * z2 + b[16]) * z2 + b[18]) * z
                    + ((((((((b[1] * z2 + b[3]) * z2 + b[5]) * z2 + b[7]) * z2 + b[9]) * z2 + b[11]) * z2 + b[13]) * z2 + b[15]) * z2 + b[17]) * z2
                    + b[19]);
        }
    }
}
