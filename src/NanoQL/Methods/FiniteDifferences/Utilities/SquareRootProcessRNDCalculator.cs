// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/squarerootprocessrndcalculator.hpp ql/methods/finitedifferences/utilities/squarerootprocessrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The density of the square-root process dv = κ(θ − v)dt + σ√v dW at a time, a scaled non-central chi-squared
/// distribution, and its stationary density, a gamma distribution.
/// </summary>
/// <remarks>
/// Immutable. The stationary density is QuantLib's product β^α·v^{α−1}·e^{−βv − lnΓ(α)} wherever that product is
/// finite, its bits; past α of about 170 the product overflows into NaN, and there it is the same density in logarithms,
/// exp(α ln β + (α − 1) ln v − βv − lnΓ(α)).
/// </remarks>
public sealed class SquareRootProcessRNDCalculator : RiskNeutralDensityCalculator
{
    private readonly double v0, kappa, theta, d, df;

    /// <summary>Initializes a new instance of the <see cref="SquareRootProcessRNDCalculator"/> class.</summary>
    /// <param name="v0">The initial value.</param>
    /// <param name="kappa">The mean-reversion speed.</param>
    /// <param name="theta">The long-run mean.</param>
    /// <param name="sigma">The volatility.</param>
    public SquareRootProcessRNDCalculator(double v0, double kappa, double theta, double sigma)
    {
        this.v0 = v0;
        this.kappa = kappa;
        this.theta = theta;
        d = 4 * kappa / (sigma * sigma);
        df = d * theta;
    }

    /// <inheritdoc/>
    public override double Pdf(double v, double t)
    {
        (double k, double ncp) = Parameters(t);
        return NonCentralChiSquaredFunctions.Pdf(df, ncp, v * k) * k;
    }

    /// <inheritdoc/>
    public override double Cdf(double v, double t)
    {
        (double k, double ncp) = Parameters(t);
        return NonCentralChiSquaredFunctions.Cdf(df, ncp, v * k);
    }

    /// <inheritdoc/>
    public override double InvCdf(double q, double t)
    {
        (double k, double ncp) = Parameters(t);
        return NonCentralChiSquaredFunctions.Quantile(df, ncp, q) / k;
    }

    /// <summary>The stationary density at <paramref name="v"/>.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The density.</returns>
    public double StationaryPdf(double v)
    {
        double alpha = 0.5 * df;
        double beta = alpha / theta;
        double density = Math.Pow(beta, alpha) * Math.Pow(v, alpha - 1) * Math.Exp((-beta * v) - GammaFunctions.LGamma(alpha));
        if (double.IsFinite(density))
        {
            return density;
        }

        // the product overflowed: the same density in logarithms
        return Math.Exp((alpha * Math.Log(beta)) + ((alpha - 1) * Math.Log(v)) - (beta * v) - GammaFunctions.LGamma(alpha));
    }

    /// <summary>The stationary cumulative distribution at <paramref name="v"/>.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The probability that the process is at most <paramref name="v"/>.</returns>
    public double StationaryCdf(double v)
    {
        double alpha = 0.5 * df;
        double beta = alpha / theta;
        return GammaFunctions.GammaP(alpha, beta * v);
    }

    /// <summary>The stationary distribution's quantile at <paramref name="q"/>.</summary>
    /// <param name="q">The probability.</param>
    /// <returns>The value.</returns>
    public double StationaryInvCdf(double q)
    {
        double alpha = 0.5 * df;
        double beta = alpha / theta;
        return IgammaInverseFunctions.GammaPInv(alpha, q) / beta;
    }

    // the scale k = d/(1 - e^{-kappa t}) and the non-centrality k v0 e^{-kappa t}
    private (double K, double Ncp) Parameters(double t)
    {
        double e = Math.Exp(-kappa * t);
        double k = d / (1 - e);
        double ncp = k * v0 * e;
        return (k, ncp);
    }
}
