// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/bsmrndcalculator.hpp ql/methods/finitedifferences/utilities/bsmrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The risk-neutral density of the logarithm of the underlying in the Black-Scholes-Merton model with constant
/// volatility: normal, its mean the log forward less half the variance, its variance the Black variance.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. Its argument x is ln(S) and its times are the process's, the risk-free curve's. The volatility is the
/// model's constant one, read at the strike e^x; the inverse reads it at e^p, the probability as QuantLib passes it,
/// which a constant volatility doesn't see.
/// </para>
/// <para>
/// The dividend curve and the volatility are read at the instant a time denotes, each on its own clock, the variance
/// accrued from the risk-free curve's reference date, where QuantLib reads them at the risk-free curve's time (the
/// mismatched-dates class); structures that share the curve's times keep QuantLib's bits. A documented divergence.
/// </para>
/// </remarks>
public sealed class BSMRNDCalculator : RiskNeutralDensityCalculator
{
    private readonly GeneralizedBlackScholesProcess process;

    /// <summary>Initializes a new instance of the <see cref="BSMRNDCalculator"/> class.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public BSMRNDCalculator(GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        this.process = process;
    }

    /// <summary>The density of ln(S) at a time.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    /// <exception cref="NanoQLException">A structure can't be read at the time.</exception>
    public override double Pdf(double x, double t)
    {
        (double mean, double stdDev) = DistributionParams(x, t);
        return new NormalDistribution(mean, stdDev).Evaluate(x);
    }

    /// <summary>The distribution function of ln(S) at a time.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">A structure can't be read at the time.</exception>
    public override double Cdf(double x, double t)
    {
        (double mean, double stdDev) = DistributionParams(x, t);
        return new CumulativeNormalDistribution(mean, stdDev).Evaluate(x);
    }

    /// <summary>The inverse of the distribution function at a time.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="t">The time.</param>
    /// <returns>The logarithm of the underlying.</returns>
    /// <exception cref="NanoQLException">A structure can't be read at the time.</exception>
    public override double InvCdf(double p, double t)
    {
        (double mean, double stdDev) = DistributionParams(p, t);
        return new InverseCumulativeNormal(mean, stdDev).Evaluate(p);
    }

    private (double Mean, double StdDev) DistributionParams(double x, double t)
    {
        YieldTermStructure clock = process.RiskFreeRate;
        BlackVolTermStructure vol = process.BlackVolatility;
        double strike = Math.Exp(x);
        double stdDev = vol.SharesTimes(clock)
            ? vol.BlackVol(t, strike) * Math.Sqrt(t)
            : Math.Sqrt(vol.BlackForwardVarianceAt(clock, 0.0, t, strike));
        double mean = Math.Log(process.X0) - (0.5 * stdDev * stdDev)
            + Math.Log(process.DividendYield.DiscountAt(clock, t) / process.RiskFreeRate.Discount(t));
        return (mean, stdDev);
    }
}
