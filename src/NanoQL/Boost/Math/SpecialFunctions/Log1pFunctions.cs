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
/// returns the C runtime's <c>std::log1p</c>; and <c>log1pmx</c>, ln(1 + x) − x, by its Taylor series near 0.
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

    /// <summary><c>log1pmx(x)</c>: ln(1 + <paramref name="x"/>) − <paramref name="x"/>.</summary>
    /// <param name="x">The value, at least −1.</param>
    /// <returns>ln(1 + x) − x.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is below −1.</exception>
    /// <exception cref="NanoQLException"><paramref name="x"/> is −1, whose logarithm overflows, or the series runs
    /// out of terms.</exception>
    internal static double Log1pmx(double x)
    {
        const string function = "boost::math::log1pmx<long double>(long double)";

        if (x < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Error in function {function}: log1pmx(x) requires x > -1, but got x = {x.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (x == -1)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        double a = Math.Abs(x);
        if (a > (double)0.95f)
        {
            return Math.Log(1 + x) - x;
        }

        // Note that without numeric_limits specialisation support,
        // epsilon just returns zero, and our "optimisation" will always fail:
        if (a < NumericLimits.QL_EPSILON)
        {
            return -x * x / 2;
        }

        var s = new Log1pSeries(x);
        s.Next();
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;

        double result = SeriesFunctions.SumSeries(ref s, NumericLimits.QL_EPSILON, ref maxIter, 0.0);

        SeriesFunctions.CheckSeriesIterations(function, maxIter);
        return result;
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

    // log1p_series: the Taylor series' terms (−1)^(k−1)·x^k/k
    private struct Log1pSeries(double x) : SeriesFunctions.ITerms
    {
        private readonly double mult = -x;
        private int k;
        private double prod = -1;

        public double Next()
        {
            prod *= mult;
            return prod / ++k;
        }
    }
}
