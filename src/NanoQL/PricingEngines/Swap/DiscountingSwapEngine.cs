// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swap/discountingswapengine.hpp ql/pricingengines/swap/discountingswapengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.PricingEngines;

/// <summary>The discounting engine for swaps: it discounts the future cash flows to the curve's reference date.</summary>
/// <remarks>
/// A static class: <c>Calculate</c> takes the engine's inputs and returns the results, in place of QuantLib's engine
/// object, whose <c>discountCurve()</c> accessor has no instance to belong to. Its overloads for
/// <see cref="FixedVsFloatingSwap"/> and <see cref="OvernightIndexedSwap"/> run the same steps and body and return
/// those swaps' results records, so a caller needs no cast.
/// </remarks>
public static class DiscountingSwapEngine
{
    /// <summary>Prices a swap.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static SwapResults Calculate(
        Swap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        CheckArguments(swap, context, discountCurve, settlementDate, npvDate);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        SwapResults results = Body(swap, context, discountCurve, includeSettlementDateFlows, settlementDate, npvDate);
        return swap.FetchResults(results);
    }

    /// <summary>Prices a fixed-vs-floating swap, with the fair rate and spread its results derive.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static FixedVsFloatingSwapResults Calculate(
        FixedVsFloatingSwap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        CheckArguments(swap, context, discountCurve, settlementDate, npvDate);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        SwapResults results = Body(swap, context, discountCurve, includeSettlementDateFlows, settlementDate, npvDate);
        return swap.FetchResults(results);
    }

    /// <summary>Prices an overnight-indexed swap, with the fair rate and spread its results derive.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static OvernightIndexedSwapResults Calculate(
        OvernightIndexedSwap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        CheckArguments(swap, context, discountCurve, settlementDate, npvDate);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        SwapResults results = Body(swap, context, discountCurve, includeSettlementDateFlows, settlementDate, npvDate);
        return swap.FetchResults(results);
    }

    private static void CheckArguments(
        Swap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        Date? settlementDate,
        Date? npvDate)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
    }

    private static SwapResults Body(
        Swap swap,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        bool? includeSettlementDateFlows,
        Date? settlementDateInput,
        Date? npvDateInput)
    {
        double value = 0.0;

        Date refDate = discountCurve.ReferenceDate;

        Date settlementDate;
        if (settlementDateInput is not Date sd)
        {
            settlementDate = refDate;
        }
        else
        {
            settlementDate = sd;
            if (!(settlementDate >= refDate))
            {
                throw new ArgumentException(
                    $"settlement date ({settlementDate}) before discount curve reference date ({refDate})",
                    nameof(settlementDate));
            }
        }

        Date valuationDate;
        if (npvDateInput is not Date nd)
        {
            valuationDate = refDate;
        }
        else
        {
            valuationDate = nd;
            if (!(valuationDate >= refDate))
            {
                throw new ArgumentException(
                    $"npv date ({valuationDate}) before discount curve reference date ({refDate})", "npvDate");
            }
        }

        double npvDateDiscount = discountCurve.Discount(valuationDate);

        ImmutableArray<ImmutableArray<CashFlow>> legs = swap.Legs;
        ImmutableArray<double> payer = swap.PayerMultipliers;
        int n = legs.Length;
        var legNPV = new double?[n];
        var legBPS = new double?[n];
        var startDiscounts = new double?[n];
        var endDiscounts = new double?[n];

        bool includeRefDateFlows = includeSettlementDateFlows ?? context.IncludeReferenceDateEvents;

        for (int i = 0; i < n; ++i)
        {
            try
            {
                (double npv, double bps) = CashFlows.NpvBps(
                    legs[i], discountCurve, context, includeRefDateFlows, settlementDate, valuationDate);
                legNPV[i] = npv * payer[i];
                legBPS[i] = bps * payer[i];

                if (!legs[i].IsEmpty)
                {
                    Date d1 = CashFlows.StartDate(legs[i]);
                    startDiscounts[i] = d1 >= refDate ? discountCurve.Discount(d1) : null;

                    Date d2 = CashFlows.MaturityDate(legs[i]);
                    endDiscounts[i] = d2 >= refDate ? discountCurve.Discount(d2) : null;
                }
                else
                {
                    startDiscounts[i] = null;
                    endDiscounts[i] = null;
                }
            }
            catch (Exception e)
            {
                throw new NanoQLException($"{DataFormattersFunctions.Ordinal(i + 1)} leg: {QuantLibText(e)}", e);
            }

            value += legNPV[i]!.Value;
        }

        return new SwapResults
        {
            NPV = value,
            ErrorEstimate = null,
            ValuationDate = valuationDate,
            NpvDateDiscount = npvDateDiscount,
            LegNPV = [.. legNPV],
            LegBPS = [.. legBPS],
            StartDiscounts = [.. startDiscounts],
            EndDiscounts = [.. endDiscounts],
        };
    }

    // The failure's text as QuantLib's what() gives it: without the parameter suffix .NET appends to an
    // ArgumentException's message.
    private static string QuantLibText(Exception e)
    {
        if (e is ArgumentException { ParamName: { } name })
        {
            // An empty message gives the suffix alone, in the runtime's own wording.
            string suffix = new ArgumentException(string.Empty, name).Message;
            if (e.Message.EndsWith(suffix, StringComparison.Ordinal))
            {
                return e.Message[..^suffix.Length];
            }
        }

        return e.Message;
    }
}
