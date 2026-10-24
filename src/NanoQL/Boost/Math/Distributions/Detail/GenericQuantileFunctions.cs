// SPDX-FileCopyrightText: Copyright John Maddock 2008.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/distributions/detail/generic_quantile.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's quantile of a continuous distribution by root finding on its <c>cdf</c>
/// (<c>distributions/detail/generic_quantile.hpp</c>): <c>generic_quantile</c>, which brackets the root from a guess
/// and solves it with TOMS748 to 3 bits short of a <c>double</c>'s, and <c>check_range_result</c>, its treatment of
/// the range's ends at a probability of 0 or 1. A distribution here is its <c>cdf</c>, its complement and its range.
/// </summary>
/// <remarks>
/// Under the default policy an underflowing end is 0 (the policy ignores underflow); an infinite end is an overflow,
/// and a root not found within the root finder's 200 calls an evaluation error, each thrown as
/// <see cref="NanoQLException"/> with Boost's message.
/// </remarks>
internal static class GenericQuantileFunctions
{
    // tools::min_value<double>(), tools::max_value<double>()
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;

    /// <summary>
    /// <c>generic_quantile(dist, p, guess, comp, function)</c>: the variate at which the distribution's <c>cdf</c>
    /// (with <paramref name="comp"/>, its complement) is <paramref name="p"/>.
    /// </summary>
    /// <param name="cdf">The distribution's <c>cdf</c>.</param>
    /// <param name="cdfComplement">The distribution's complement <c>cdf</c>.</param>
    /// <param name="range">The distribution's range.</param>
    /// <param name="p">The probability.</param>
    /// <param name="guess">The first guess at the variate.</param>
    /// <param name="comp">Whether <paramref name="p"/> is the complement's probability.</param>
    /// <param name="function">Boost's name of the calling quantile, for its errors.</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="NanoQLException">The quantile is an infinite end of the range, or the root isn't found.</exception>
    internal static double GenericQuantile(
        Func<double, double> cdf,
        Func<double, double> cdfComplement,
        (double First, double Second) range,
        double p,
        double guess,
        bool comp,
        string function)
    {
        //
        // Special cases first:
        //
        if (p == 0)
        {
            return comp
                ? CheckRangeResult(range.Second, function)
                : CheckRangeResult(range.First, function);
        }

        if (p == 1)
        {
            return !comp
                ? CheckRangeResult(range.Second, function)
                : CheckRangeResult(range.First, function);
        }

        // generic_quantile_finder
        Func<double, double> f = comp
            ? x => p - cdfComplement(x)
            : x => cdf(x) - p;

        // eps_tolerance(policies::digits<double>() - 3)
        var tol = new Toms748SolveFunctions.EpsTolerance(53 - 3);
        ulong maxIter = RootsFunctions.MaxRootIterations;
        (double first, double second) = Toms748SolveFunctions.BracketAndSolveRoot(f, guess, 2, true, tol, ref maxIter);
        double result = first + (second - first) / 2;
        if (maxIter >= RootsFunctions.MaxRootIterations)
        {
            throw new NanoQLException(
                $"Error in function {function}: Unable to locate solution in a reasonable time: either there is no answer "
                + "to quantile or the answer is infinite.  Current best guess is "
                + result.ToString(CultureInfo.InvariantCulture));
        }

        return result;
    }

    // check_range_result: an end of the range as a quantile, 0 below the smallest normal value (the underflow ignored),
    // an overflow at an infinite or the largest end
    private static double CheckRangeResult(double x, string function)
    {
        if ((x >= 0) && (x < MinValue))
        {
            // raise_underflow_error: ignored by the default policy, which returns 0
            return 0;
        }

        if (x <= -MaxValue)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        if (x >= MaxValue)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        return x;
    }
}
