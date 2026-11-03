// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticdigitalamericanengine.hpp ql/pricingengines/vanilla/analyticdigitalamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic pricing engine for American vanilla options with a digital payoff: cash-or-nothing or
/// asset-or-nothing, paid at the hit (<see cref="AmericanPayoffAtHit"/>) or at expiry
/// (<see cref="AmericanPayoffAtExpiry"/>, knock-in).
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, and at the hit the delta, gamma and rho (QuantLib's to-do: more Greeks). An exercise
/// window opening after the volatility's reference date is refused, as QuantLib refuses it.
/// <see cref="AnalyticDigitalAmericanKOEngine"/> prices the knock-out at expiry: QuantLib's sibling class, whose
/// <c>knock_in()</c> override is a flag here.
/// </para>
/// <para>
/// Each structure is read at the exercise date by date, nothing accrued before its own reference date (the
/// mismatched-dates class), where QuantLib's reads throw; the rho's time is the risk-free curve's, as QuantLib's.
/// </para>
/// </remarks>
public static class AnalyticDigitalAmericanEngine
{
    /// <summary>Prices an American digital option, knock-in.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value, and at the hit the delta, gamma and rho; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The payoff is neither cash-or-nothing nor asset-or-nothing.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't American, its window opens after the volatility's reference date, or the underlying isn't
    /// positive.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process) =>
        Calculate(option, context, process, true);

    // QuantLib's calculate, its knock_in() a flag.
    internal static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, bool knockIn)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (option.Exercise is not AmericanExercise ex)
        {
            throw new NanoQLException("non-American exercise given");
        }

        if (!(ex.Dates[0] <= process.BlackVolatility.ReferenceDate))
        {
            throw new NanoQLException("American option with window exercise not handled yet");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        double spot = process.StateVariable;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        // each structure read by date, nothing accrued before its reference date (the mismatched-dates class)
        Date lastDate = ex.LastDate();
        double variance = process.BlackVolatility.AccruedVariance(lastDate, payoff.Strike);
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);

        OneAssetOptionResults results;
        if (ex.PayoffAtExpiry)
        {
            var pricer = new AmericanPayoffAtExpiry(spot, riskFreeDiscount, dividendDiscount, variance, payoff, knockIn);
            results = new OneAssetOptionResults { NPV = pricer.Value() };
        }
        else
        {
            // QuantLib's knock-out engine prices the knock-in here, since its formula has no knock-out at the hit
            if (!knockIn)
            {
                throw new NanoQLException("a knock-out digital pays at expiry: no knock-out formula at the hit");
            }

            var pricer = new AmericanPayoffAtHit(spot, riskFreeDiscount, dividendDiscount, variance, payoff);
            double t = process.RiskFreeRate.AccrualTimeTo(lastDate);
            results = new OneAssetOptionResults
            {
                NPV = pricer.Value(),
                Delta = pricer.Delta(),
                Gamma = pricer.Gamma(),
                Rho = pricer.Rho(t),
            };
        }

        return option.FetchResults(results);
    }
}
