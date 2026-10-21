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
/// documented divergence.
/// </para>
/// <para>
/// QuantLib divides the theta's terms by the volatility's time, and runs the rho's two rates, the forward's and the
/// payoff discount's, over one time, the discount curve's day counter from the risk-free curve's reference date. Here
/// each term runs over its own structure's time on the volatility's day counter, as QuantLib's
/// <c>blackScholesTheta</c> reads them, and each rate over its own curve's time. An exercise before a structure's
/// reference date sees nothing accrue on it, where QuantLib's reads throw. Where the structures share their times,
/// every value is QuantLib's. A documented divergence (the S-eq plan's rulings of 2026-10-21,
/// <c>Divergences/AnalyticThetaMismatchedTimesTests.cs</c>, <c>Divergences/EuropeanRhoDiscountCurveTests.cs</c>,
/// <c>Divergences/ExerciseBeforeReferenceDateTests.cs</c>).
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

        // each structure read at the exercise date, none accruing before its own reference date, where QuantLib's reads
        // throw (the S-eq plan's ruling of 2026-10-21, C4R2-O2 (i))
        Date lastDate = option.Exercise.LastDate();
        double variance = process.BlackVolatility.AccruedVariance(lastDate, payoff.Strike);
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double df = discountPtr.AccruedDiscount(lastDate);
        double riskFreeDiscountForFwdEstimation = process.RiskFreeRate.AccruedDiscount(lastDate);
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

        // each structure's time to the exercise its own, none before its reference date; QuantLib's rho runs the
        // forward's and the payoff discount's rates over one time, the discount curve's day counter from the risk-free
        // curve's date, and here each runs over its own curve's time (the S-eq plan's ruling of 2026-10-21), QuantLib's
        // where the two curves share their times
        DayCounter voldc = process.BlackVolatility.DayCounter;
        double riskFreeTime = process.RiskFreeRate.AccrualTimeTo(lastDate);
        double rho = discountPtr.SharesTimes(process.RiskFreeRate)
            ? black.Rho(riskFreeTime)
            : black.Rho(riskFreeTime) + ((riskFreeTime - discountPtr.AccrualTimeTo(lastDate)) * value);

        double dividendRho = black.DividendRho(process.DividendYield.AccrualTimeTo(lastDate));

        double t = process.BlackVolatility.AccrualTimeTo(lastDate);
        double vega = black.Vega(t);

        // QuantLib divides the theta's terms by the volatility's time; here each by its own structure's, on the
        // volatility's day counter (the S-eq plan's ruling of 2026-10-21, under R8), QuantLib's where they agree
        double? theta, thetaPerDay;
        try
        {
            theta = black.Theta(
                spot,
                discountPtr.AccrualTimeTo(lastDate, voldc),
                riskFreeDiscountForFwdEstimation,
                process.RiskFreeRate.AccrualTimeTo(lastDate, voldc),
                dividendDiscount,
                process.DividendYield.AccrualTimeTo(lastDate, voldc),
                t);
            thetaPerDay = theta / 365.0;
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            theta = null;
            thetaPerDay = null;
        }

        double strikeSensitivity = black.StrikeSensitivity;
        double itmCashProbability = black.ItmCashProbability;

        double tte = t;

        // QuantLib's √(variance / tte) is 0/0 for an option expiring today; the volatility there is the structure's,
        // and none for an option expiring before the structure's reference date, which sees no variance.
        double volatility = tte == 0.0
            ? (lastDate < process.BlackVolatility.ReferenceDate ? 0.0 : process.BlackVolatility.BlackVol(lastDate, payoff.Strike))
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
