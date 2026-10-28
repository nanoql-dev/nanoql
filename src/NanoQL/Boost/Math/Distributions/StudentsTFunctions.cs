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
/// QuantLib passes.
/// </summary>
internal static class StudentsTFunctions
{
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
