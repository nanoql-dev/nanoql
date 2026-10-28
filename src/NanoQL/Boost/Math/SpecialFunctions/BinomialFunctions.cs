// SPDX-FileCopyrightText: Copyright John Maddock 2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/binomial.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's binomial coefficient (<c>binomial.hpp</c>) for <c>double</c> under the default policy: by the
/// factorials' table to 170, by the beta function beyond, rounded to the nearest integer.
/// </summary>
internal static class BinomialFunctions
{
    /// <summary><c>binomial_coefficient&lt;double&gt;(n, k)</c>: the number of ways to choose k of n.</summary>
    /// <param name="n">The number of elements.</param>
    /// <param name="k">The number chosen, at most <paramref name="n"/>.</param>
    /// <returns>The coefficient.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> exceeds <paramref name="n"/>.</exception>
    /// <exception cref="NanoQLException">The coefficient overflows.</exception>
    internal static double BinomialCoefficient(uint n, uint k)
    {
        const string function = "boost::math::binomial_coefficient<double>(unsigned, unsigned)";
        if (k > n)
        {
            throw new ArgumentOutOfRangeException(
                nameof(k),
                $"Error in function {function}: The binomial coefficient is undefined for k > n, but got k = "
                + ((double)k).ToString(CultureInfo.InvariantCulture) + ".");
        }

        double result;
        if ((k == 0) || (k == n))
        {
            return 1;
        }

        if ((k == 1) || (k == n - 1))
        {
            return n;
        }

        if (n <= UncheckedFactorialFunctions.MaxFactorial)
        {
            // Use fast table lookup:
            result = UncheckedFactorialFunctions.UncheckedFactorial((int)n);
            result /= UncheckedFactorialFunctions.UncheckedFactorial((int)(n - k));
            result /= UncheckedFactorialFunctions.UncheckedFactorial((int)k);
        }
        else
        {
            // Use the beta function:
            if (k < n - k)
            {
                result = k * IncompleteBetaFunctions.Beta(k, n - k + 1);
            }
            else
            {
                result = (n - k) * IncompleteBetaFunctions.Beta(k + 1, n - k);
            }

            if (result == 0)
            {
                throw new NanoQLException($"Error in function {function}: Overflow Error");
            }

            result = 1 / result;
        }

        // convert to nearest integer:
        return Math.Ceiling(result - 0.5f);
    }
}
