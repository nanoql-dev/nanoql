// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/stulzengine.hpp ql/pricingengines/basket/stulzengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>
/// Stulz's engine for a European option on the minimum or the maximum of two assets: R. Stulz, "Options on the
/// Minimum or the Maximum of Two Risky Assets", Journal of Financial Economics 10 (1982), 161-185.
/// </summary>
/// <remarks>
/// <para>
/// The bivariate normal is QuantLib's default, <see cref="BivariateCumulativeNormalDistributionWe04DP"/>. Each
/// forward reads its own process's curves and the payoff is discounted on the first process's risk-free curve, where
/// QuantLib builds the second forward on the first process's discount
/// (<c>Divergences/TwoAssetTwoRiskFreeCurvesTests.cs</c>);
/// each structure is read by date and accrues nothing before its reference date
/// (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>).
/// </para>
/// <para>
/// Where the assets move together, a combined deviation of zero (a correlation of 1 with equal volatilities, or two
/// zero volatilities), the minimum is the asset of the smaller forward and the maximum that of the larger, each priced
/// by Black's formula, where QuantLib's derived correlations are NaN or infinite and refused; an asset of zero
/// volatility reads the bivariate normal at an infinite bound, its limit (<c>Divergences/StulzDegenerateTests.cs</c>).
/// At a negative strike the payoffs are linear: a call is the call struck at zero less the discounted strike, a put
/// is worth nothing, where QuantLib takes the log of a negative ratio (<c>Divergences/StulzNegativeStrikeTests.cs</c>).
/// </para>
/// </remarks>
public static class StulzEngine
{
    private static readonly CumulativeNormalDistribution Cum = new();

