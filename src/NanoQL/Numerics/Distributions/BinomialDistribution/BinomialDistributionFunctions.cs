// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/binomialdistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The binomial coefficients and Peizer-Pratt's second inversion.</summary>
public static class BinomialDistributionFunctions
{
    /// <summary>ln C(n, k), through the factorials' logarithms.</summary>
    /// <param name="n">The number of trials, in [0, 2³² − 1] (QuantLib's 32-bit <c>BigNatural</c>).</param>
    /// <param name="k">The number of successes, in [0, n].</param>
    /// <returns>ln C(n, k).</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside [0, 2³² − 1].</exception>
    /// <exception cref="ArgumentException"><paramref name="k"/> is above <paramref name="n"/>.</exception>
    public static double BinomialCoefficientLn(long n, long k)
    {
        CheckBigNatural(n, nameof(n));
        CheckBigNatural(k, nameof(k));
        if (!(n >= k))
        {
            throw new ArgumentException("n<k not allowed", nameof(k));
        }

        return Factorial.Ln((uint)n) - Factorial.Ln((uint)k) - Factorial.Ln((uint)(n - k));
    }

    /// <summary>C(n, k), the exponential of its logarithm rounded to the nearest integer.</summary>
    /// <param name="n">The number of trials, in [0, 2³² − 1].</param>
    /// <param name="k">The number of successes, in [0, n].</param>
    /// <returns>C(n, k).</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside [0, 2³² − 1].</exception>
    /// <exception cref="ArgumentException"><paramref name="k"/> is above <paramref name="n"/>.</exception>
    public static double BinomialCoefficient(long n, long k) =>
        Math.Floor(0.5 + Math.Exp(BinomialCoefficientLn(n, k)));

    /// <summary>
    /// Given an odd integer n and a real number z, the p such that
    /// 1 − CumulativeBinomialDistribution((n − 1)/2, n, p) = CumulativeNormalDistribution(z).
    /// </summary>
    /// <param name="z">The normal deviate.</param>
    /// <param name="n">The number of trials, odd, in [0, 2³² − 1].</param>
    /// <returns>p.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is outside [0, 2³² − 1].</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is even.</exception>
    public static double PeizerPrattMethod2Inversion(double z, long n)
    {
        CheckBigNatural(n, nameof(n));
        if (!(n % 2 == 1))
        {
            throw new ArgumentException($"n must be an odd number: {n} not allowed", nameof(n));
        }

        double result = z / (n + (1.0 / 3.0) + (0.1 / (n + 1.0)));
        result *= result;
        result = Math.Exp(-result * (n + (1.0 / 6.0)));
        double root = Math.Sqrt(1.0 - result);

        // 0.5*(1.0-root) rewritten to keep its digits as root nears 1
        if (z > 0.0)
        {
            return 0.5 * (1.0 + root);
        }
        else
        {
            return 0.5 * result / (1.0 + root);
        }
    }

    // QuantLib's BigNatural, a 32-bit unsigned long under MSVC: a negative long reads as a large unsigned one.
    internal static void CheckBigNatural(long value, string paramName)
    {
        if ((ulong)value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "a value in [0, 4294967295] is required");
        }
    }
}
