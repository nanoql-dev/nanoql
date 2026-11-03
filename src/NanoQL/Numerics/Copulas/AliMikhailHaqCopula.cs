// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/copulas/alimikhailhaqcopula.hpp ql/math/copulas/alimikhailhaqcopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Ali-Mikhail-Haq copula, C(x, y) = xy/(1 − θ(1 − x)(1 − y)).</summary>
/// <remarks>
/// Immutable. On the unit square's edges it returns the boundary values, C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v)
/// = v, before its formula, as every copula here does: QuantLib's is NaN at (0, 0) at θ 1. Where θ(1 − x)(1 − y)
/// exceeds 1/2 the denominator is computed as (1 − θ) + θ(x + y − xy), which doesn't cancel as θ nears 1 and x and y
/// near 0, where QuantLib's loses its digits (at θ 1, C(1e-14, 1e-14) 8e-4 off, and ∞ below about 1.1e-16); QuantLib's
/// formula elsewhere, so its bits at θ in [−1, 1/2]. In the rewritten region, where xy is below the normal range, the
/// product is formed last.
/// </remarks>
public sealed class AliMikhailHaqCopula
{
    private readonly double theta;

    /// <summary>Initializes a new instance of the <see cref="AliMikhailHaqCopula"/> class.</summary>
    /// <param name="theta">The parameter θ, in [−1, 1].</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range.</exception>
    public AliMikhailHaqCopula(double theta)
    {
        if (!(theta >= -1.0 && theta <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be in [-1,1]"),
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

        double c = theta * (1.0 - x) * (1.0 - y);
        if (c > 0.5)
        {
            double xy = x * y;
            double denominator = (1.0 - theta) + (theta * (x + y - xy));

            // x·y below the normal range: the product formed last, as the quotient y over the denominator is at most 1
            return xy >= NumericLimits.QL_MIN_POSITIVE_REAL ? xy / denominator : x * (y / denominator);
        }

        return (x * y) / (1.0 - c);
    }
}
