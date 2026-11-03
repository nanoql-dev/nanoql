// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/cliquet/analyticcliquetengine.hpp ql/pricingengines/cliquet/analyticcliquetengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a European cliquet option: each period a forward-start option struck at the moneyness times
/// the spot, by Black's formula on the period's forward variance, weighted by the dividend discount to its start.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. Each period's theta is its value times the dividend rate to its start, over which its weight decays,
/// where QuantLib's reads the rate over the period itself, after the start (<c>Divergences/CliquetThetaTests.cs</c>):
/// QuantLib's forward engine's theta, period by period. Each period's vega is under a parallel shift of the input
/// volatilities, its vega in the forward volatility times dσ_f/dε, exactly QuantLib's where the volatilities at the
/// period's ends are equal (<c>Divergences/ForwardVegaTermStructureTests.cs</c>).
/// </para>
/// <para>
/// Every structure is read by date, accruing nothing before its own date, the rates on the process's clock
/// (<see cref="FdmBlackScholesOp.Clock"/>), where QuantLib's reads throw for a reset before a later-dated structure's
/// date (<c>Divergences/ExoticMismatchedTimesTests.cs</c>); a reset before the clock's own date keeps QuantLib's
/// "negative time" refusal; on structures dated alike, QuantLib's reads.
/// </para>
/// </remarks>
public static class AnalyticCliquetEngine
{
    /// <summary>Prices a European cliquet option: QuantLib's <c>calculate</c>.</summary>
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

        double strike = underlying * moneyness.Strike;
        var payoff = new PlainVanillaPayoff(moneyness.OptionType, strike);

        YieldTermStructure rTS = process.RiskFreeRate;
        YieldTermStructure qTS = process.DividendYield;
        BlackVolTermStructure volTS = process.BlackVolatility;
        TermStructure clock = FdmBlackScholesOp.Clock(process);

        double value = 0.0;
        double delta = 0.0, gamma = 0.0;
        double theta = 0.0;
        double rho = 0.0, dividendRho = 0.0;
        double vega = 0.0;

        for (int i = 1; i < resetDates.Length; i++)
        {
            double weight = qTS.AccruedDiscountOn(clock, resetDates[i - 1]);
            double discount = rTS.AccruedDiscountOn(clock, resetDates[i]) / rTS.AccruedDiscountOn(clock, resetDates[i - 1]);
            double qDiscount = qTS.AccruedDiscountOn(clock, resetDates[i]) / qTS.AccruedDiscountOn(clock, resetDates[i - 1]);
            double forward = underlying * qDiscount / discount;
            double variance = volTS.AccruedForwardVariance(resetDates[i - 1], resetDates[i], strike);

            var black = new BlackCalculator(payoff, forward, Math.Sqrt(variance), discount);

            DayCounter rfdc = rTS.DayCounter;
            DayCounter divdc = qTS.DayCounter;
            DayCounter voldc = volTS.DayCounter;

            value += weight * black.Value;
            delta += weight * (black.Delta(underlying) + (moneyness.Strike * discount * black.Beta));
            gamma += 0.0;

            // the dividend rate to the period's start, over which its weight decays, where QuantLib's reads the
            // rate over the period
            theta += ForwardVanillaEngine.RateTo(qTS, resetDates[i - 1], clock) * weight * black.Value;

            double dt = rTS.AccrualYearFraction(rfdc, resetDates[i - 1], resetDates[i]);
            rho += weight * black.Rho(dt);

            double t = qTS.AccrualTimeTo(resetDates[i - 1], divdc);
            dt = qTS.AccrualYearFraction(divdc, resetDates[i - 1], resetDates[i]);
            dividendRho += weight * (black.DividendRho(dt) - (t * black.Value));

            dt = volTS.AccrualYearFraction(voldc, resetDates[i - 1], resetDates[i]);
            vega += weight * ForwardVanillaEngine.ShiftedVega(
                volTS, resetDates[i - 1], resetDates[i], strike, ComparisonFunctions.Close(forward, strike), black.Vega(dt));
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
