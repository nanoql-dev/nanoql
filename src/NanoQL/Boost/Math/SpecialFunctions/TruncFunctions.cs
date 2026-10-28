// SPDX-FileCopyrightText: Copyright John Maddock 2007.
// SPDX-FileCopyrightText: Copyright Matt Borland 2023.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/trunc.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's truncation towards zero (<c>trunc.hpp</c>): <c>trunc</c>, and <c>itrunc</c> and <c>lltrunc</c>, which
/// convert to <c>int</c> and <c>long long</c>.
/// </summary>
/// <remarks>
/// Under the default policy a value that isn't finite, or whose truncation lies outside the target type, raises
/// Boost's rounding error, thrown as <see cref="NanoQLException"/> with Boost's message; the names carry
/// <c>long double</c>, as at <see cref="RoundFunctions"/>.
/// </remarks>
internal static class TruncFunctions
{
    /// <summary><c>trunc(v)</c>: <paramref name="v"/> truncated towards zero.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The truncated value.</returns>
    /// <exception cref="NanoQLException"><paramref name="v"/> isn't finite.</exception>
    internal static double Trunc(double v)
    {
        if (!double.IsFinite(v))
        {
            throw RoundFunctions.RoundingError("boost::math::trunc<long double>(long double)", v);
        }

        return (v >= 0) ? Math.Floor(v) : Math.Ceiling(v);
    }

    /// <summary><c>itrunc(v)</c>: <paramref name="v"/> truncated towards zero to an <c>int</c>.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The truncated value.</returns>
    /// <exception cref="NanoQLException"><paramref name="v"/> isn't finite, or its truncation isn't an
    /// <c>int</c>.</exception>
    internal static int ITrunc(double v)
    {
        double r = Trunc(v);

        // ldexp(1, std::numeric_limits<int>::digits), 2^31
        const double maxVal = 2147483648.0;
        if (r >= maxVal || r < -maxVal)
        {
            throw RoundFunctions.RoundingError("boost::math::itrunc<long double>(long double)", v);
        }

        return (int)r;
    }

    /// <summary><c>lltrunc(v)</c>: <paramref name="v"/> truncated towards zero to a <c>long long</c>.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The truncated value.</returns>
    /// <exception cref="NanoQLException"><paramref name="v"/> isn't finite, or its truncation isn't a
    /// <c>long long</c>.</exception>
    internal static long LLTrunc(double v)
    {
        double r = Trunc(v);

        // ldexp(1, std::numeric_limits<long long>::digits), 2^63
        const double maxVal = 9223372036854775808.0;
        if (r >= maxVal || r < -maxVal)
        {
            throw RoundFunctions.RoundingError("boost::math::lltrunc<long double>(long double)", v);
        }

        return (long)r;
    }
}
