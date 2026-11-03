// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swap/discountingconstnotionalcrosscurrencyswapengine.hpp ql/pricingengines/swap/discountingconstnotionalcrosscurrencyswapengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.PricingEngines;

/// <summary>
/// The discounting engine of constant-notional swaps whose legs involve two currencies: each leg is discounted on its
/// currency's curve, and the NPV is expressed in the domestic currency.
/// </summary>
/// <remarks>
/// <para>
/// The given domestic and foreign currencies are matched to the swap's legs. The evaluation date is the reference date
/// of either discount curve, which must be equal. The spot rate is quoted as units of the domestic currency for one
/// unit of the foreign currency, for settlement on the NPV date.
/// </para>
/// <para>
/// Static: QuantLib's engine object becomes the arguments of <c>Calculate</c>, one overload per swap type, each
/// returning its swap's results. An explicit FX settlement date converts the quote from that date to the NPV date by
/// the discount factors' parity, as QuantLib's mark-to-market engine does, where QuantLib converts it to the curves'
/// reference date; without one, the quote is the NPV date's, as QuantLib reads it
/// (<c>tests/NanoQL.Tests/Divergences/CrossCurrencySwapNpvDateFxTests.cs</c>).
/// </para>
/// </remarks>
public static class DiscountingConstNotionalCrossCurrencySwapEngine
{
    /// <summary>Prices a constant-notional cross-currency swap.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="domesticCcy">The domestic currency, the NPV's.</param>
    /// <param name="domesticCcyDiscountCurve">The discount curve of cash flows in the domestic currency.</param>
    /// <param name="foreignCcy">The foreign currency.</param>
    /// <param name="foreignCcyDiscountCurve">The discount curve of cash flows in the foreign currency.</param>
    /// <param name="spotFx">The spot rate: units of the domestic currency for one unit of the foreign currency.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <param name="settlementDate">The settlement date; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="spotFxSettleDate">The settlement date of the spot quote, from which it is converted to the NPV
    /// date; none when <see langword="null"/>, the quote then being the NPV date's.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the currencies is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The curves' reference dates differ; or a date is <c>default(Date)</c> or
    /// before the curves' reference date.</exception>
    /// <exception cref="NanoQLException">A leg fails to price, or its currency is neither of the two; the message
    /// names the leg.</exception>
    public static CrossCurrencySwapResults Calculate(
        ConstNotionalCrossCurrencySwap swap,
        EvaluationContext context,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve,
        double spotFx,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        Date? spotFxSettleDate = null)
    {
        CheckArguments(
            swap,
            context,
            domesticCcyDiscountCurve,
            foreignCcyDiscountCurve,
            settlementDate,
            npvDate,
            spotFxSettleDate);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        CrossCurrencySwapResults results = Body(
            swap,
            context,
            domesticCcy,
            domesticCcyDiscountCurve,
            foreignCcy,
            foreignCcyDiscountCurve,
            spotFx,
            includeSettlementDateFlows,
            settlementDate,
            npvDate,
            spotFxSettleDate);
        return swap.FetchResults(results);
    }

