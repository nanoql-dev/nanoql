// SPDX-FileCopyrightText: Copyright (C) 2008 Marek Glowacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/frankcopula.hpp ql/math/copulas/frankcopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Frank copula, C(x, y) = −(1/θ)·ln(1 + (e^(−θx) − 1)(e^(−θy) − 1)/(e^(−θ) − 1)).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does. QuantLib's formula loses digits wherever the logarithm's
/// argument nears 1 (near the edges, about 1e-7 relative at every θ on points 1e-8 from them, and everywhere as θ nears
/// 0) and overflows from |θ| about 700; nanoql writes z = (e^(−θx) − 1)(e^(−θy) − 1)/(e^(−θ) − 1) with <c>expm1</c>,
/// the quotient first, and the logarithm with <c>log1p</c>. Above θ 0, where z falls to −1/2 or below, 1 + z is (A +
/// B)/(1 − e^(−θ)) with A = e^(−θx)(1 − e^(−θy)) and B = e^(−θy)(1 − e^(−θ(1 − y))), two positive terms, summed in
/// logarithms; below θ 0 the quotient takes the larger argument, at most 1, so it stays normal where the other factor
/// is small; and below θ −700 the logarithm of z is summed from its factors'. Within 6e-16 relative of a high-precision
/// evaluation of QuantLib's formula on points within 1e-12 of the edges at θ from 1e-8 to 2000, and within 2e-15 at θ
/// from −1e-8 to −30, the error growing as |θ|·ε below that (5.6e-14 at θ −700, 1.8e-13 at −2000), the rounding of θx's
/// own amplification.
/// </remarks>
public sealed class FrankCopula
{
    private readonly double theta;

    /// <summary>Initializes a new instance of the <see cref="FrankCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, not 0.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public FrankCopula(double theta)
    {
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

        if (theta > 0.0)
        {
            double z = Expm1Functions.StdExpm1(-theta * x)
                * (Expm1Functions.StdExpm1(-theta * y) / Expm1Functions.StdExpm1(-theta));
            if (z > -0.5)
            {
                return -Log1pFunctions.Log1p(z) / theta;
            }

            double logA = (-theta * x) + Math.Log(-Expm1Functions.StdExpm1(-theta * y));
            double logB = (-theta * y) + Math.Log(-Expm1Functions.StdExpm1(-theta * (1.0 - y)));
            return -(LogSum(logA, logB) - Log1pFunctions.Log1p(-Math.Exp(-theta))) / theta;
        }

        double s = -theta;
        if (s <= 700.0)
        {
            // the quotient with the larger argument, at most 1, so it stays normal where the other factor is small
            double z = (Expm1Functions.StdExpm1(s * Math.Max(x, y)) / Expm1Functions.StdExpm1(s))
                * Expm1Functions.StdExpm1(s * Math.Min(x, y));
            return Log1pFunctions.Log1p(z) / s;
        }

        double logZ = (s * (x + y - 1.0))
            + Math.Log(-Expm1Functions.StdExpm1(-s * x))
            + Math.Log(-Expm1Functions.StdExpm1(-s * y))
            - Math.Log(-Expm1Functions.StdExpm1(-s));
        return (logZ > 0.0
            ? logZ + Log1pFunctions.Log1p(Math.Exp(-logZ))
            : Log1pFunctions.Log1p(Math.Exp(logZ))) / s;
    }

    // ln(e^a + e^b), the larger factored out
    private static double LogSum(double a, double b)
    {
        double hi = Math.Max(a, b), lo = Math.Min(a, b);
        return hi + Log1pFunctions.Log1p(Math.Exp(lo - hi));
    }
}
