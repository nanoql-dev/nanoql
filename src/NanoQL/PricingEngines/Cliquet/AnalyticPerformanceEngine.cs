// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/cliquet/analyticperformanceengine.hpp ql/pricingengines/cliquet/analyticperformanceengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a European performance option: a cliquet whose periods pay their returns, each by Black's
/// formula on the period's forward variance, discounted to its start.
/// </summary>
/// <remarks>
/// Stateless. Each period's theta is its value times the risk-free rate to its start, where QuantLib's reads the rate
/// over the period itself (the S-exo plan's ER5 (xxii), <c>Divergences/CliquetThetaTests.cs</c>): QuantLib's
/// forward-performance engine's theta, period by period. Each period's vega is under a parallel shift of the input
/// volatilities (ER5 (xxxvii)), and every structure is read by date on the process's clock (ER5 (xxiv)), as
/// <see cref="AnalyticCliquetEngine"/> reads them.
/// </remarks>
public static class AnalyticPerformanceEngine
{
    /// <summary>Prices a European performance option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <returns>The option's results: the value, delta, gamma, theta, rho, dividend rho and vega.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, it isn't European, the spot isn't positive, or a reset is before the clock's
    /// date.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        CliquetOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process));
    }

    // QuantLib's calculate().
    private static OneAssetOptionResults Body(CliquetOption option, GeneralizedBlackScholesProcess process)
    {
        if (option.Exercise.Type != ExerciseType.European)
        {
            throw new NanoQLException("not an European option");
        }

        var moneyness = (PercentageStrikePayoff)option.Payoff!;
        Date[] resetDates = [.. option.ResetDates, option.Exercise.LastDate()];

        double underlying = process.X0;
        if (!(underlying > 0.0))
        {
            throw new NanoQLException("negative or null underlying");
        }

        var payoff = new PlainVanillaPayoff(moneyness.OptionType, 1.0);

        YieldTermStructure rTS = process.RiskFreeRate;
        YieldTermStructure qTS = process.DividendYield;
        BlackVolTermStructure volTS = process.BlackVolatility;
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        double strike = underlying * moneyness.Strike;

        double value = 0.0;
        double delta = 0.0, gamma = 0.0;
        double theta = 0.0;
        double rho = 0.0, dividendRho = 0.0;
        double vega = 0.0;

        for (int i = 1; i < resetDates.Length; i++)
        {
            double discount = rTS.AccruedDiscountOn(clock, resetDates[i - 1]);
            double rDiscount = rTS.AccruedDiscountOn(clock, resetDates[i])
                / rTS.AccruedDiscountOn(clock, resetDates[i - 1]);
            double qDiscount = qTS.AccruedDiscountOn(clock, resetDates[i])
                / qTS.AccruedDiscountOn(clock, resetDates[i - 1]);
            double forward = 1.0 / moneyness.Strike * qDiscount / rDiscount;
            double variance = volTS.AccruedForwardVariance(resetDates[i - 1], resetDates[i], strike);

            var black = new BlackCalculator(payoff, forward, Math.Sqrt(variance), rDiscount);

            DayCounter rfdc = rTS.DayCounter;
            DayCounter divdc = qTS.DayCounter;
            DayCounter voldc = volTS.DayCounter;

            value += discount * moneyness.Strike * black.Value;
            delta += 0.0;
            gamma += 0.0;

            // the risk-free rate to the period's start, over which its discount decays, where QuantLib's reads the
            // rate over the period
            theta += ForwardVanillaEngine.RateTo(rTS, resetDates[i - 1], clock) * discount * moneyness.Strike
                * black.Value;

            double dt = rTS.AccrualYearFraction(rfdc, resetDates[i - 1], resetDates[i]);
            double t = rTS.AccrualTimeTo(resetDates[i - 1], rfdc);
            rho += discount * moneyness.Strike * (black.Rho(dt) - (t * black.Value));

            dt = qTS.AccrualYearFraction(divdc, resetDates[i - 1], resetDates[i]);
            dividendRho += discount * moneyness.Strike * black.DividendRho(dt);

            dt = volTS.AccrualYearFraction(voldc, resetDates[i - 1], resetDates[i]);
            vega += discount * moneyness.Strike * ForwardVanillaEngine.ShiftedVega(
                volTS, resetDates[i - 1], resetDates[i], strike, ComparisonFunctions.Close(forward, 1.0), black.Vega(dt));
        }

        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            Gamma = gamma,
            Theta = theta,
            Rho = rho,
            DividendRho = dividendRho,
            Vega = vega,
        };
    }
}
