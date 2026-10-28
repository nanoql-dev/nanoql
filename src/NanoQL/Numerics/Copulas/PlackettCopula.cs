// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/plackettcopula.hpp ql/math/copulas/plackettcopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Plackett copula, C(x, y) = (s − √(s² − 4xyθ(θ − 1)))/(2(θ − 1)), s = 1 + (θ − 1)(x + y).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does (the S-core plan's CR7 (xxxiii)). QuantLib's formula cancels
/// wherever the root nears s: near the edges at every θ (1e-7 relative at θ 0.5 and 2 on points 1e-8 from them) and
/// everywhere as θ nears 1 (9.5e-5). nanoql takes the stable root, 2xyθ/(s + √…) where s is positive, QuantLib's form
/// elsewhere, and above θ 1 the radicand as 1 + 2(θ − 1)(x(1 − y) + y(1 − x)) + ((θ − 1)(x − y))², whose terms are all
/// non-negative, where QuantLib's cancels as θ grows (1e-8 at θ 1e8) (the plan's CR7 (xxi), its stable root): within
/// 4e-16 relative of a high-precision evaluation of QuantLib's formula on points within 1e-12 of the edges, from θ 0.1
/// to 1e12; toward θ 0 the copula nears max(x + y − 1, 0), whose cancellation near x + y = 1 no form avoids.
/// </remarks>
public sealed class PlackettCopula
{
    private readonly double theta;

    /// <summary>Initializes a new instance of the <see cref="PlackettCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, at least 0, not 1.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public PlackettCopula(double theta)
    {
        if (!(theta >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be greater or equal to 0"),
                nameof(theta));
        }

        if (theta == 1.0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be different from 1"),
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

        double s = 1.0 + ((theta - 1.0) * (x + y));
        double r = theta > 1.0
            ? 1.0 + (2.0 * (theta - 1.0) * ((x * (1.0 - y)) + (y * (1.0 - x)))) + Squared((theta - 1.0) * (x - y))
            : (s * s) - (4.0 * x * y * theta * (theta - 1.0));
        double d = Math.Sqrt(r);
        return s > 0.0 ? 2.0 * x * y * theta / (s + d) : (s - d) / (2.0 * (theta - 1.0));
    }

    private static double Squared(double v) => v * v;
}
