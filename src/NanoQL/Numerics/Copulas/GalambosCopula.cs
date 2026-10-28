// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/galamboscopula.hpp ql/math/copulas/galamboscopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Galambos copula, C(x, y) = xy·exp(((−ln x)^−θ + (−ln y)^−θ)^(−1/θ)).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does (the S-core plan's CR7 (xxxiii)): QuantLib's is NaN at (0, 0).
/// Above θ 19, where (−ln x)^−θ can overflow (from θ 19.3 at the largest x below 1, so QuantLib's is ∞ from θ about 50
/// on points 1e-8 from the edge), the smaller logarithm m is factored out, xy·exp(m(1 + r^θ)^(−1/θ)) with r the ratio
/// of the two (the plan's CR7 (xxi)); QuantLib's formula to θ 19, where no power leaves the doubles' normal range.
/// Where xy is below the normal range the product is formed last, and where e^(…) overflows (x and y both subnormal)
/// as e^(… + ln x + ln y), where QuantLib's is NaN.
/// </remarks>
public sealed class GalambosCopula
{
    private readonly double theta;

    /// <summary>Initializes a new instance of the <see cref="GalambosCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, at least 0.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public GalambosCopula(double theta)
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

        double a = -Math.Log(x), b = -Math.Log(y);
        if (theta <= 19.0)
        {
            return Product(x, y, a, b, Math.Pow(Math.Pow(a, -theta) + Math.Pow(b, -theta), -1 / theta));
        }

        double m = Math.Min(a, b), r = Math.Min(a, b) / Math.Max(a, b);
        return Product(x, y, a, b, m * Math.Pow(1.0 + Math.Pow(r, theta), -1.0 / theta));
    }

    // x·y·e^t, a = −ln x and b = −ln y: QuantLib's order where x·y is normal; below it, the product formed last, and
    // where e^t overflows, which only x and y both below the normal range reach (t is at most min(a, b)), e^(t − a − b),
    // where QuantLib's is 0·∞, NaN
    private static double Product(double x, double y, double a, double b, double t)
    {
        double xy = x * y;
        if (xy >= NumericLimits.QL_MIN_POSITIVE_REAL)
        {
            return xy * Math.Exp(t);
        }

        double e = Math.Exp(t);
        return double.IsFinite(e) ? x * (y * e) : Math.Exp(t - a - b);
    }
}
