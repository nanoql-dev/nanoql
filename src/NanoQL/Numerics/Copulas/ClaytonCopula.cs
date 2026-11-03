// SPDX-FileCopyrightText: Copyright (C) 2008 Marek Glowacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/claytoncopula.hpp ql/math/copulas/claytoncopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Clayton copula, C(x, y) = max(x^−θ + y^−θ − 1, 0)^(−1/θ).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and
/// C(1, v) = v, before its formula, as every copula here does.
/// <list type="bullet">
/// <item>Below θ 0 the base is taken at 0 before the power, the copula's definition, where QuantLib takes the maximum
/// after it, so a negative base gives NaN or, at an even exponent, a positive value: at θ −0.5, C(0.2, 0.2) 0 for
/// QuantLib's 0.0111.</item>
/// <item>Near independence, |θ| below 0.1, the base is computed as (x^−θ − 1) + (y^−θ − 1) by <c>expm1</c> and the
/// power by <c>log1p</c>, where QuantLib's cancels (4e-8 relative at θ 1e-8, on points within 1e-8 of the edges);
/// above θ 0.1 QuantLib's formula, but where x^−θ or y^−θ would overflow (θ·(−ln min(x, y)) above 709, where
/// QuantLib's gives 0): there m(1 + (m/M)^θ − m^θ)^(−1/θ), with m and M the smaller and larger argument. Each form is
/// within 5e-15 relative of a 50-digit evaluation of QuantLib's formula on points within 1e-12 of the edges; at θ −1
/// the copula is max(x + y − 1, 0), whose cancellation near x + y = 1 no form avoids.</item>
/// </list>
/// </remarks>
public sealed class ClaytonCopula
{
    private readonly double theta;

    /// <summary>Initializes a new instance of the <see cref="ClaytonCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, at least −1, not 0.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public ClaytonCopula(double theta)
    {
        if (!(theta >= -1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be greater or equal to -1"),
                nameof(theta));
        }

        if (theta == 0.0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be different from 0"),
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

        if (Math.Abs(theta) < 0.1)
        {
            // x^-theta + y^-theta - 2, then its log1p
            double b = Expm1Functions.StdExpm1(-theta * Math.Log(x)) + Expm1Functions.StdExpm1(-theta * Math.Log(y));
            return b <= -1.0 ? 0.0 : Math.Exp(-Log1pFunctions.Log1p(b) / theta);
        }

        double m = Math.Min(x, y), mMax = Math.Max(x, y);
        if (theta > 0.0 && theta * -Math.Log(m) > 709.0)
        {
            return m * Math.Pow(1.0 + Math.Pow(m / mMax, theta) - Math.Pow(m, theta), -1.0 / theta);
        }

        double base_ = Math.Pow(x, -theta) + Math.Pow(y, -theta) - 1.0;
        return Math.Pow(Math.Max(base_, 0.0), -1.0 / theta);
    }
}
