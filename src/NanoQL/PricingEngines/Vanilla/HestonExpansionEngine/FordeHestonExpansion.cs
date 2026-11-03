// SPDX-FileCopyrightText: Copyright (C) 2014 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/hestonexpansionengine.hpp ql/pricingengines/vanilla/hestonexpansionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>Forde, Jacquier and Lee's small-time expansion of the Heston model's implied variance.</summary>
/// <remarks>
/// Reference: M. Forde, A. Jacquier, R. Lee, The small-time smile and term structure of implied volatility under the
/// Heston model, SIAM Journal on Financial Mathematics, 2012.
/// </remarks>
public sealed class FordeHestonExpansion : HestonExpansion
{
    private readonly double c0, c1, c2, c3, c4;

    /// <summary>Initializes a new instance of the <see cref="FordeHestonExpansion"/> class: the coefficients for one
    /// term.</summary>
    /// <param name="kappa">The variance's mean-reversion speed.</param>
    /// <param name="theta">The variance's long-run mean.</param>
    /// <param name="sigma">The volatility of the variance.</param>
    /// <param name="v0">The initial variance.</param>
    /// <param name="rho">The correlation.</param>
    /// <param name="term">The term.</param>
    public FordeHestonExpansion(double kappa, double theta, double sigma, double v0, double rho, double term)
    {
        (double sigma00, double sigma01, double sigma02) = LeadingOrder(sigma, v0, rho);
        double rhoBarSquare = 1 - (rho * rho);
        double a00 = (-sigma * sigma / 12 * (1 - (rho * rho / 4))) + (v0 * rho * sigma / 4) + (kappa / 2 * (theta - v0));
        double a01 = rho * sigma / (24 * v0) * ((sigma * sigma * rhoBarSquare) - (2 * kappa * (theta + v0)) + (v0 * rho * sigma)); // term in x
        double a02 = ((176 * sigma * sigma) - (480 * kappa * theta) - (712 * rho * rho * sigma * sigma)
                      + (521 * rho * rho * rho * rho * sigma * sigma) + (40 * sigma * rho * rho * rho * v0)
                      + (1040 * kappa * theta * rho * rho) - (80 * v0 * kappa * rho * rho)) * sigma * sigma / (v0 * v0 * 7680);
        c0 = (sigma00 * sigma00) + (a00 * term);
        c1 = (sigma00 * sigma01 * 2) + (a01 * term);
        c2 = (sigma00 * sigma02 * 2) + (sigma01 * sigma01) + (a02 * term);
        c3 = sigma01 * sigma02 * 2;
        c4 = sigma02 * sigma02;
    }

    /// <inheritdoc/>
    public override double ImpliedVolatility(double strike, double forward)
    {
        double x = Math.Log(strike / forward);
        double variance = c0 + (x * (c1 + (x * (c2 + (x * (c3 + (x * c4)))))));
        variance = Max(1e-8, variance);
        return Math.Sqrt(variance);
    }

    // The volatility's leading order in x: sqrt(v0), the term in x and the term in x*x, QuantLib's sigma00, sigma01 and
    // sigma02, which LPP2's and LPP3's first three coefficients tend to as the term falls to 0
    internal static (double Sigma00, double Sigma01, double Sigma02) LeadingOrder(double sigma, double v0, double rho)
    {
        double v0Sqrt = Math.Sqrt(v0);
        double sigma00 = v0Sqrt;
        double sigma01 = v0Sqrt * (rho * sigma / (4 * v0)); // term in x
        double sigma02 = v0Sqrt * ((1 - (5 * rho * rho / 2)) / 24 * sigma * sigma / (v0 * v0)); // term in x*x
        return (sigma00, sigma01, sigma02);
    }

    // std::max, which returns its first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;
}
