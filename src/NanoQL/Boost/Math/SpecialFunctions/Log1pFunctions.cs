// SPDX-FileCopyrightText: (C) Copyright John Maddock 2005-2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/log1p.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>log1p</c> (<c>log1p.hpp</c>) for <c>double</c> under the default policy: the overload Boost
/// declares for <c>double</c>, which overload resolution prefers to the generic template, checks its argument and
/// returns the C runtime's <c>std::log1p</c>.
/// </summary>
internal static class Log1pFunctions
{
    /// <summary><c>log1p(x)</c>: the natural logarithm of 1 + <paramref name="x"/>, accurate for small <paramref name="x"/>.</summary>
    /// <param name="x">The value, at least −1.</param>
    /// <returns>ln(1 + x).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is below −1.</exception>
    /// <exception cref="NanoQLException"><paramref name="x"/> is −1, whose logarithm overflows.</exception>
    internal static double Log1p(double x)
    {
        // The name the overload for double gives Boost's errors, which leaves out the namespace.
        const string function = "log1p<double>(double)";

        if (x < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Error in function {function}: log1p(x) requires x > -1, but got x = {x.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (x == -1)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        return StdLog1p(x);
    }

    // std::log1p, which .NET doesn't provide (double.LogP1 computes Math.Log(1 + x), which loses x's low digits): the
    // logarithm of u = 1 + x, rounded, scaled by x/(u − 1), the exact ratio of x to the argument the logarithm saw, as
    // D. Goldberg, "What Every Computer Scientist Should Know About Floating-Point Arithmetic" (1991), Theorem 4, gives
    // it; within a few units in the last place wherever Math.Log is correctly rounded. A NaN and +∞ pass through.
    private static double StdLog1p(double x)
    {
        double u = 1.0 + x;
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return Math.Log(u);
        }

        return Math.Log(u) * x / (u - 1.0);
    }
}
