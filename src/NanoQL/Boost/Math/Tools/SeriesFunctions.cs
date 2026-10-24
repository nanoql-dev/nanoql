// SPDX-FileCopyrightText: (C) Copyright John Maddock 2005-2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/series.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's series summation (<c>series.hpp</c>): <c>sum_series</c>, which adds a functor's terms until the next
/// is below the factor times the sum, and <c>policies::check_series_iterations</c>, its default policy's check, which
/// throws Boost's evaluation error when the series runs out of terms.
/// </summary>
internal static class SeriesFunctions
{
    /// <summary><c>policies::get_max_series_iterations</c> under the default policy.</summary>
    internal const ulong MaxSeriesIterations = 1000000;

    /// <summary>A series' terms, each call to <see cref="Next"/> the next: Boost's functor.</summary>
    internal interface ITerms
    {
        /// <summary>The next term.</summary>
        /// <returns>The term.</returns>
        double Next();
    }

    /// <summary><c>sum_series(func, factor, max_terms, init_value)</c>.</summary>
    /// <typeparam name="TTerms">The series' type.</typeparam>
    /// <param name="func">The series.</param>
    /// <param name="factor">The relative size of the last term.</param>
    /// <param name="maxTerms">The most terms; on return, the number of terms summed.</param>
    /// <param name="initValue">The sum's initial value.</param>
    /// <returns>The sum.</returns>
    internal static double SumSeries<TTerms>(ref TTerms func, double factor, ref ulong maxTerms, double initValue)
        where TTerms : struct, ITerms
    {
        ulong counter = maxTerms;

        double result = initValue;
        double nextTerm;
        do
        {
            nextTerm = func.Next();
            result += nextTerm;
        }
        while ((Math.Abs(factor * result) < Math.Abs(nextTerm)) && (--counter != 0));

        // set max_terms to the actual number of terms of the series evaluated:
        maxTerms -= counter;

        return result;
    }

    /// <summary><c>policies::check_series_iterations</c>: refuses a series that ran out of terms.</summary>
    /// <param name="function">Boost's name of the calling function.</param>
    /// <param name="maxIter">The number of terms summed.</param>
    /// <exception cref="NanoQLException">The series used every term it was allowed.</exception>
    internal static void CheckSeriesIterations(string function, ulong maxIter)
    {
        if (maxIter >= MaxSeriesIterations)
        {
            throw new NanoQLException(
                $"Error in function {function}: Series evaluation exceeded {maxIter.ToString(CultureInfo.InvariantCulture)} "
                + "iterations, giving up now.");
        }
    }
}
