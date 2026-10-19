// SPDX-FileCopyrightText: (C) Copyright Hubert Holin 2001.
// SPDX-FileCopyrightText: (C) Copyright John Maddock 2008.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/atanh.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's inverse hyperbolic tangent (<c>atanh.hpp</c>) for <c>double</c> under the default policy:
/// <c>atanh_imp</c>. Its <c>checked_narrowing_cast</c> isn't ported: its overflow check can't fire, since the result
/// is already a <c>double</c> and the overflows are raised before it.
/// </summary>
internal static class AtanhFunctions
{
    // tools::epsilon<double>(), tools::root_epsilon<double>() and tools::forth_root_epsilon<double>(), Boost's
    // constants for a 53-bit significand.
    private const double Epsilon = 2.220446049250313E-16;
    private const double RootEpsilon = 0.1490116119384765625e-7;
    private const double ForthRootEpsilon = 0.0001220703125;

    /// <summary><c>atanh(x)</c>: the inverse hyperbolic tangent.</summary>
    /// <param name="x">The value, in [−1, 1].</param>
    /// <returns>atanh(x).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is outside [−1, 1], or NaN.</exception>
    /// <exception cref="NanoQLException">
    /// <paramref name="x"/> is within machine epsilon of −1 or 1, where the result overflows.
    /// </exception>
    internal static double Atanh(double x)
    {
        const string function = "boost::math::atanh<double>(double)";

        if (x < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"Error in function {function}: atanh requires x >= -1, but got x = {Show(x)}.");
        }
        else if (x > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"Error in function {function}: atanh requires x <= 1, but got x = {Show(x)}.");
        }
        else if (double.IsNaN(x))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"Error in function {function}: atanh requires -1 <= x <= 1, but got x = {Show(x)}.");
        }
        else if (x < -1 + Epsilon)
        {
            // -Infinity:
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }
        else if (x > 1 - Epsilon)
        {
            // Infinity:
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }
        else if (Math.Abs(x) >= ForthRootEpsilon)
        {
            // http://functions.wolfram.com/ElementaryFunctions/ArcTanh/02/
            if (Math.Abs(x) < 0.5f)
            {
                return (Log1pFunctions.Log1p(x) - Log1pFunctions.Log1p(-x)) / 2;
            }

            return Math.Log((1 + x) / (1 - x)) / 2;
        }
        else
        {
            // http://functions.wolfram.com/ElementaryFunctions/ArcTanh/06/01/03/01/
            // approximation by taylor series in x at 0 up to order 2
            double result = x;

            if (Math.Abs(x) >= RootEpsilon)
            {
                double x3 = x * x * x;

                // approximation by taylor series in x at 0 up to order 4
                result += x3 / 3;
            }

            return result;
        }
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
