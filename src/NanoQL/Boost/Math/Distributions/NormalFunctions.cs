// SPDX-FileCopyrightText: Copyright John Maddock 2006, 2007.
// SPDX-FileCopyrightText: Copyright Paul A. Bristow 2006, 2007.
// SPDX-FileCopyrightText: Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/distributions/normal.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using static NanoQL.Numerics.CommonErrorHandlingFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's normal distribution (<c>normal.hpp</c>) for <c>double</c> under the default policy: the constructor's
/// checks, <c>cdf</c> and <c>quantile</c>. Boost's <c>normal_distribution</c> holds only its mean and standard
/// deviation, so the functions take them. <c>constants::root_two&lt;double&gt;()</c> is <c>Math.Sqrt(2.0)</c>
/// exactly.
/// </summary>
internal static class NormalFunctions
{
    /// <summary>
    /// <c>normal_distribution</c>'s constructor: it holds its two parameters, which must be a finite mean and a
    /// positive, finite standard deviation.
    /// </summary>
    /// <param name="lMean">The mean.</param>
    /// <param name="sd">The standard deviation.</param>
    /// <exception cref="NanoQLException">The standard deviation isn't positive and finite, or the mean isn't finite.</exception>
    internal static void NormalDistribution(double lMean, double sd)
    {
        const string function = "boost::math::normal_distribution<double>::normal_distribution";

        CheckScale(function, sd);
        CheckLocation(function, lMean);
    }

    /// <summary><c>cdf(normal_distribution, x)</c>: the probability of a variate below <paramref name="x"/>.</summary>
    /// <param name="mean">The distribution's mean.</param>
    /// <param name="sd">The distribution's standard deviation.</param>
    /// <param name="x">The variate; ±∞ gives 0 or 1.</param>
    /// <returns>The cumulative probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is NaN.</exception>
    /// <exception cref="NanoQLException">The parameters are refused.</exception>
    internal static double Cdf(double mean, double sd, double x)
    {
        const string function = "boost::math::cdf(const normal_distribution<double>&, double)";

        CheckScale(function, sd);
        CheckLocation(function, mean);
        if (double.IsInfinity(x))
        {
            if (x < 0)
            {
                return 0; // -infinity
            }

            return 1; // + infinity
        }

        CheckX(function, x, nameof(x));
        double diff = (x - mean) / (sd * Math.Sqrt(2.0));
        double result = ErfFunctions.Erfc(-diff) / 2;
        return result;
    }

    /// <summary><c>quantile(normal_distribution, p)</c>: the variate whose cumulative probability is <paramref name="p"/>.</summary>
    /// <param name="mean">The distribution's mean.</param>
    /// <param name="sd">The distribution's standard deviation.</param>
    /// <param name="p">The probability.</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="p"/> is outside [0, 1] or NaN.</exception>
    /// <exception cref="NanoQLException">The parameters are refused, or <paramref name="p"/> is 0 or 1 (an overflow).</exception>
    internal static double Quantile(double mean, double sd, double p)
    {
        const string function = "boost::math::quantile(const normal_distribution<double>&, double)";

        CheckScale(function, sd);
        CheckLocation(function, mean);
        CheckProbability(function, p, nameof(p));

        double result = ErfInvFunctions.ErfcInv(2 * p);
        result = -result;
        result *= sd * Math.Sqrt(2.0);
        result += mean;
        return result;
    }
}
