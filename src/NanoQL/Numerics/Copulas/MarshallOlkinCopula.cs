// SPDX-FileCopyrightText: Copyright (C) 2008 Marek Glowacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/marshallolkincopula.hpp ql/math/copulas/marshallolkincopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Marshall-Olkin copula, C(x, y) = min(y·x^(1 − a₁), x·y^(1 − a₂)).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does (the S-core plan's CR7 (xxxiii)). Its parameters are refused
/// above 1, where QuantLib's accepts them and returns values above min(x, y), the Fréchet-Hoeffding bound every copula
/// meets: at a₁ 2, C(0.2, 0.9) 0.222 (the plan's CR7 (xxxii)).
/// </remarks>
public sealed class MarshallOlkinCopula
{
    private readonly double a1;
    private readonly double a2;

    /// <summary>Initializes a new instance of the <see cref="MarshallOlkinCopula"/> class.</summary>
    /// <param name="a1">The first parameter, in [0, 1].</param>
    /// <param name="a2">The second parameter, in [0, 1].</param>
    /// <exception cref="ArgumentException">A parameter is outside [0, 1].</exception>
    public MarshallOlkinCopula(double a1, double a2)
    {
        if (!(a1 >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"1st parameter ({a1}) must be non-negative"), nameof(a1));
        }

        if (!(a2 >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"2nd parameter ({a2}) must be non-negative"), nameof(a2));
        }

        if (a1 > 1.0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"1st parameter ({a1}) must not exceed 1"), nameof(a1));
        }

        if (a2 > 1.0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"2nd parameter ({a2}) must not exceed 1"), nameof(a2));
        }

        this.a1 = 1.0 - a1;
        this.a2 = 1.0 - a2;
    }

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

        return Math.Min(y * Math.Pow(x, a1), x * Math.Pow(y, a2));
    }
}
