// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticdividendeuropeanengine.hpp ql/pricingengines/vanilla/analyticdividendeuropeanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic pricing engine for European options with discrete dividends: the Black-Scholes formulas on the spot
/// less the present value of the dividends paid from the settlement date to the exercise date.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma, vega, theta and rho; the other Greeks are <see langword="null"/>, as
/// QuantLib leaves them. A theta the calculator refuses is <see langword="null"/>.
/// </para>
/// <para>
/// QuantLib's settlement date is the risk-free curve's reference date. Here it is the earliest reference date of the
/// process's structures, each discounting nothing before its own, so a dividend paid between the evaluation date and a
/// later risk-free curve's reference date is the option's, as on the market restated from the evaluation date; where no
/// structure is dated before the risk-free curve, it is QuantLib's. The theta's and rho's times are the risk-free
/// curve's own, as QuantLib's, so a dividend before its reference date adds to neither. The theta reads each curve's
/// zero rate per the risk-free curve's time and the rho reads the dividend curve by date, where QuantLib reads the rate
/// per the curve's own day counter's year and the curve at the risk-free curve's time. A documented divergence
/// (<c>Divergences/DividendMismatchedTimesTests.cs</c>).
/// </para>
/// <para>
/// QuantLib divides the theta's terms by the risk-free curve's time; here each runs over its own structure's, the
/// discount's, the dividend curve's and the variance's, on the risk-free curve's day counter, as QuantLib's
/// <c>blackScholesTheta</c> reads them. An exercise before a structure's reference date sees nothing accrue on it,
/// where QuantLib's reads throw. Where the structures share their times, every value is QuantLib's. A documented
/// divergence (<c>Divergences/AnalyticThetaMismatchedTimesTests.cs</c>,
/// <c>Divergences/ExerciseBeforeReferenceDateTests.cs</c>).
/// </para>
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

        // the dividends and times from the earliest reference date of the structures, each structure discounting
        // nothing before its own: QuantLib's settlement date, the risk-free curve's, where none is dated before it
        Date lastDate = option.Exercise.LastDate();
        Date settlementDate = process.EarliestReferenceDate();
        double riskless = 0.0;
        int i;
        for (i = 0; i < dividends.Length; i++)
        {
            Date cashFlowDate = dividends[i].Date;

            if (cashFlowDate >= settlementDate && cashFlowDate <= lastDate)
            {
                riskless += dividends[i].Amount(context)
                    * process.RiskFreeRate.AccruedDiscount(cashFlowDate)
                    / process.DividendYield.AccruedDiscount(cashFlowDate);
            }
        }

        double spot = process.StateVariable - riskless;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying after subtracting dividends");
        }

        // each structure read at the exercise date, none accruing before its own reference date, where QuantLib's reads
        // throw
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
        double forwardPrice = spot * dividendDiscount / riskFreeDiscount;

        double variance = process.BlackVolatility.AccruedVariance(lastDate, payoff.Strike);

        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(variance), riskFreeDiscount);

        double value = black.Value;
        double delta = black.Delta(spot);
        double gamma = black.Gamma(spot);

        DayCounter rfdc = process.RiskFreeRate.DayCounter;
        double t = process.BlackVolatility.AccrualTimeTo(lastDate);
        double vega = black.Vega(t);

        double deltaTheta = 0.0, deltaRho = 0.0;
        for (i = 0; i < dividends.Length; i++)
        {
            Date d = dividends[i].Date;

            if (d >= settlementDate && d <= lastDate)
            {
                deltaTheta -= dividends[i].Amount(context)
                    * (ZeroRate(process.RiskFreeRate, rfdc, d) - ZeroRate(process.DividendYield, rfdc, d))
                    * process.RiskFreeRate.AccruedDiscount(d)
                    / process.DividendYield.AccruedDiscount(d);

                // the risk-free curve's own time to the dividend, none before its reference date, where the dividend's
                // value has no risk-free discount; QuantLib reads the dividend curve at that time, here by date, as the
                // value reads it, which is QuantLib's read where the two curves share their times
                double ti = process.RiskFreeRate.AccrualTimeTo(d);
                deltaRho += dividends[i].Amount(context) * ti
                    * process.RiskFreeRate.AccruedDiscount(d)
                    / process.DividendYield.AccruedDiscount(d);
            }
        }

        // the theta's and rho's times the risk-free curve's own, as QuantLib's; QuantLib divides the theta's terms by
        // that time, and here each by its own structure's, on the risk-free curve's day counter, QuantLib's where they
        // agree
        t = process.RiskFreeRate.AccrualTimeTo(lastDate);
        double? theta;
        try
        {
            theta = black.Theta(
                    spot,
                    t,
                    riskFreeDiscount,
                    t,
                    dividendDiscount,
                    process.DividendYield.AccrualTimeTo(lastDate, rfdc),
                    process.BlackVolatility.AccrualTimeTo(lastDate, rfdc))
                + (deltaTheta * black.Delta(spot));
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

    // A curve's continuous zero rate to a date from its own reference date, per unit of the risk-free curve's time,
    // none before that date: QuantLib's where the curve is on the risk-free curve's day counter; on another, where
    // QuantLib reads it per the curve's own year, its discount's rate over the risk-free curve's time between the two
    // dates
    private static double ZeroRate(YieldTermStructure curve, DayCounter rfdc, Date d)
    {
        if (d < curve.ReferenceDate)
        {
            return 0.0;
        }

        if (curve.DayCounter == rfdc)
        {
            return curve.ZeroRate(d, rfdc, Compounding.Continuous, Frequency.Annual).Rate;
        }

        double t = rfdc.YearFraction(curve.ReferenceDate, d);
        return t > 0.0
            ? Math.Log(1.0 / curve.Discount(d)) / t
            : curve.ZeroRate(d, curve.DayCounter, Compounding.Continuous, Frequency.Annual).Rate
                * curve.DayCounter.YearFraction(d, d + 1) / rfdc.YearFraction(d, d + 1);
    }
}