    /// <summary>
    /// Prices a European option on the minimum or the maximum of two assets: QuantLib's <c>calculate</c>.
    /// </summary>
    /// <param name="option">
    /// The option, on a <see cref="MinBasketPayoff"/> or <see cref="MaxBasketPayoff"/> over a plain vanilla payoff.
    /// </param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, or the payoff isn't a minimum or maximum basket over a plain vanilla payoff.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process1);
        ArgumentNullException.ThrowIfNull(process2);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new MultiAssetOptionResults
        {
            NPV = Value(option, process1, process2, correlation),
        });
    }

    // QuantLib's calculate().
    private static double Value(
        BasketOption option,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double rho)
    {
        if (option.Exercise.Type != ExerciseType.European)
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European Option");
        }

        var basketPayoff = (BasketPayoff)option.Payoff!;
        bool minBasket = basketPayoff is MinBasketPayoff;
        bool maxBasket = basketPayoff is MaxBasketPayoff;
        if (!(minBasket || maxBasket))
        {
            throw new NanoQLException("unknown basket type");
        }

        if (basketPayoff.BasePayoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double strike = payoff.Strike;
        Date lastDate = exercise.LastDate();

        // at a negative strike, the call struck at zero less the discounted strike, the variances read where it reads
        double varianceStrike = strike < 0.0 ? 0.0 : strike;
        double variance1 = process1.BlackVolatility.AccruedVariance(lastDate, varianceStrike);
        double variance2 = process2.BlackVolatility.AccruedVariance(lastDate, varianceStrike);

        double riskFreeDiscount = process1.RiskFreeRate.AccruedDiscount(lastDate);

        // the forwards carry the dividend yields, where QuantLib's comment doubts them; its 2-D engine agrees
        double dividendDiscount1 = process1.DividendYield.AccruedDiscount(lastDate);
        double dividendDiscount2 = process2.DividendYield.AccruedDiscount(lastDate);

        // each forward on its own process's curves
        double forward1 = process1.X0 * dividendDiscount1 / riskFreeDiscount;
        double forward2 = process2.X0 * dividendDiscount2 / process2.RiskFreeRate.AccruedDiscount(lastDate);

        Func<double, double> call = maxBasket
            ? k => EuroTwoAssetMaxBasketCall(forward1, forward2, k, riskFreeDiscount, variance1, variance2, rho)
            : k => EuroTwoAssetMinBasketCall(forward1, forward2, k, riskFreeDiscount, variance1, variance2, rho);

        // the payoffs are linear in the assets at a negative strike
        if (strike < 0.0)
        {
            return payoff.OptionType switch
            {
                OptionType.Call => call(0.0) - (strike * riskFreeDiscount),
                OptionType.Put => 0.0,
                _ => throw new NanoQLException("unknown option type"),
            };
        }

        return payoff.OptionType switch
        {
            // euro call on a two asset max or min basket
            OptionType.Call => call(strike),

            // euro put on a two asset max or min basket
            OptionType.Put => (strike * riskFreeDiscount) - call(0.0) + call(strike),
            _ => throw new NanoQLException("unknown option type"),
        };
    }

    // calculate the value of euro min basket call
    private static double EuroTwoAssetMinBasketCall(
        double forward1,
        double forward2,
        double strike,
        double riskFreeDiscount,
        double variance1,
        double variance2,
        double rho)
    {
        double stdDev1 = Math.Sqrt(variance1);
        double stdDev2 = Math.Sqrt(variance2);

        double variance = variance1 + variance2 - (2 * rho * stdDev1 * stdDev2);

        // the assets move together: the minimum is the asset of the smaller forward
        if (variance <= 0.0)
        {
            return forward1 <= forward2
                ? BlackFormula(OptionType.Call, strike, forward1, stdDev1) * riskFreeDiscount
                : BlackFormula(OptionType.Call, strike, forward2, stdDev2) * riskFreeDiscount;
        }

        double stdDev = Math.Sqrt(variance);

        double modRho1 = ((rho * stdDev2) - stdDev1) / stdDev;
        double modRho2 = ((rho * stdDev1) - stdDev2) / stdDev;

        double d1 = (Math.Log(forward1 / forward2) + (0.5 * variance)) / stdDev;

        double alfa, beta, gamma;
        if (strike != 0.0)
        {
            var bivCNorm = new BivariateCumulativeNormalDistributionWe04DP(rho);
            var bivCNormMod2 = new BivariateCumulativeNormalDistributionWe04DP(modRho2);
            var bivCNormMod1 = new BivariateCumulativeNormalDistributionWe04DP(modRho1);

            double d1_1 = Ratio(Math.Log(forward1 / strike) + (0.5 * variance1), stdDev1);
            double d1_2 = Ratio(Math.Log(forward2 / strike) + (0.5 * variance2), stdDev2);
            alfa = bivCNormMod1.Evaluate(d1_1, -d1);
            beta = bivCNormMod2.Evaluate(d1_2, d1 - stdDev);
            gamma = bivCNorm.Evaluate(d1_1 - stdDev1, d1_2 - stdDev2);
        }
        else
        {
            alfa = Cum.Evaluate(-d1);
            beta = Cum.Evaluate(d1 - stdDev);
            gamma = 1.0;
        }

        return riskFreeDiscount *
            ((forward1 * alfa) + (forward2 * beta) - (strike * gamma));
    }

    // calculate the value of euro max basket call
    private static double EuroTwoAssetMaxBasketCall(
        double forward1,
        double forward2,
        double strike,
        double riskFreeDiscount,
        double variance1,
        double variance2,
        double rho)
    {
        double stdDev1 = Math.Sqrt(variance1);
        double stdDev2 = Math.Sqrt(variance2);

        // the assets move together: the maximum is the asset of the larger forward
        if (variance1 + variance2 - (2 * rho * stdDev1 * stdDev2) <= 0.0)
        {
            return forward1 <= forward2
                ? BlackFormula(OptionType.Call, strike, forward2, stdDev2) * riskFreeDiscount
                : BlackFormula(OptionType.Call, strike, forward1, stdDev1) * riskFreeDiscount;
        }

        double black1 = BlackFormula(OptionType.Call, strike, forward1, stdDev1) * riskFreeDiscount;

        double black2 = BlackFormula(OptionType.Call, strike, forward2, stdDev2) * riskFreeDiscount;

        return black1 + black2 -
            EuroTwoAssetMinBasketCall(forward1, forward2, strike, riskFreeDiscount, variance1, variance2, rho);
    }

    // num/den, its limit where an asset's deviation is zero: ±∞ by the sign of num, 0 where num is 0 too
    private static double Ratio(double num, double den)
    {
        if (den != 0.0)
        {
            return num / den;
        }

        return num > 0.0 ? double.PositiveInfinity : num < 0.0 ? double.NegativeInfinity : 0.0;
    }
}
