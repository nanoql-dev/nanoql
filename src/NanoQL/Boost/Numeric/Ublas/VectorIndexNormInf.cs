// SPDX-FileCopyrightText: Copyright (c) 2000-2009 Joerg Walter, Mathias Koch, Gunter Winkler
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/numeric/ublas/functional.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// uBLAS's <c>vector_index_norm_inf</c> (<c>functional.hpp</c>): the index of a vector's element of greatest magnitude,
/// the pivot search of <see cref="LuFunctions.LuFactorize"/>.
/// </summary>
internal static class VectorIndexNormInf
{
    /// <summary>
    /// apply (e): the index of the first element whose magnitude strictly exceeds every earlier one's, starting from
    /// zero. A tie keeps the earlier element, a NaN never wins, and a vector whose elements are all zero or NaN, or an
    /// empty one, gives 0.
    /// </summary>
    /// <param name="e">The storage from the vector's first element; element k is at <c>k * stride</c>.</param>
    /// <param name="size">The vector's size.</param>
    /// <param name="stride">The distance between successive elements: a matrix's column count, for a column.</param>
    /// <returns>The index.</returns>
    internal static int Apply(ReadOnlySpan<double> e, int size, int stride)
    {
        // ISSUE For CBLAS compatibility return 0 index in empty case
        int iNormInf = 0;
        double t = 0.0;
        for (int i = 0; i < size; ++i)
        {
            // type_traits<double>::norm_inf: the absolute value
            double u = Math.Abs(e[i * stride]);
            if (u > t)
            {
                iNormInf = i;
                t = u;
            }
        }

        return iNormInf;
    }
}
