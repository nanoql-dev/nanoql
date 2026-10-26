// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/blackscholescalculator.hpp ql/pricingengines/blackscholescalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Black-Scholes (1973) calculator: <see cref="BlackCalculator"/> on the forward of a spot, its growth and discount
/// factors, with the spot sensitivities at that spot.
/// </summary>
/// <remarks>Immutable, as its base.</remarks>
public sealed class BlackScholesCalculator : BlackCalculator
{
    private readonly double spot;

    /// <summary>Initializes a new instance of the <see cref="BlackScholesCalculator"/> class for a striked payoff.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="growth">The growth factor, the dividend discount to expiry.</param>
    /// <param name="stdDev">The standard deviation of the log of the underlying at expiry, σ√T.</param>
    /// <param name="discount">The discount factor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The base's refusals, or the spot or the growth isn't positive.
    /// </exception>
    public BlackScholesCalculator(StrikedTypePayoff payoff, double spot, double growth, double stdDev, double discount)
        : base(payoff, spot * growth / discount, stdDev, discount)
    {
        this.spot = Checked(spot, growth);
    }

    /// <summary>Initializes a new instance of the <see cref="BlackScholesCalculator"/> class for a plain vanilla
    /// payoff.</summary>
    /// <param name="optionType">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="growth">The growth factor, the dividend discount to expiry.</param>
    /// <param name="stdDev">The standard deviation of the log of the underlying at expiry, σ√T.</param>
    /// <param name="discount">The discount factor.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">
    /// The base's refusals, or the spot or the growth isn't positive.
    /// </exception>
    public BlackScholesCalculator(
        OptionType optionType, double strike, double spot, double growth, double stdDev, double discount)
        : base(optionType, strike, spot * growth / discount, stdDev, discount)
    {
        this.spot = Checked(spot, growth);
    }

    /// <summary>The sensitivity to the spot.</summary>
    /// <returns>The delta.</returns>
    public double Delta() => Delta(spot);

    /// <summary>The sensitivity in percent to a percent change in the spot.</summary>
    /// <returns>The elasticity.</returns>
    public double Elasticity() => Elasticity(spot);

    /// <summary>The second derivative in the spot.</summary>
    /// <returns>The gamma.</returns>
    public double Gamma() => Gamma(spot);

    /// <summary>The sensitivity to the time to maturity.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The theta.</returns>
    public double Theta(double maturity) => Theta(spot, maturity);

    /// <summary>The sensitivity to the time to maturity per day, a year of 365 days.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The theta per day.</returns>
    public double ThetaPerDay(double maturity) => ThetaPerDay(spot, maturity);

    // QuantLib's checks, after the base's (its constructor body)
    private static double Checked(double spot, double growth)
    {
        if (!(spot > 0.0))
        {
            throw new ArgumentException(
                $"spot ({spot.ToString(CultureInfo.InvariantCulture)}) must be positive", nameof(spot));
        }

        if (!(growth > 0.0))
        {
            throw new ArgumentException(
                $"growth ({growth.ToString(CultureInfo.InvariantCulture)}) must be positive", nameof(growth));
        }

        return spot;
    }
}