    /// <summary>Prices a constant-notional cross-currency basis swap, with the fair spreads its results
    /// derive.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="domesticCcy">The domestic currency, the NPV's.</param>
    /// <param name="domesticCcyDiscountCurve">The discount curve of cash flows in the domestic currency.</param>
    /// <param name="foreignCcy">The foreign currency.</param>
    /// <param name="foreignCcyDiscountCurve">The discount curve of cash flows in the foreign currency.</param>
    /// <param name="spotFx">The spot rate: units of the domestic currency for one unit of the foreign currency.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <param name="settlementDate">The settlement date; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="spotFxSettleDate">The settlement date of the spot quote, from which it is converted to the NPV
    /// date; none when <see langword="null"/>, the quote then being the NPV date's.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the currencies is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The curves' reference dates differ; or a date is <c>default(Date)</c> or
    /// before the curves' reference date.</exception>
    /// <exception cref="NanoQLException">A leg fails to price, or its currency is neither of the two; the message
    /// names the leg.</exception>
    public static ConstNotionalCrossCurrencyBasisSwapResults Calculate(
        ConstNotionalCrossCurrencyBasisSwap swap,
        EvaluationContext context,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve,
        double spotFx,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        Date? spotFxSettleDate = null)
    {
        double Npv(CrossCurrencySwap copy) => Calculate(
            (ConstNotionalCrossCurrencySwap)copy,
            context,
            domesticCcy,
            domesticCcyDiscountCurve,
            foreignCcy,
            foreignCcyDiscountCurve,
            spotFx,
            includeSettlementDateFlows,
            settlementDate,
            npvDate,
            spotFxSettleDate).NPV!.Value;
        var results = (ConstNotionalCrossCurrencyBasisSwapResults)Calculate(
            (ConstNotionalCrossCurrencySwap)swap,
            context,
            domesticCcy,
            domesticCcyDiscountCurve,
            foreignCcy,
            foreignCcyDiscountCurve,
            spotFx,
            includeSettlementDateFlows,
            settlementDate,
            npvDate,
            spotFxSettleDate);
        return results with
        {
            FairPaySpread = FairSpread(swap, 0, swap.PaySpread, results.FairPaySpread, results.NPV, Npv),
            FairRecSpread = FairSpread(swap, 1, swap.RecSpread, results.FairRecSpread, results.NPV, Npv),
        };
    }

    /// <summary>
    /// Prices a constant-notional cross-currency fixed-vs-floating swap, with the fair rate and spread its results
    /// derive.
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="domesticCcy">The domestic currency, the NPV's.</param>
    /// <param name="domesticCcyDiscountCurve">The discount curve of cash flows in the domestic currency.</param>
    /// <param name="foreignCcy">The foreign currency.</param>
    /// <param name="foreignCcyDiscountCurve">The discount curve of cash flows in the foreign currency.</param>
    /// <param name="spotFx">The spot rate: units of the domestic currency for one unit of the foreign currency.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <param name="settlementDate">The settlement date; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curves' reference date when
    /// <see langword="null"/>.</param>
    /// <param name="spotFxSettleDate">The settlement date of the spot quote, from which it is converted to the NPV
    /// date; none when <see langword="null"/>, the quote then being the NPV date's.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the currencies is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The curves' reference dates differ; or a date is <c>default(Date)</c> or
    /// before the curves' reference date.</exception>
    /// <exception cref="NanoQLException">A leg fails to price, or its currency is neither of the two; the message
    /// names the leg.</exception>
    public static ConstNotionalCrossCurrencyFixedVsFloatingSwapResults Calculate(
        ConstNotionalCrossCurrencyFixedVsFloatingSwap swap,
        EvaluationContext context,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve,
        double spotFx,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        Date? spotFxSettleDate = null)
    {
        double Npv(CrossCurrencySwap copy) => Calculate(
            (ConstNotionalCrossCurrencySwap)copy,
            context,
            domesticCcy,
            domesticCcyDiscountCurve,
            foreignCcy,
            foreignCcyDiscountCurve,
            spotFx,
            includeSettlementDateFlows,
            settlementDate,
            npvDate,
            spotFxSettleDate).NPV!.Value;
        var results = (ConstNotionalCrossCurrencyFixedVsFloatingSwapResults)Calculate(
            (ConstNotionalCrossCurrencySwap)swap,
            context,
            domesticCcy,
            domesticCcyDiscountCurve,
            foreignCcy,
            foreignCcyDiscountCurve,
            spotFx,
            includeSettlementDateFlows,
            settlementDate,
            npvDate,
            spotFxSettleDate);
        int floatLeg = swap.Type == SwapType.Payer ? 1 : 0;
        return results with
        {
            FairSpread = FairSpread(swap, floatLeg, swap.FloatSpread, results.FairSpread, results.NPV, Npv),
        };
    }

    // A leg's fair spread: QuantLib's BPS route, unless the leg compounds its spread daily, which makes the route
    // inexact; then solved on the swap's own pricing.
    internal static double? FairSpread(
        CrossCurrencySwap swap,
        int j,
        double spread,
        double? bpsRoute,
        double? npv,
        Func<CrossCurrencySwap, double> npvOf) =>
        swap.CompoundsSpreadDaily(j)
            ? DiscountingSwapEngine.SolvedFairSpread(spread, bpsRoute, npv, x => npvOf(swap.WithLegSpreadShift(j, x)))
            : bpsRoute;

