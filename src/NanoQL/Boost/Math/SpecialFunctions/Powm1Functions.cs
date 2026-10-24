// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/powm1.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's x^y − 1 (<c>powm1.hpp</c>) for <c>double</c>: through <c>expm1</c> of y·ln x where that is below ½, so
/// that small results keep their digits; through <c>pow</c> otherwise. Boost's <c>expm1</c> for <c>double</c> is the C
/// runtime's, here <see cref="Expm1Functions.StdExpm1"/>.
/// </summary>
internal static class Powm1Functions
{
    private const string Function = "boost::math::powm1<long double>(long double, long double)";

    /// <summary><c>powm1(x, y)</c>: x^y − 1.</summary>
    /// <param name="x">The base.</param>
    /// <param name="y">The exponent.</param>
    /// <returns>x^y − 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">x is negative and y isn't an integer, or the power is complex or
    /// undefined.</exception>
    /// <exception cref="NanoQLException">The result overflows.</exception>
    internal static double Powm1(double x, double y)
    {
        if (double.IsNegative(x))
        {
            // y had better be an integer:
            if (Math.Truncate(y) != y)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x), $"Error in function {Function}: For non-integral exponent, expected base > 0 but got {Show(x)}");
            }

            if (Math.Truncate(y / 2) == y / 2)
            {
                return Powm1Imp(-x, y);
            }
        }

        return Powm1Imp(x, y);
    }

    private static double Powm1Imp(double x, double y)
    {
        if ((Math.Abs(y * (x - 1)) < 0.5) || (Math.Abs(y) < 0.2))
        {
            // We don't have any good/quick approximation for log(x) * y
            // so just try it and see:
            double l = y * Math.Log(x);
            if (l < 0.5)
            {
                return Expm1Functions.StdExpm1(l);
            }

            if (l > 709.0)
            {
                throw new NanoQLException($"Error in function {Function}: Overflow Error");
            }

            // fall through....
        }

        double result = Math.Pow(x, y) - 1;
        if (double.IsInfinity(result))
        {
            throw new NanoQLException($"Error in function {Function}: Overflow Error");
        }

        if (double.IsNaN(result))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"Error in function {Function}: Result of pow is complex or undefined");
        }

        return result;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
