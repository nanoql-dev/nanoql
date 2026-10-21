// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/greeks.hpp ql/pricingengines/greeks.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The default Greek calculations of QuantLib's <c>greeks.hpp</c>.</summary>
public static class GreeksFunctions
{
    /// <summary>
    /// The theta implied by the Black-Scholes differential equation from the value, delta and gamma:
    /// r V − (r − q) S Δ − σ² S² Γ / 2, with the zero rates and the local volatility at time 0.
    /// </summary>
    /// <param name="p">The process.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="delta">Its delta.</param>
    /// <param name="gamma">Its gamma.</param>
    /// <returns>The theta.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A curve or the local volatility can't be read at time 0.</exception>
    public static double BlackScholesTheta(GeneralizedBlackScholesProcess p, double value, double delta, double gamma)
    {
        ArgumentNullException.ThrowIfNull(p);
        double u = p.StateVariable;
        double r = p.RiskFreeRate.ZeroRate(0.0, Compounding.Continuous).Rate;
        double q = p.DividendYield.ZeroRate(0.0, Compounding.Continuous).Rate;
        double v = p.LocalVolatility.LocalVol(0.0, u);

        return (r * value) - ((r - q) * u * delta) - (0.5 * v * v * u * u * gamma);
    }

    // BlackScholesTheta for an option expiring on a date: a structure dated on or after it accrues nothing over the
    // option's life, so its rate or volatility is 0 (the S-eq plan's ruling of 2026-10-21, C4R2-O2 (i)), where QuantLib
    // reads it at the structure's own time 0; BlackScholesTheta where every structure is dated before the exercise.
    internal static double BlackScholesTheta(
        GeneralizedBlackScholesProcess p, double value, double delta, double gamma, Date exercise)
    {
        double u = p.StateVariable;
        double r = p.RiskFreeRate.ReferenceDate < exercise ? p.RiskFreeRate.ZeroRate(0.0, Compounding.Continuous).Rate : 0.0;
        double q = p.DividendYield.ReferenceDate < exercise ? p.DividendYield.ZeroRate(0.0, Compounding.Continuous).Rate : 0.0;
        double v = p.LocalVolatility.ReferenceDate < exercise ? p.LocalVolatility.LocalVol(0.0, u) : 0.0;

        return (r * value) - ((r - q) * u * delta) - (0.5 * v * v * u * u * gamma);
    }

    /// <summary>The theta per day from the theta per year, on 365 days a year.</summary>
    /// <param name="theta">The theta per year.</param>
    /// <returns>The theta per day.</returns>
    public static double DefaultThetaPerDay(double theta) => theta / 365.0;
}