    private static void CheckArguments(
        ConstNotionalCrossCurrencySwap swap,
        EvaluationContext context,
        YieldTermStructure domesticCcyDiscountCurve,
        YieldTermStructure foreignCcyDiscountCurve,
        Date? settlementDate,
        Date? npvDate,
        Date? spotFxSettleDate)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (domesticCcyDiscountCurve is null)
        {
            throw new ArgumentNullException(
                nameof(domesticCcyDiscountCurve), "Discounting term structure handle is empty.");
        }

        if (foreignCcyDiscountCurve is null)
        {
            throw new ArgumentNullException(
                nameof(foreignCcyDiscountCurve), "Discounting term structure handle is empty.");
        }

        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        Date.ThrowIfDefault(spotFxSettleDate, nameof(spotFxSettleDate));
    }

    private static CrossCurrencySwapResults Body(
        ConstNotionalCrossCurrencySwap swap,
        EvaluationContext context,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve,
        double spotFx,
        bool? includeSettlementDateFlows,
        Date? settlementDateInput,
        Date? npvDateInput,
        Date? spotFxSettleDateInput)
    {
        if (domesticCcyDiscountCurve.ReferenceDate != foreignCcyDiscountCurve.ReferenceDate)
        {
            throw new ArgumentException(
                "Term structures should have the same reference date.", nameof(foreignCcyDiscountCurve));
        }

        Date referenceDate = domesticCcyDiscountCurve.ReferenceDate;
        Date settlementDate = referenceDate;
        if (settlementDateInput is Date sd)
        {
            settlementDate = sd;
            if (!(settlementDate >= referenceDate))
            {
                throw new ArgumentException(
                    $"Settlement date ({settlementDate}) cannot be before discount curve reference date " +
                    $"({referenceDate})",
                    "settlementDate");
            }
        }

        // - Instrument::Results
        Date valuationDate = referenceDate;
        if (npvDateInput is Date nd)
        {
            if (!(nd >= referenceDate))
            {
                throw new ArgumentException(
                    $"NPV date ({nd}) cannot be before discount curve reference date ({referenceDate})", "npvDate");
            }

            valuationDate = nd;
        }

        if (spotFxSettleDateInput is Date fd && !(fd >= referenceDate))
        {
            throw new ArgumentException(
                $"FX settlement date ({fd}) cannot be before discount curve reference date ({referenceDate})",
                "spotFxSettleDate");
        }

        ImmutableArray<ImmutableArray<CashFlow>> legs = swap.Legs;
        ImmutableArray<double> payer = swap.PayerMultipliers;
        ImmutableArray<Currency?> currencies = swap.Currencies;
        int numLegs = legs.Length;
        double value = 0.0;

        // - Swap::Results
        var legNPV = new double?[numLegs];
        var legBPS = new double?[numLegs];
        var startDiscounts = new double?[numLegs];
        var endDiscounts = new double?[numLegs];

        // - ConstNotionalCrossCurrencySwap::Results
        var inCcyLegNPV = new double?[numLegs];
        var inCcyLegBPS = new double?[numLegs];
        var npvDateDiscounts = new double?[numLegs];

        bool includeReferenceDateFlows = includeSettlementDateFlows ?? context.IncludeReferenceDateEvents;

        for (int legNo = 0; legNo < numLegs; legNo++)
        {
            try
            {
                // Choose the correct discount curve for the leg.
                YieldTermStructure legDiscountCurve;
                if (currencies[legNo] == domesticCcy)
                {
                    legDiscountCurve = domesticCcyDiscountCurve;
                }
                else
                {
                    if (currencies[legNo] != foreignCcy)
                    {
                        throw new NanoQLException(
                            $"leg ccy ({Currency.Format(currencies[legNo])}) must be domesticCcy " +
                            $"({Currency.Format(domesticCcy)}) or foreignCcy ({Currency.Format(foreignCcy)})");
                    }

                    legDiscountCurve = foreignCcyDiscountCurve;
                }

                npvDateDiscounts[legNo] = legDiscountCurve.Discount(valuationDate);

                // Calculate the NPV and BPS of each leg in its currency.
                (double npv, double bps) = CashFlows.NpvBps(
                    legs[legNo], legDiscountCurve, context, includeReferenceDateFlows, settlementDate, valuationDate);
                double inCcyNpv = npv * payer[legNo];
                double inCcyBps = bps * payer[legNo];
                inCcyLegNPV[legNo] = inCcyNpv;
                inCcyLegBPS[legNo] = inCcyBps;

                // Convert to NPV currency if necessary.
                if (currencies[legNo] != domesticCcy)
                {
                    double spotFxRate = NpvDateFx(
                        spotFx,
                        spotFxSettleDateInput,
                        referenceDate,
                        valuationDate,
                        domesticCcy,
                        domesticCcyDiscountCurve,
                        foreignCcy,
                        foreignCcyDiscountCurve);
                    inCcyNpv *= spotFxRate;
                    inCcyBps *= spotFxRate;
                }

                legNPV[legNo] = inCcyNpv;
                legBPS[legNo] = inCcyBps;

                // Get start date and end date discount for the leg
                Date startDate = CashFlows.StartDate(legs[legNo]);
                startDiscounts[legNo] = startDate >= referenceDate ? legDiscountCurve.Discount(startDate) : null;

                Date maturityDate = CashFlows.MaturityDate(legs[legNo]);
                endDiscounts[legNo] = maturityDate >= referenceDate ? legDiscountCurve.Discount(maturityDate) : null;
            }
            catch (Exception e)
            {
                throw new NanoQLException(
                    $"{DataFormattersFunctions.Ordinal(legNo + 1)} leg: {DiscountingSwapEngine.QuantLibText(e)}", e);
            }

            value += legNPV[legNo]!.Value;
        }

        return new CrossCurrencySwapResults
        {
            NPV = value,
            ErrorEstimate = null,
            ValuationDate = valuationDate,
            LegNPV = [.. legNPV],
            LegBPS = [.. legBPS],
            StartDiscounts = [.. startDiscounts],
            EndDiscounts = [.. endDiscounts],
            InCcyLegNPV = [.. inCcyLegNPV],
            InCcyLegBPS = [.. inCcyLegBPS],
            NpvDateDiscounts = [.. npvDateDiscounts],
        };
    }

    // The spot quote as the NPV date's rate. Without a settlement date the quote is used as it is, the NPV date's by
    // the engine's convention. Given one, QuantLib moves it to the curves' reference date by the parity
    // fx(T1)/fx(T2) = FwdDF_Quote(T1->T2) / FwdDF_Base(T1->T2) and stops there, unless the date is the reference date,
    // which it takes for an unset date; here it moves on to the NPV date, as QuantLib's mark-to-market engine does, a
    // move that is exactly 1 where the NPV date is the reference date.
    private static double NpvDateFx(
        double spotFx,
        Date? spotFxSettleDate,
        Date referenceDate,
        Date valuationDate,
        Currency? domesticCcy,
        YieldTermStructure domesticCcyDiscountCurve,
        Currency? foreignCcy,
        YieldTermStructure foreignCcyDiscountCurve)
    {
        double spotFxRate = spotFx;
        if (spotFxSettleDate is not Date settle)
        {
            return spotFxRate;
        }

        double domesticCcyDf = domesticCcyDiscountCurve.Discount(settle);
        double foreignCcyDf = foreignCcyDiscountCurve.Discount(settle);
        if (foreignCcyDf == 0.0)
        {
            throw new NanoQLException(
                $"Discount Factor associated with currency {Currency.Format(foreignCcy)} at maturity {settle} " +
                "cannot be zero");
        }

        spotFxRate *= domesticCcyDf / foreignCcyDf;
        if (valuationDate != referenceDate)
        {
            double domesticValuationDf = domesticCcyDiscountCurve.Discount(valuationDate);
            if (domesticValuationDf == 0.0)
            {
                throw new NanoQLException(
                    $"Discount factor for currency {Currency.Format(domesticCcy)} at {valuationDate} cannot be zero");
            }

            spotFxRate *= foreignCcyDiscountCurve.Discount(valuationDate) / domesticValuationDf;
        }

        return spotFxRate;
    }
}
