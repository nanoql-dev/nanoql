// SPDX-FileCopyrightText: Copyright John Maddock 2006, 2007.
// SPDX-FileCopyrightText: Copyright Paul A. Bristow 2008, 2010.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/distributions/chi_squared.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;
using static NanoQL.Numerics.CommonErrorHandlingFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's chi-squared distribution (<c>chi_squared.hpp</c>) for <c>double</c> under the default policy: the
/// constructor's check, <c>pdf</c>, <c>cdf</c> and <c>quantile</c> with their complements, through the incomplete
/// gamma and its inverse. Boost's <c>chi_squared_distribution</c> holds only its degrees of freedom, so the functions
/// take them.
/// </summary>
/// <remarks>
/// The non-central chi-squared calls these in <c>long double</c>, so Boost's names there carry <c>long double</c>
/// where these carry <c>double</c>; its arguments pass every check here, so only a message raised deeper, a root
/// finder out of iterations, would show the difference.
/// </remarks>
internal static class ChiSquaredFunctions
{
    /// <summary>
    /// <c>chi_squared_distribution</c>'s constructor: it holds its degrees of freedom, which must be positive and
    /// finite.
    /// </summary>
    /// <param name="df">The degrees of freedom.</param>
    /// <exception cref="NanoQLException">The degrees of freedom are refused.</exception>
    internal static void ChiSquaredDistribution(double df) =>
        CheckDf("boost::math::chi_squared_distribution<double>::chi_squared_distribution", df);

    /// <summary><c>pdf(chi_squared_distribution, x)</c>: the density at <paramref name="chiSquare"/>.</summary>
    /// <param name="df">The distribution's degrees of freedom.</param>
    /// <param name="chiSquare">The variate, finite and not below 0.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chiSquare"/> is negative or isn't finite.</exception>
    /// <exception cref="NanoQLException">The degrees of freedom are refused, or the density at 0 is infinite (fewer than
    /// 2 degrees of freedom).</exception>
    internal static double Pdf(double df, double chiSquare)
    {
        const string function = "boost::math::pdf(const chi_squared_distribution<double>&, double)";

        CheckDf(function, df);
        CheckChiSquare(function, chiSquare);

        if (chiSquare == 0)
        {
            if (df < 2)
            {
                throw new NanoQLException($"Error in function {function}: Overflow Error");
            }
            else if (df == 2)
            {
                return 0.5;
            }
            else
            {
                return 0;
            }
        }

        return GammaFunctions.GammaPDerivative(df / 2, chiSquare / 2) / 2;
    }

    /// <summary>
    /// <c>cdf(chi_squared_distribution, x)</c>: the probability of a variate below <paramref name="chiSquare"/>.
    /// </summary>
    /// <param name="df">The distribution's degrees of freedom.</param>
    /// <param name="chiSquare">The variate, finite and not below 0.</param>
    /// <returns>P(df/2, x/2).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chiSquare"/> is negative or isn't finite.</exception>
    /// <exception cref="NanoQLException">The degrees of freedom are refused.</exception>
    internal static double Cdf(double df, double chiSquare)
    {
        const string function = "boost::math::cdf(const chi_squared_distribution<double>&, double)";

        CheckDf(function, df);
        CheckChiSquare(function, chiSquare);

        return GammaFunctions.GammaP(df / 2, chiSquare / 2);
    }

    /// <summary>
    /// <c>cdf(complement(chi_squared_distribution, x))</c>: the probability of a variate above
    /// <paramref name="chiSquare"/>.
    /// </summary>
    /// <param name="df">The distribution's degrees of freedom.</param>
    /// <param name="chiSquare">The variate, finite and not below 0.</param>
    /// <returns>Q(df/2, x/2).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chiSquare"/> is negative or isn't finite.</exception>
    /// <exception cref="NanoQLException">The degrees of freedom are refused.</exception>
    internal static double CdfComplement(double df, double chiSquare)
    {
        const string function = "boost::math::cdf(const chi_squared_distribution<double>&, double)";

        CheckDf(function, df);
        CheckChiSquare(function, chiSquare);

        return GammaFunctions.GammaQ(df / 2, chiSquare / 2);
    }

    /// <summary><c>quantile(chi_squared_distribution, p)</c>: the variate below which the probability is p.</summary>
    /// <param name="df">The distribution's degrees of freedom.</param>
    /// <param name="p">The probability, in [0, 1].</param>
    /// <returns>2·P⁻¹(df/2, p).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="p"/> is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">The degrees of freedom are refused; p is 1, an overflow; or the inverse
    /// fails.</exception>
    internal static double Quantile(double df, double p)
    {
        const string function = "boost::math::quantile(const chi_squared_distribution<double>&, double)";

        CheckDf(function, df);
        CheckProbability(function, p, nameof(p));

        return 2 * IgammaInverseFunctions.GammaPInv(df / 2, p);
    }

    /// <summary>
    /// <c>quantile(complement(chi_squared_distribution, q))</c>: the variate above which the probability is q.
    /// </summary>
    /// <param name="df">The distribution's degrees of freedom.</param>
    /// <param name="q">The probability, in [0, 1].</param>
    /// <returns>2·Q⁻¹(df/2, q).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="q"/> is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">The degrees of freedom are refused; q is 0, an overflow; or the inverse
    /// fails.</exception>
    internal static double QuantileComplement(double df, double q)
    {
        const string function = "boost::math::quantile(const chi_squared_distribution<double>&, double)";

        CheckDf(function, df);
        CheckProbability(function, q, nameof(q));

        return 2 * IgammaInverseFunctions.GammaQInv(df / 2, q);
    }

    // the variate's check in pdf and cdf, a domain error on the caller's argument
    private static void CheckChiSquare(string function, double chiSquare)
    {
        if ((chiSquare < 0) || !double.IsFinite(chiSquare))
        {
            throw new ArgumentOutOfRangeException(
                nameof(chiSquare),
                $"Error in function {function}: Chi Square parameter was "
                + $"{chiSquare.ToString(CultureInfo.InvariantCulture)}, but must be > 0 !");
        }
    }
}
