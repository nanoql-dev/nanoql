// SPDX-FileCopyrightText: Copyright (C) 2008 Marek Glowacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/mincopula.hpp ql/math/copulas/mincopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The min copula, C(x, y) = max(x + y − 1, 0), the Fréchet-Hoeffding lower bound.</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does; this one's formula meets them.
/// </remarks>
public sealed class MinCopula
{
    /// <summary>The copula's value.</summary>
    /// <param name="x">The first argument, in [0, 1].</param>
    /// <param name="y">The second argument, in [0, 1].</param>
    /// <returns>C(x, y).</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> or <paramref name="y"/> is outside [0, 1].</exception>
    public double Evaluate(double x, double y)
    {
        if (!(x >= 0.0 && x <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"1st argument ({x}) must be in [0,1]"), nameof(x));
        }

        if (!(y >= 0.0 && y <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"2nd argument ({y}) must be in [0,1]"), nameof(y));
        }

        if (x == 0.0 || y == 0.0)
        {
            return 0.0;
        }

        if (x == 1.0)
        {
            return y;
        }

        if (y == 1.0)
        {
            return x;
        }

        return Math.Max(x + y - 1.0, 0.0);
    }
}
