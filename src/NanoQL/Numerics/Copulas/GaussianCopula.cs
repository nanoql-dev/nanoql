// SPDX-FileCopyrightText: Copyright (C) 2008 Marek Glowacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/gaussiancopula.hpp ql/math/copulas/gaussiancopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Gaussian copula, C(x, y) = Φ₂(Φ⁻¹(x), Φ⁻¹(y); ρ).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does: QuantLib's is NaN at (0, 0) and (1, 1) at ρ 0.5, at (0, 1) and
/// (1, 0) at −0.5, and 2e-10 off on the edges at 1, where the inverse normal's approximation ends.
/// </remarks>
public sealed class GaussianCopula
{
    private readonly BivariateCumulativeNormalDistributionWe04DP bivariateNormalCdf;
    private readonly InverseCumulativeNormal invCumNormal = default;

    /// <summary>Initializes a new instance of the <see cref="GaussianCopula"/> class.</summary>
    /// <param name="rho">The correlation ρ, in [−1, 1].</param>
    /// <exception cref="ArgumentException"><paramref name="rho"/> is outside [−1, 1].</exception>
    public GaussianCopula(double rho)
    {
        // the bivariate distribution's own check first, QuantLib's member order
        bivariateNormalCdf = new BivariateCumulativeNormalDistributionWe04DP(rho);
        if (!(rho >= -1.0 && rho <= 1.00))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"rho ({rho}) must be in [-1,1]"), nameof(rho));
        }
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

        return bivariateNormalCdf.Evaluate(invCumNormal.Evaluate(x), invCumNormal.Evaluate(y));
    }
}
