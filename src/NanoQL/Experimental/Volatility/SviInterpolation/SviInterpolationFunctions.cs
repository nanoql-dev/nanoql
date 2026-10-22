// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sviinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NanoQL.Experimental;

/// <summary>QuantLib's SVI free functions: the parameters' check and the total variance.</summary>
[Experimental("NQL0001")]
public static class SviInterpolationFunctions
{
    /// <summary>Checks SVI's parameters, as QuantLib's <c>checkSviParameters</c> does.</summary>
    /// <param name="a">The level, a.</param>
    /// <param name="b">The slope, b.</param>
    /// <param name="sigma">The smoothness, σ.</param>
    /// <param name="rho">The correlation, ρ.</param>
    /// <param name="m">The shift, m; not checked.</param>
    /// <param name="tte">The time to expiry; not checked.</param>
    /// <exception cref="ArgumentException">b is negative, |ρ| isn't below 1, σ isn't positive, a + bσ√(1−ρ²) is
    /// negative, or b(1+|ρ|) is above 4.</exception>
    public static void CheckSviParameters(double a, double b, double sigma, double rho, double m, double tte)
    {
        if (!(b >= 0.0))
        {
            throw new ArgumentException($"b ({Show(b)}) must be non negative", nameof(b));
        }

        if (!(Math.Abs(rho) < 1.0))
        {
            throw new ArgumentException($"rho ({Show(rho)}) must be in (-1,1)", nameof(rho));
        }

        if (!(sigma > 0.0))
        {
            throw new ArgumentException($"sigma ({Show(sigma)}) must be positive", nameof(sigma));
        }

        if (!(a + (b * sigma * Math.Sqrt(1.0 - (rho * rho))) >= 0.0))
        {
            throw new ArgumentException(
                $"a + b sigma sqrt(1-rho^2) (a={Show(a)}, b={Show(b)}, sigma={Show(sigma)}, rho={Show(rho)}) must be non negative",
                nameof(a));
        }

        if (!(b * (1.0 + Math.Abs(rho)) <= 4.0))
        {
            throw new ArgumentException(
                $"b(1+|rho|) must be less than or equal to 4, (b={Show(b)}, rho={Show(rho)})", nameof(b));
        }
    }

    /// <summary>SVI's total variance at a log-moneyness: a + b(ρ(k − m) + √((k − m)² + σ²)).</summary>
    /// <param name="a">The level, a.</param>
    /// <param name="b">The slope, b.</param>
    /// <param name="sigma">The smoothness, σ.</param>
    /// <param name="rho">The correlation, ρ.</param>
    /// <param name="m">The shift, m.</param>
    /// <param name="k">The log-moneyness, ln(K/F).</param>
    /// <returns>The total variance.</returns>
    public static double SviTotalVariance(double a, double b, double sigma, double rho, double m, double k) =>
        a + (b * ((rho * (k - m)) + Math.Sqrt(((k - m) * (k - m)) + (sigma * sigma))));

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
