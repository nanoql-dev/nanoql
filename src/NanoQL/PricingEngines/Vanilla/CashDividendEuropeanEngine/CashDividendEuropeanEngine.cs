// SPDX-FileCopyrightText: Copyright (C) 2025 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/cashdividendeuropeanengine.hpp ql/pricingengines/vanilla/cashdividendeuropeanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// The (semi-)analytic engine for European options on an underlying paying cash dividends: J. Healy, "The Pricing of
/// Vanilla Options with Cash Dividends as a Classic Vanilla Basket Option Problem" (2021),
/// https://arxiv.org/pdf/2106.12971.
/// </summary>
/// <remarks>
/// <para>
/// Under the spot model the option is a basket of the dividends and the strike against the spot, priced by
/// <see cref="ChoiBasketEngine"/> with QuantLib's arguments (λ 10, 2,000 steps, the control variate); a single
/// dividend on the risk-free curve's date, or the escrowed model, is priced by
/// <see cref="AnalyticDividendEuropeanEngine"/>, and no dividend by <see cref="AnalyticEuropeanEngine"/>. The value
/// alone, as QuantLib's. Stateless: the dividends' processes are built per call, as QuantLib builds them.
/// </para>
/// <para>
/// The settlement and the time to maturity are the process's clock's (<see cref="FdmBlackScholesOp.Clock"/>), from
/// its earliest structure date, and the curves and the volatility are read by date, accruing nothing before their
/// dates, where QuantLib's settlement and time are the risk-free curve's, so a dividend before that curve's date is
/// dropped, and its reads throw for an expiry before it (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>). Each
/// dividend's flat volatility is dated and counted on the clock, so its variance to expiry is the one read by date,
/// where QuantLib's is dated and counted on the volatility's own clock and sized on the risk-free curve's, its variance
/// scaled by the ratio of the two times. On structures dated and counted alike the clock is the risk-free curve and
/// the reads are QuantLib's. The dividends are ordered by date by a stable sort, where QuantLib's sort leaves
/// dividends of one date in no set order.
/// </para>
/// </remarks>
public static class CashDividendEuropeanEngine
{
    /// <summary>Prices a European option on an underlying paying cash dividends: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option, on a plain vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <param name="dividends">The dividends.</param>
    /// <param name="cashDividendModel">The dividends' model.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The model isn't one of its values.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or the payoff isn't plain vanilla.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        DividendSchedule dividends,
        CashDividendEuropeanEngineCashDividendModel cashDividendModel = CashDividendEuropeanEngineCashDividendModel.Spot)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (dividends.IsDefault)
        {
            throw new ArgumentNullException(nameof(dividends));
        }

        foreach (Dividend d in dividends)
        {
            ArgumentNullException.ThrowIfNull(d, nameof(dividends));
        }

        EnumArguments.Check(cashDividendModel, nameof(cashDividendModel));
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = Value(option, context, process, dividends, cashDividendModel),
        });
    }

    // QuantLib's calculate().
    private static double Value(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        DividendSchedule dividends_,
        CashDividendEuropeanEngineCashDividendModel cashDividendModel)
    {
        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European option");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        double strike = payoff.Strike;

        YieldTermStructure rTS = process.RiskFreeRate;
        YieldTermStructure qTS = process.DividendYield;

        // the settlement and the time to maturity on the process's clock, from its earliest structure date, where
        // QuantLib's are the risk-free curve's
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Date settlementDate = clock.ReferenceDate;
        Date maturityDate = exercise.LastDate();
        double maturity = clock.TimeFromReference(maturityDate);

        // the dividends paid from the settlement to the maturity, positive, by date
        var dividends = new List<(Date Date, double Amount)>();
        foreach (Dividend div in dividends_)
        {
            double amount = div.Amount(context);
            if (div.Date >= settlementDate && div.Date <= maturityDate && amount > 0.0)
            {
                dividends.Add((div.Date, amount));
            }
        }

        dividends = [.. dividends.OrderBy(d => d.Date)];

        if (cashDividendModel == CashDividendEuropeanEngineCashDividendModel.Escrowed
            || (dividends.Count == 1 && dividends[^1].Date == settlementDate))
        {
            var escrowed = new VanillaOption(payoff, exercise);
            return AnalyticDividendEuropeanEngine.Calculate(escrowed, context, process, dividends_).NPV!.Value;
        }

        var underlyings = new List<(Date Date, double Amount)>(dividends);
        if (underlyings.Count != 0 && underlyings[^1].Date == maturityDate)
        {
            underlyings[^1] = (maturityDate, underlyings[^1].Amount + strike);
        }
        else
        {
            underlyings.Add((maturityDate, strike));
        }

        if (underlyings.Count == 1)
        {
            var vanilla = new VanillaOption(
                new PlainVanillaPayoff(payoff.OptionType, underlyings[^1].Amount), exercise);
            return AnalyticEuropeanEngine.Calculate(vanilla, context, process).NPV!.Value;
        }

        BlackVolTermStructure volTS = process.BlackVolatility;
        var zeroRateTS = new FlatForward(settlementDate, 0.0, clock.DayCounter);

        var processes = ImmutableArray.CreateBuilder<GeneralizedBlackScholesProcess>(underlyings.Count);
        foreach ((Date date, double amount) in underlyings)
        {
            double r_mod = Math.Log(rTS.AccruedDiscount(date)) / maturity;
            double q_mod = Math.Log(qTS.AccruedDiscount(date)) / maturity;

            // each dividend's volatility on the clock, so its variance to expiry is the one read by date, where QuantLib
            // dates and counts it on the volatility's own clock but sizes it on the risk-free curve's
            processes.Add(new GeneralizedBlackScholesProcess(
                amount,
                new FlatForward(settlementDate, q_mod - r_mod, clock.DayCounter),
                zeroRateTS,
                new BlackConstantVol(
                    settlementDate,
                    volTS.Calendar,
                    Math.Sqrt(volTS.AccruedVariance(date, strike) / maturity),
                    clock.DayCounter)));
        }

        var v = new Vector(underlyings.Count);
        for (int i = 0; i < underlyings.Count; ++i)
        {
            v[i] = volTS.AccruedVariance(underlyings[i].Date, strike);
        }

        var rho = new Matrix(underlyings.Count, underlyings.Count);
        for (int i = 0; i < underlyings.Count; ++i)
        {
            rho[i, i] = 1.0;
            for (int j = 0; j < i; ++j)
            {
                if (v[j] > QL_EPSILON)
                {
                    rho[i, j] = rho[j, i] = v[j] / Math.Sqrt(v[i] * v[j]);
                }
                else
                {
                    rho[i, j] = rho[j, i] = QL_EPSILON;
                }
            }
        }

        var basketOption = new BasketOption(
            new AverageBasketPayoff(
                new PlainVanillaPayoff(OptionType.Put, process.X0),
                [.. Enumerable.Repeat(1.0, underlyings.Count)]),
            new EuropeanExercise(maturityDate));

        double basketNpv = ChoiBasketEngine.Calculate(
            basketOption, context, processes.MoveToImmutable(), rho, 10, 2000, false, true).NPV!.Value;

        if (payoff.OptionType == OptionType.Call)
        {
            return basketNpv * qTS.AccruedDiscount(maturityDate);
        }
        else
        {
            double divDiscounted = 0.0;
            foreach ((Date date, double amount) in dividends)
            {
                divDiscounted += amount * rTS.AccruedDiscount(date) / qTS.AccruedDiscount(date);
            }

            double fwd = (process.X0 - divDiscounted)
                * qTS.AccruedDiscount(maturityDate) / rTS.AccruedDiscount(maturityDate);
            return (basketNpv * qTS.AccruedDiscount(maturityDate))
                - ((fwd - payoff.Strike) * rTS.AccruedDiscount(maturityDate));
        }
    }
}
