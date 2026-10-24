// SPDX-FileCopyrightText: Copyright John Maddock 2007.
// SPDX-FileCopyrightText: Copyright Matt Borland 2023.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/round.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's rounding to the nearest integer, halves away from zero (<c>round.hpp</c>): <c>round</c>, and
/// <c>iround</c> and <c>llround</c>, which convert to <c>int</c> and <c>long long</c>.
/// </summary>
/// <remarks>
/// Under the default policy a value that isn't finite, or whose rounding lies outside the target type, raises Boost's
/// rounding error, a <c>std::runtime_error</c>, thrown as <see cref="NanoQLException"/> with Boost's message. Boost's
/// names carry the argument's type: <c>long double</c> at every call nanoql ports, the distributions' and the Bessel
/// functions' evaluation type.
/// </remarks>
internal static class RoundFunctions
{
    /// <summary><c>round(v)</c>: <paramref name="v"/> rounded to the nearest integer, halves away from zero.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The rounded value.</returns>
    /// <exception cref="NanoQLException"><paramref name="v"/> isn't finite.</exception>
    internal static double Round(double v)
    {
        if (!double.IsFinite(v))
        {
            throw RoundingError("boost::math::round<long double>(long double)", v);
        }

        //
        // The logic here is rather convoluted, but avoids a number of traps,
        // see discussion here https://github.com/boostorg/math/pull/8
        //
        if (-0.5 < v && v < 0.5)
        {
            // special case to avoid rounding error on the direct
            // predecessor of +0.5 resp. the direct successor of -0.5 in
            // IEEE floating point types
            return 0;
        }
        else if (v > 0)
        {
            // subtract v from ceil(v) first in order to avoid rounding
            // errors on largest representable integer numbers
            double c = Math.Ceiling(v);
            return 0.5 < c - v ? c - 1 : c;
        }
        else
        {
            // see former branch
            double f = Math.Floor(v);
            return 0.5 < v - f ? f + 1 : f;
        }
    }

    /// <summary><c>iround(v)</c>: <paramref name="v"/> rounded to the nearest <c>int</c>, halves away from zero.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The rounded value.</returns>
    /// <exception cref="NanoQLException"><paramref name="v"/> isn't finite, or its rounding isn't an <c>int</c>.</exception>
    internal static int IRound(double v)
    {
        double r = Round(v);

        // ldexp(1, std::numeric_limits<int>::digits), 2^31
        const double maxVal = 2147483648.0;
        if (r >= maxVal || r < -maxVal)
        {
            throw RoundingError("boost::math::iround<long double>(long double)", v);
        }

        return (int)r;
    }

    /// <summary>
    /// <c>llround(v)</c>: <paramref name="v"/> rounded to the nearest <c>long long</c>, halves away from zero.
    /// </summary>
    /// <param name="v">The value.</param>
    /// <returns>The rounded value.</returns>
    /// <exception cref="NanoQLException"><paramref name="v"/> isn't finite, or its rounding isn't a
    /// <c>long long</c>.</exception>
    internal static long LLRound(double v)
    {
        double r = Round(v);

        // ldexp(1, std::numeric_limits<long long>::digits), 2^63
        const double maxVal = 9223372036854775808.0;
        if (r >= maxVal || r < -maxVal)
        {
            throw RoundingError("boost::math::llround<long double>(long double)", v);
        }

        return (long)r;
    }

    // policies::raise_rounding_error under the default policy, with no message of its own
    internal static NanoQLException RoundingError(string function, double value) =>
        new($"Error in function {function}: Cause unknown: error caused by bad argument with value "
            + value.ToString(CultureInfo.InvariantCulture));
}
