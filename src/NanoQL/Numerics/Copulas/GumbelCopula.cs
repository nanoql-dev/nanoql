// SPDX-FileCopyrightText: Copyright (C) 2008 Marek Glowacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/gumbelcopula.hpp ql/math/copulas/gumbelcopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Gumbel copula, C(x, y) = exp(−((−ln x)^θ + (−ln y)^θ)^(1/θ)).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does. Above θ 19, where (−ln x)^θ can underflow (from θ 19.3 at the
/// largest x below 1, so QuantLib's is 1e-8 off at θ 50 and reads C(0.9, 0.7) as 1 for 0.7 at θ 1000), the larger
/// logarithm M is factored out, exp(−M(1 + r^θ)^(1/θ)) with r the ratio of the two; QuantLib's formula to θ 19, where
/// no power leaves the doubles' normal range.
/// </remarks>
public sealed class GumbelCopula
{
    private readonly double theta;

    /// <summary>Initializes a new instance of the <see cref="GumbelCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, at least 1.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public GumbelCopula(double theta)
    {
        if (!(theta >= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be greater or equal to 1"),
                nameof(theta));
        }

        this.theta = theta;
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

        if (theta <= 19.0)
        {
            return Math.Exp(-Math.Pow(Math.Pow(-Math.Log(x), theta) + Math.Pow(-Math.Log(y), theta), 1 / theta));
        }

        double a = -Math.Log(x), b = -Math.Log(y);
        double mMax = Math.Max(a, b), r = Math.Min(a, b) / Math.Max(a, b);
        return Math.Exp(-mMax * Math.Pow(1.0 + Math.Pow(r, theta), 1.0 / theta));
    }
}
