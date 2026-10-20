// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticdividendeuropeanengine.hpp ql/pricingengines/vanilla/analyticdividendeuropeanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic pricing engine for European options with discrete dividends: the Black-Scholes formulas on the spot
/// less the present value of the dividends paid from the risk-free curve's reference date to the exercise date.
/// </summary>
/// <remarks>
/// The engine reports the value, delta, gamma, vega, theta and rho; the other Greeks are <see langword="null"/>, as
/// QuantLib leaves them. A theta the calculator refuses is <see langword="null"/>.
/// </remarks>
public static class AnalyticDividendEuropeanEngine
{
    /// <summary>Prices a European vanilla option with discrete dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="dividends">The dividends.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The dividends are <c>default(ImmutableArray)</c> or hold a null one, or the calculator refuses its inputs.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't striked, the spot less the dividends isn't positive, a dividend
    /// has no amount, or a curve or the volatility can't be read.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        CheckDividends(dividends, nameof(dividends));
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        OneAssetOptionResults results = Body(option, context, process, dividends);
        return option.FetchResults(results);
    }

    /// <summary>Refuses a default or null-holding dividend schedule, naming the parameter.</summary>
    /// <param name="dividends">The dividends.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentException">The schedule is <c>default(ImmutableArray)</c> or holds a null
    /// dividend.</exception>
    internal static void CheckDividends(ImmutableArray<Dividend> dividends, string paramName)
    {
        if (dividends.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a dividend schedule; an empty one is []", paramName);
        }

        if (dividends.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", paramName);
        }
    }

    /// <summary>QuantLib's <c>calculate</c>: the engine's body, without the instrument's expiry check.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context, which the dividends' amounts take.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="dividends">The dividends.</param>
    /// <returns>The option's results.</returns>
    internal static OneAssetOptionResults Body(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends)
    {
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        Date lastDate = option.Exercise.LastDate();
        Date settlementDate = process.RiskFreeRate.ReferenceDate;
        double riskless = 0.0;
        int i;
        for (i = 0; i < dividends.Length; i++)
        {
            Date cashFlowDate = dividends[i].Date;

            if (cashFlowDate >= settlementDate && cashFlowDate <= lastDate)
            {
                riskless += dividends[i].Amount(context)
                    * process.RiskFreeRate.Discount(cashFlowDate)
                    / process.DividendYield.Discount(cashFlowDate);
            }
        }

        double spot = process.StateVariable - riskless;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying after subtracting dividends");
        }

        double dividendDiscount = process.DividendYield.Discount(lastDate);
        double riskFreeDiscount = process.RiskFreeRate.Discount(lastDate);
        double forwardPrice = spot * dividendDiscount / riskFreeDiscount;

        double variance = process.BlackVolatility.BlackVariance(lastDate, payoff.Strike);

        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(variance), riskFreeDiscount);

        double value = black.Value;
        double delta = black.Delta(spot);
        double gamma = black.Gamma(spot);

        DayCounter rfdc = process.RiskFreeRate.DayCounter;
        DayCounter dydc = process.DividendYield.DayCounter;
        DayCounter voldc = process.BlackVolatility.DayCounter;
        double t = voldc.YearFraction(process.BlackVolatility.ReferenceDate, lastDate);
        double vega = black.Vega(t);

        double deltaTheta = 0.0, deltaRho = 0.0;
        for (i = 0; i < dividends.Length; i++)
        {
            Date d = dividends[i].Date;

            if (d >= settlementDate && d <= lastDate)
            {
                deltaTheta -= dividends[i].Amount(context)
                    * (process.RiskFreeRate.ZeroRate(d, rfdc, Compounding.Continuous, Frequency.Annual).Rate
                        - process.DividendYield.ZeroRate(d, dydc, Compounding.Continuous, Frequency.Annual).Rate)
                    * process.RiskFreeRate.Discount(d)
                    / process.DividendYield.Discount(d);

                double ti = process.Time(d);
                deltaRho += dividends[i].Amount(context) * ti
                    * process.RiskFreeRate.Discount(ti)
                    / process.DividendYield.Discount(ti);
            }
        }

        t = process.Time(lastDate);
        double? theta;
        try
        {
            theta = black.Theta(spot, t) + (deltaTheta * black.Delta(spot));
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            theta = null;
        }

        double rho = black.Rho(t) + (deltaRho * black.Delta(spot));

        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            Gamma = gamma,
            Vega = vega,
            Theta = theta,
            Rho = rho,
        };
    }
}
