// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/gbsmrndcalculator.hpp ql/methods/finitedifferences/utilities/gbsmrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The risk-neutral density of the underlying in the Black-Scholes-Merton model with a strike-dependent volatility:
/// Breeden and Litzenberger's, the distribution function the strike derivative of the discounted call or put price,
/// the volatility's skew included.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. Its argument is the strike, S itself, and its times are the process's, the risk-free curve's.
/// </para>
/// <para>
/// The dividend curve and the volatility are read at the instant a time denotes, each on its own clock, the variance
/// accrued from the risk-free curve's reference date and the volatility its root per unit of that curve's time, where
/// QuantLib reads them at the risk-free curve's time (the S-van plan's VR4 (xxiii), the mismatched-dates class);
/// structures that share the curve's times keep QuantLib's bits. A documented divergence.
/// </para>
/// </remarks>
public sealed class GBSMRNDCalculator : RiskNeutralDensityCalculator
{
    private readonly GeneralizedBlackScholesProcess process;

    /// <summary>Initializes a new instance of the <see cref="GBSMRNDCalculator"/> class.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public GBSMRNDCalculator(GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        this.process = process;
    }

    /// <summary>The density at a strike and time: the distribution function's central difference.</summary>
    /// <param name="k">The strike.</param>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    /// <exception cref="NanoQLException">A structure can't be read at the time.</exception>
    public override double Pdf(double k, double t)
    {
        double dk = 1e-3 * k;
        return (Cdf(k + dk, t) - Cdf(k - dk, t)) / (2 * dk);
    }

    /// <summary>The distribution function at a strike and time.</summary>
    /// <param name="k">The strike.</param>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">A structure can't be read at the time.</exception>
    public override double Cdf(double k, double t)
    {
        YieldTermStructure clock = process.RiskFreeRate;
        double dk = 1e-3 * k;
        double dvol_dk = (Vol(t, k + dk) - Vol(t, k - dk)) / (2 * dk);
        double dR = process.RiskFreeRate.Discount(t, true);
        double dD = process.DividendYield.DiscountAt(clock, t, true);
        double forward = process.X0 * dD / dR;
        double stdDev = Math.Sqrt(Variance(t, k));

        if (forward <= k)
        {
            var calc = new BlackCalculator(OptionType.Call, k, forward, stdDev, dR);
            return 1.0 + ((calc.StrikeSensitivity + (calc.Vega(t) * dvol_dk)) / dR);
        }
        else
        {
            var calc = new BlackCalculator(OptionType.Put, k, forward, stdDev, dR);
            return (calc.StrikeSensitivity + (calc.Vega(t) * dvol_dk)) / dR;
        }
    }

    /// <summary>The inverse of the distribution function at a time, by Brent's method from the forward's
    /// quantile.</summary>
    /// <param name="q">The probability.</param>
    /// <param name="t">The time.</param>
    /// <returns>The strike.</returns>
    /// <exception cref="NanoQLException">No bracket is found within a factor of 65535 of the guess, with QuantLib's
    /// message, or a structure can't be read.</exception>
    public override double InvCdf(double q, double t)
    {
        YieldTermStructure clock = process.RiskFreeRate;
        double fwd = process.X0 / process.RiskFreeRate.Discount(t, true) * process.DividendYield.DiscountAt(clock, t, true);
        double atmVariance = Math.Sqrt(Variance(t, fwd));
        double atmX = new InverseCumulativeNormal().Evaluate(q);
        double guess = fwd * Math.Exp(atmVariance * atmX);

        double lower = guess;
        while (guess / lower < 65535.0 && Cdf(lower, t) > q)
        {
            lower *= 0.5;
        }

        double upper = guess;
        while (upper / guess < 65535.0 && Cdf(upper, t) < q)
        {
            upper *= 2;
        }

        if (!(guess / lower < 65535.0 && upper / guess < 65535.0))
        {
            throw new NanoQLException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Could not find an start interval with ({lower}, {upper}) -> ({Cdf(lower, t)}, {Cdf(upper, t)})"));
        }

        return new Brent().Solve(k => Cdf(k, t) - q, 1e-10, 0.5 * (lower + upper), lower, upper);
    }

    // the Black volatility at a time and strike: QuantLib's read on the risk-free curve's clock, otherwise the root of
    // the variance accrued to the instant per unit of the curve's time
    private double Vol(double t, double k)
    {
        BlackVolTermStructure vol = process.BlackVolatility;
        YieldTermStructure clock = process.RiskFreeRate;
        return vol.SharesTimes(clock) ? vol.BlackVol(t, k) : Math.Sqrt(vol.BlackForwardVarianceAt(clock, 0.0, t, k) / t);
    }

    // the Black variance at a time and strike, extrapolating: QuantLib's read on the risk-free curve's clock, otherwise
    // the variance accrued to the instant
    private double Variance(double t, double k)
    {
        BlackVolTermStructure vol = process.BlackVolatility;
        YieldTermStructure clock = process.RiskFreeRate;
        return vol.SharesTimes(clock) ? vol.BlackVariance(t, k, true) : vol.BlackForwardVarianceAt(clock, 0.0, t, k, true);
    }
}
