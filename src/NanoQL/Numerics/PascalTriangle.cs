// SPDX-FileCopyrightText: Copyright (C) 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/pascaltriangle.hpp ql/math/pascaltriangle.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>Pascal's triangle: the binomial coefficients C(n, k) of each order n.</summary>
/// <remarks>
/// QuantLib grows a static vector of rows on demand, unsynchronised, of <c>BigNatural</c>, a 32-bit <c>unsigned
/// long</c> under MSVC, so its rows wrap from order 35 on Windows (C(35, 17) 242,600,354 for 4,537,567,650), where
/// Linux's 64 bits are exact. Here the rows are 64-bit and computed once, to order 67, the last whose coefficients fit;
/// a higher order is refused.
/// </remarks>
public static class PascalTriangle
{
    /// <summary>The highest order whose coefficients fit 64 bits.</summary>
    public const int MaxOrder = 67;

    private static readonly ImmutableArray<ulong>[] Coefficients = Build();

    /// <summary>The coefficients C(n, 0) to C(n, n) of order n.</summary>
    /// <param name="order">The order n, from 0 to <see cref="MaxOrder"/>.</param>
    /// <returns>The n + 1 coefficients.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> is negative or above <see cref="MaxOrder"/>.
    /// </exception>
    public static ImmutableArray<ulong> Get(int order)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(order, MaxOrder);
        return Coefficients[order];
    }

    private static ImmutableArray<ulong>[] Build()
    {
        var rows = new ulong[MaxOrder + 1][];

        // order zero mandatory for bootstrap
        rows[0] = [1];
        rows[1] = [1, 1];
        rows[2] = [1, 2, 1];
        rows[3] = [1, 3, 3, 1];
        for (int order = 4; order <= MaxOrder; ++order)
        {
            // nextOrder
            var row = new ulong[order + 1];
            row[0] = row[order] = 1;
            for (int i = 1; i < (order / 2) + 1; ++i)
            {
                row[i] = row[order - i] = checked(rows[order - 1][i - 1] + rows[order - 1][i]);
            }

            rows[order] = row;
        }

        var result = new ImmutableArray<ulong>[MaxOrder + 1];
        for (int order = 0; order <= MaxOrder; ++order)
        {
            result[order] = [.. rows[order]];
        }

        return result;
    }
}
