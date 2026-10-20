// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticeuropeanengine.hpp ql/pricingengines/vanilla/analyticeuropeanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The pricing engine for European vanilla options by the analytic Black-Scholes formulas, through
/// <see cref="BlackCalculator"/>, for plain vanilla, cash-or-nothing, asset-or-nothing and gap payoffs.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value and every Greek, and the additional results "spot", "dividendDiscount",
/// "riskFreeDiscount", "forward", "strike", "volatility" and "timeToExpiry". A theta the calculator refuses is
/// <see langword="null"/>, as QuantLib's.
/// </para>
/// <para>
/// The "volatility" result of an option expiring on the evaluation date is the Black volatility at its exercise date
/// and strike. QuantLib's is √(variance / time to expiry), 0/0 there (<c>analyticeuropeanengine.cpp:113</c>); a
/// documented divergence. Every other value is QuantLib's.
/// </para>
/// </remarks>
public static class AnalyticEuropeanEngine
{
    /// <summary>
    /// Prices a European vanilla option, the process's risk-free curve both forecasting the forward and discounting the
    /// payoff.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The calculator refuses its inputs.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't striked, the underlying isn't positive, or a curve or the
    /// volatility can't be read at the exercise date.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        return Calculate(option, context, process, process.RiskFreeRate);
    }

    /// <summary>
    /// Prices a European vanilla option, the process's risk-free curve forecasting the forward and another curve
    /// discounting the payoff.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="discountCurve">The curve discounting the payoff.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The calculator refuses its inputs.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't striked, the underlying isn't positive, or a curve or the
    /// volatility can't be read at the exercise date.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        YieldTermStructure discountCurve)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        OneAssetOptionResults results = Body(option, process, discountCurve);
        return option.FetchResults(results);
    }

    /// <summary>QuantLib's <c>calculate</c>: the engine's body, without the instrument's expiry check.</summary>
    /// <param name="option">The option.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="discountCurve">The curve discounting the payoff.</param>
    /// <returns>The option's results.</returns>
    internal static OneAssetOptionResults Body(
        VanillaOption option, GeneralizedBlackScholesProcess process, YieldTermStructure discountCurve)
    {
        // if the discount curve is not specified, we default to the
        // risk free rate curve embedded within the GBM process
        YieldTermStructure discountPtr = discountCurve;

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        Date lastDate = option.Exercise.LastDate();
        double variance = process.BlackVolatility.BlackVariance(lastDate, payoff.Strike);
        double dividendDiscount = process.DividendYield.Discount(lastDate);
        double df = discountPtr.Discount(lastDate);
        double riskFreeDiscountForFwdEstimation = process.RiskFreeRate.Discount(lastDate);
        double spot = process.StateVariable;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double forwardPrice = spot * dividendDiscount / riskFreeDiscountForFwdEstimation;

        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(variance), df);

        double value = black.Value;
        double delta = black.Delta(spot);
        double deltaForward = black.DeltaForward;
        double elasticity = black.Elasticity(spot);
        double gamma = black.Gamma(spot);

        DayCounter rfdc = discountPtr.DayCounter;
        DayCounter divdc = process.DividendYield.DayCounter;
        DayCounter voldc = process.BlackVolatility.DayCounter;
        double t = rfdc.YearFraction(process.RiskFreeRate.ReferenceDate, lastDate);
        double rho = black.Rho(t);

        t = divdc.YearFraction(process.DividendYield.ReferenceDate, lastDate);
        double dividendRho = black.DividendRho(t);

        t = voldc.YearFraction(process.BlackVolatility.ReferenceDate, lastDate);
        double vega = black.Vega(t);
        double? theta, thetaPerDay;
        try
        {
            theta = black.Theta(spot, t);
            thetaPerDay = black.ThetaPerDay(spot, t);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            theta = null;
            thetaPerDay = null;
        }

        double strikeSensitivity = black.StrikeSensitivity;
        double itmCashProbability = black.ItmCashProbability;

        double tte = process.BlackVolatility.TimeFromReference(lastDate);

        // QuantLib's √(variance / tte) is 0/0 for an option expiring today; the volatility there is the structure's.
        double volatility = tte == 0.0
            ? process.BlackVolatility.BlackVol(lastDate, payoff.Strike)
            : Math.Sqrt(variance / tte);

        ImmutableSortedDictionary<string, object>.Builder additional = ImmutableSortedDictionary.CreateBuilder<string, object>();
        additional["spot"] = spot;
        additional["dividendDiscount"] = dividendDiscount;
        additional["riskFreeDiscount"] = riskFreeDiscountForFwdEstimation;
        additional["forward"] = forwardPrice;
        additional["strike"] = payoff.Strike;
        additional["volatility"] = volatility;
        additional["timeToExpiry"] = tte;

        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            DeltaForward = deltaForward,
            Elasticity = elasticity,
            Gamma = gamma,
            Rho = rho,
            DividendRho = dividendRho,
            Vega = vega,
            Theta = theta,
            ThetaPerDay = thetaPerDay,
            StrikeSensitivity = strikeSensitivity,
            ItmCashProbability = itmCashProbability,
            AdditionalResults = additional.ToImmutable(),
        };
    }
}
