// SPDX-FileCopyrightText: (C) Copyright John Maddock 2005-2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/fraction.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's continued fractions (<c>fraction.hpp</c>): <c>continued_fraction_a</c> by the modified Lentz algorithm,
/// a generator returning each step's (a, b), the form the incomplete gamma reads.
/// </summary>
internal static class FractionFunctions
{
    // detail::tiny_value<double>: 16 times the smallest normal double
    private const double Tiny = 16 * 2.2250738585072014E-308;

    /// <summary>A continued fraction's terms, each call to <see cref="Next"/> the next (a, b): Boost's generator.</summary>
    internal interface IGenerator
    {
        /// <summary>The next pair.</summary>
        /// <returns>The pair.</returns>
        (double A, double B) Next();
    }

    /// <summary>
    /// <c>continued_fraction_a(g, factor, max_terms)</c>: a1/(b1 + a2/(b2 + a3/(b3 + …))), the first a and b both
    /// used.
    /// </summary>
    /// <typeparam name="TGenerator">The generator's type.</typeparam>
    /// <param name="g">The generator.</param>
    /// <param name="factor">The relative accuracy of the last step.</param>
    /// <param name="maxTerms">The most terms; on return, the number used.</param>
    /// <returns>The fraction.</returns>
    internal static double ContinuedFractionA<TGenerator>(ref TGenerator g, double factor, ref ulong maxTerms)
        where TGenerator : struct, IGenerator
    {
        double terminator = Math.Abs(factor);

        (double a, double b) v = g.Next();

        double f, c, d, delta, a0;
        f = v.b;
        a0 = v.a;
        if (f == 0)
        {
            f = Tiny;
        }

        c = f;
        d = 0;

        ulong counter = maxTerms;

        do
        {
            v = g.Next();
            d = v.b + (v.a * d);
            if (d == 0)
            {
                d = Tiny;
            }

            c = v.b + (v.a / c);
            if (c == 0)
            {
                c = Tiny;
            }

            d = 1 / d;
            delta = c * d;
            f *= delta;
        }
        while ((Math.Abs(delta - 1) > terminator) && (--counter != 0));

        maxTerms -= counter;

        return a0 / f;
    }

    /// <summary><c>continued_fraction_a(g, factor)</c>, with no limit on the terms.</summary>
    /// <typeparam name="TGenerator">The generator's type.</typeparam>
    /// <param name="g">The generator.</param>
    /// <param name="factor">The relative accuracy of the last step.</param>
    /// <returns>The fraction.</returns>
    internal static double ContinuedFractionA<TGenerator>(ref TGenerator g, double factor)
        where TGenerator : struct, IGenerator
    {
        ulong maxIter = ulong.MaxValue;
        return ContinuedFractionA(ref g, factor, ref maxIter);
    }
}
