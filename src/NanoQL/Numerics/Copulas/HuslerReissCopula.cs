// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/huslerreisscopula.hpp ql/math/copulas/huslerreisscopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Hüsler-Reiss copula, C(x, y) = x^Φ(1/θ + θ/2·ln(ln x/ln y))·y^Φ(1/θ + θ/2·ln(ln y/ln x)).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does (the S-core plan's CR7 (xxxiii)): QuantLib's is NaN at (0, 0) and
/// wherever x or y is 1 but (1, 1), where −ln 1 is −0 and the ratio's logarithm that of −∞, and at θ 0, its own lower
/// bound, at every edge point but (1, 1).
/// </remarks>
public sealed class HuslerReissCopula
{
    private readonly double theta;
    private readonly CumulativeNormalDistribution cumNormal = new();

    /// <summary>Initializes a new instance of the <see cref="HuslerReissCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, at least 0.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public HuslerReissCopula(double theta)
    {
        if (!(theta >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be greater or equal to 0"),
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

        return Math.Pow(x, cumNormal.Evaluate((1.0 / theta) + (0.5 * theta * Math.Log(-Math.Log(x) / -Math.Log(y)))))
            * Math.Pow(y, cumNormal.Evaluate((1.0 / theta) + (0.5 * theta * Math.Log(-Math.Log(y) / -Math.Log(x)))));
    }
}
