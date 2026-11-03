// SPDX-FileCopyrightText: Copyright John Maddock 2006.
// SPDX-FileCopyrightText: Copyright Paul A. Bristow 2006, 2012, 2017.
// SPDX-FileCopyrightText: Copyright Thomas Mang 2012.
// SPDX-FileCopyrightText: Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/distributions/students_t.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's Student's t distribution (<c>students_t.hpp</c>) for <c>double</c> under the default policy: its
/// <c>quantile</c>, on the fast method (<see cref="TDistributionInvFunctions"/>), for the integer degrees of freedom
/// QuantLib passes, and its <c>cdf</c> and <c>pdf</c>, which QuantLib's T copula policy reads.
/// </summary>
internal static class StudentsTFunctions
{
    // 1/eps for 64-bit double: above it the normal distribution answers.
    private const double NormalLimit = 4503599627370496.0;

    /// <summary><c>pdf(students_t_distribution&lt;double&gt;(df), x)</c>: Student's t density.</summary>
    /// <param name="df">The degrees of freedom, positive or +∞.</param>
    /// <param name="x">The random variate, which may be infinite.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is NaN.</exception>
    /// <exception cref="NanoQLException"><paramref name="df"/> isn't positive.</exception>
    internal static double Pdf(double df, double x)
    {
        const string function = "boost::math::pdf(const students_t_distribution<double>&, double)";
        CommonErrorHandlingFunctions.CheckXNotNaN(function, x, nameof(x));

        // Check that df > 0 or == +infinity.
        CommonErrorHandlingFunctions.CheckDfGt0ToInf(function, df);

        double result;
        if (double.IsInfinity(x))
        {
            // - or +infinity.
            return 0;
        }

        if (df > NormalLimit)
        {
            // Special case for really big degrees_of_freedom > 1 / eps
            // - use normal distribution which is much faster and more accurate.
            result = NormalFunctions.Pdf(0, 1, x);
        }
        else
        {
            double basem1 = x * x / df;
            if (basem1 < 0.125)
            {
                result = Math.Exp(-Log1pFunctions.Log1p(basem1) * (1 + df) / 2);
            }
            else
            {
                result = Math.Pow(1 / (1 + basem1), (df + 1) / 2);
            }

            result /= Math.Sqrt(df) * IncompleteBetaFunctions.Beta(df / 2, 0.5);
        }

        return result;
    }

    /// <summary><c>cdf(students_t_distribution&lt;double&gt;(df), x)</c>: Student's t cumulative distribution.</summary>
    /// <param name="df">The degrees of freedom, positive or +∞.</param>
    /// <param name="x">The random variate, which may be infinite.</param>
    /// <returns>The probability of a variate at or below <paramref name="x"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is NaN.</exception>
    /// <exception cref="NanoQLException"><paramref name="df"/> isn't positive.</exception>
    internal static double Cdf(double df, double x)
    {
        const string function = "boost::math::cdf(const students_t_distribution<double>&, double)";

        // degrees_of_freedom > 0 or infinity check:
        CommonErrorHandlingFunctions.CheckDfGt0ToInf(function, df);

        // Check for bad x first.
        CommonErrorHandlingFunctions.CheckXNotNaN(function, x, nameof(x));
        if (x == 0)
        {
            // Special case with exact result.
            return 0.5;
        }

        if (double.IsInfinity(x))
        {
            // x == - or + infinity, regardless of df.
            return x < 0 ? 0 : 1;
        }

        if (df > NormalLimit)
        {
            // Special case for really big degrees_of_freedom > 1 / eps (perhaps infinite?)
            // - use normal distribution which is much faster and more accurate.
            return NormalFunctions.Cdf(0, 1, x);
        }

        // normal df case.
        //
        // Calculate probability of Student's t using the incomplete beta function.
        // probability = ibeta(degrees_of_freedom / 2, 1/2, degrees_of_freedom / (degrees_of_freedom + t*t))
        //
        // However when t is small compared to the degrees of freedom, that formula
        // suffers from rounding error, use the identity formula to work around
        // the problem:
        //
        // I[x](a,b) = 1 - I[1-x](b,a)
        //
        // and:
        //
        //     x = df / (df + t^2)
        //
        // so:
        //
        // 1 - x = t^2 / (df + t^2)
        //
        double x2 = x * x;
        double probability;
        if (df > 2 * x2)
        {
            double z = x2 / (df + x2);
            probability = IncompleteBetaFunctions.Ibetac(0.5, df / 2, z) / 2;
        }
        else
        {
            double z = df / (df + x2);
            probability = IncompleteBetaFunctions.Ibeta(df / 2, 0.5, z) / 2;
        }

        return x > 0 ? 1 - probability : probability;
    }

    /// <summary><c>quantile(students_t_distribution&lt;double&gt;(df), p)</c>: Student's t quantile.</summary>
    /// <param name="df">The degrees of freedom, an integer of at least 1.</param>
    /// <param name="p">The probability, in [0, 1].</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="p"/> is outside [0, 1], or <paramref name="df"/> isn't an integer of at least 1.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// <paramref name="df"/> isn't positive, or <paramref name="p"/> is 0 or 1, where the quantile is infinite.
    /// </exception>
    internal static double Quantile(double df, double p)
    {
        const string function = "boost::math::quantile(const students_t_distribution<double>&, double)";

        // Check for domain errors:
        CommonErrorHandlingFunctions.CheckDfGt0ToInf(function, df);
        CommonErrorHandlingFunctions.CheckProbability(function, p, nameof(p));

        // Special cases, regardless of degrees_of_freedom.
        if (p == 0 || p == 1)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        if (p == 0.5)
        {
            return 0;
        }

        //
        // Depending on how many digits RealType has, this may forward
        // to the incomplete beta inverse as above.  Otherwise uses a
        // faster method that is accurate to ~15 digits everywhere
        // and a couple of epsilon at double precision and in the central
        // region where most use cases will occur...
        //
        return TDistributionInvFunctions.FastStudentsTQuantile(df, p);
    }
}
