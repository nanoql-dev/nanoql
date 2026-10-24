// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swap/discountingswapengine.hpp ql/pricingengines/swap/discountingswapengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.PricingEngines;

/// <summary>The discounting engine for swaps: it discounts the future cash flows to the curve's reference date.</summary>
/// <remarks>
/// A static class: <c>Calculate</c> takes the engine's inputs and returns the results, in place of QuantLib's engine
/// object, whose <c>discountCurve()</c> accessor has no instance to belong to. Its overloads for
/// <see cref="FixedVsFloatingSwap"/>, <see cref="OvernightIndexedSwap"/>, <see cref="BMASwap"/>,
/// <see cref="FloatFloatSwap"/>, <see cref="YearOnYearInflationSwap"/> and <see cref="CPISwap"/> run the same steps and
/// body and return those swaps' results records, so a caller needs no cast; the <see cref="CPISwap"/> overload also
/// gives the fair rate, which QuantLib's swap derives from the CPI leg's BPS (see <see cref="CPISwapResults"/>), and
/// the <see cref="FloatFloatSwap"/> overload solves a capped or floored leg's fair spread.
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

    /// <summary>Prices a BMA swap, with the inspectors its results derive.</summary>
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
    public static BMASwapResults Calculate(
        BMASwap swap,
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

    /// <summary>Prices a float-float swap, with the fair spreads that zero its NPV.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <remarks>
    /// A leg's fair spread is QuantLib's, derived from its BPS, where the leg has no cap or floor: there its amount
    /// moves one for one with its spread, and the BPS route is exact. On a capped or floored leg QuantLib's BPS route
    /// doesn't zero the NPV, once a cap or floor binds; there the fair spread is the leg's first spread plus the
    /// uniform shift of its coupons' spreads that zeroes the NPV, solved on the swap's own pricing, each coupon keeping
    /// its pricer, cap and floor. The shift is bracketed from the BPS route's estimate within the spreads the pricers
    /// price, and is <see langword="null"/> where no shift in that domain, within a shift of 100%, changes the NPV's
    /// sign (the S-rx review's B1-1, under R8;
    /// <c>tests/NanoQL.Tests/Divergences/FloatFloatSwapOptionalLegFairSpreadTests.cs</c>).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static FloatFloatSwapResults Calculate(
        FloatFloatSwap swap,
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
        FloatFloatSwapResults fetched = swap.FetchResults(results);
        double Npv(FloatFloatSwap shifted) =>
            Body(shifted, context, discountCurve, includeSettlementDateFlows, settlementDate, npvDate).NPV!.Value;
        return fetched with
        {
            FairSpread1 = FairSpread(swap, 0, fetched.FairSpread1, fetched.NPV, Npv),
            FairSpread2 = FairSpread(swap, 1, fetched.FairSpread2, fetched.NPV, Npv),
        };
    }

    /// <summary>Prices a year-on-year inflation swap, with the fair rate and spread its results derive.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static YearOnYearInflationSwapResults Calculate(
        YearOnYearInflationSwap swap,
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

    /// <summary>
    /// Prices a CPI swap, with the fair rate that zeroes it and the fair spread its results derive.
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <remarks>
    /// The fair rate is <c>fixedRate - NPV / (p S)</c>, where <c>p</c> is the CPI leg's sign and <c>S</c> its
    /// sensitivity to its rate: the NPV, on this curve and under these dates, of the swap's CPI coupons rebuilt at a
    /// unit rate, so the swap rebuilt at the fair rate is worth zero. It is <see langword="null"/> when <c>S</c> is
    /// zero, the CPI leg having no live coupon. QuantLib's <c>fixedRate - NPV / (legBPS[0] / 1bp)</c> leaves out the
    /// index ratio a CPI coupon's sensitivity carries, a documented divergence.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static CPISwapResults Calculate(
        CPISwap swap,
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

        // the CPI leg's sensitivity to its rate, under the body's dates
        double sensitivity;
        try
        {
            sensitivity = CashFlows.Npv(
                swap.UnitRateCpiCoupons(),
                discountCurve,
                context,
                includeSettlementDateFlows ?? context.IncludeReferenceDateEvents,
                settlementDate ?? discountCurve.ReferenceDate,
                results.ValuationDate);
        }
        catch (Exception e)
        {
            throw new NanoQLException($"{DataFormattersFunctions.Ordinal(1)} leg: {QuantLibText(e)}", e);
        }

        double? fairRate = sensitivity != 0.0 && results.NPV is double npv
            ? swap.FixedRate - (npv / (swap.PayerMultipliers[0] * sensitivity))
            : null;
        return swap.FetchResults(new CPISwapResults(results) { FairRate = fairRate });
    }

    /// <summary>
    /// Prices a zero-coupon swap, with the fixed payment that zeroes it.
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <remarks>
    /// The fair payment is QuantLib's <c>fairFixedPayment</c>, the floating leg's NPV over the discount at the fixed
    /// amount's payment date, signed, with the discount read at the swap's payment date, where QuantLib reads its fixed
    /// leg's end discount, at the leg's maturity date: the accrual end for the fixed-rate constructor's coupon, so with
    /// a payment delay, or an adjusted payment date, its fair payment fed back leaves a non-zero NPV (the S-cms plan's
    /// CMR5 (xxiv)). For the fixed-payment constructor the two dates are one, and the payment QuantLib's. The floating
    /// leg's NPV, given at the NPV date, is taken back to the curve's reference date by the NPV date's discount, as
    /// QuantLib's asset swap scales its fair values (<c>assetswap.cpp:273</c>, <c>:294</c>), where QuantLib's divides
    /// it by a discount from the reference date, so with an NPV date after the reference date its fair payment is high
    /// by that discount's reciprocal (with CMR5 (xxiv)); at the default NPV date the discount is 1.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static ZeroCouponSwapResults Calculate(
        ZeroCouponSwap swap,
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

        // Knowing that for the fair payment NPV = 0.0, where:
        // NPV = (discount at fixed amount pay date) * (payer\receiver * fixed amount)
        //     + (discount at float amount pay date) * (-payer\receiver * float amount)
        // we have:
        // fair amount = NPV float / discount at fixed amount pay date
        // with NPV float corrected for the payer sign.
        double scaling = swap.Payer(1) ? -1.0 : 1.0;
        double? fairFixedPayment = results.LegNPV[1] is double floatingNpv
            ? floatingNpv * (results.NpvDateDiscount ?? 1.0) / (discountCurve.Discount(swap.PaymentDate) * scaling)
            : null;
        return swap.FetchResults(new ZeroCouponSwapResults(results) { FairFixedPayment = fairFixedPayment });
    }

    /// <summary>
    /// Prices a multiple-resets swap, with its fair spread solved on a compounded leg.
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <param name="settlementDate">The settlement date; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the curve's reference date when
    /// <see langword="null"/>.</param>
    /// <returns>The swap's results: the expired results when the swap has expired.</returns>
    /// <remarks>
    /// On a compounded leg the spread compounds with each fixing, so QuantLib's fair spread, linear in the floating
    /// leg's BPS, misses par (the S-cms plan's CMR5 (xv)); here it is the spread at which the swap, rebuilt at it and
    /// priced on this curve and under these dates, is worth zero, solved by Brent's method to 1e-12 from QuantLib's
    /// estimate. It is <see langword="null"/> when the floating leg's BPS is zero or unknown, as QuantLib's. On an
    /// averaged leg the spread is linear, and the swap's results derive QuantLib's.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The settlement date or the NPV date is <c>default(Date)</c>, or before the curve's reference date.
    /// </exception>
    /// <exception cref="NanoQLException">A leg fails to price; the message names the leg.</exception>
    public static FixedVsFloatingSwapResults Calculate(
        MultipleResetsSwap swap,
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
        double? fairSpread = null;
        if (swap.AveragingMethod == RateAveragingType.Compound
            && results.LegBPS[1] is double bps && bps != 0.0 && results.NPV is double npv)
        {
            double guess = swap.Spread - (npv / (bps / 1.0e-4));
            double Npv(double spread) =>
                Body(swap.WithSpread(spread), context, discountCurve, includeSettlementDateFlows, settlementDate, npvDate).NPV
                ?? throw new NanoQLException("result not available");
            fairSpread = new Brent().Solve(Npv, 1.0e-12, guess, 1.0e-4);
        }

        return swap.FetchResults(new FixedVsFloatingSwapResults(results) { FairSpread = fairSpread });
    }

    /// <summary>
    /// Prices a swap as QuantLib's engine does on an empty discount-curve handle, the engine a helper sets when it is
    /// given none: an expired swap's results, which QuantLib's instrument gives before it calls the engine, and
    /// otherwise QuantLib's refusal, once the swap's arguments are validated (<c>discountingswapengine.cpp:42</c>).
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The expired swap's results.</returns>
    /// <exception cref="NanoQLException">The swap has not expired.</exception>
    internal static SwapResults CalculateWithoutCurve(Swap swap, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        throw new NanoQLException("discounting term structure handle is empty");
    }

    /// <summary>
    /// Prices a swap as QuantLib's engine does on an empty discount-curve handle, the engine a builder's
    /// <c>WithDiscountingTermStructure(null)</c> sets: an expired swap's results, which QuantLib's instrument gives
    /// before it calls the engine, and otherwise QuantLib's refusal, once the swap's arguments are validated
    /// (<c>discountingswapengine.cpp:42</c>).
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The expired swap's results.</returns>
    /// <exception cref="NanoQLException">The swap has not expired.</exception>
    internal static FixedVsFloatingSwapResults CalculateWithoutCurve(
        FixedVsFloatingSwap swap, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        throw new NanoQLException("discounting term structure handle is empty");
    }

    /// <summary>
    /// Prices a BMA swap as QuantLib's engine does on an empty discount-curve handle, the engine the BMA swap rate
    /// helper builds on an Ibor index with no forwarding curve: an expired swap's results, and otherwise QuantLib's
    /// refusal, once the swap's arguments are validated.
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The expired swap's results.</returns>
    /// <exception cref="NanoQLException">The swap has not expired.</exception>
    internal static BMASwapResults CalculateWithoutCurve(BMASwap swap, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        throw new NanoQLException("discounting term structure handle is empty");
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

    // A float-float swap's fair spread on a leg: QuantLib's BPS route on a leg with no cap or floor, the solved one on
    // a capped or floored leg (see the overload's remarks).
    private static double? FairSpread(
        FloatFloatSwap swap, int j, double? bpsRoute, double? npv, Func<FloatFloatSwap, double> npvOf)
    {
        if (!swap.IsOptional(j) || bpsRoute is not double estimate || npv is not double npv0)
        {
            return bpsRoute;
        }

        double spread = (j == 0 ? swap.Spread1 : swap.Spread2)[0];
        double? shift = ZeroNpvShift(x => npvOf(swap.WithLegSpreadShift(j, x)), npv0, estimate - spread);
        return shift is double s ? spread + s : null;
    }

    // A leg's fair spread solved on the swap's own pricing, from QuantLib's BPS route as the estimate: its first spread
    // plus the shift of its coupons' spreads that zeroes the NPV, null where none does or where the BPS route gives
    // none. The cross-currency swaps' legs compounding their spreads take it (the S-rx plan's C3 review, B1-2).
    internal static double? SolvedFairSpread(
        double spread, double? bpsRoute, double? npv, Func<double, double> npvAtShift)
    {
        if (bpsRoute is not double estimate || npv is not double npv0)
        {
            return bpsRoute;
        }

        double? shift = ZeroNpvShift(npvAtShift, npv0, estimate - spread);
        return shift is double s ? spread + s : null;
    }

    // The shift whose NPV is zero, the NPV being monotone in it: steps from the estimate, doubling, until the NPV
    // changes sign, then Brent's solve between the last two shifts. A shift the pricers refuse (a lognormal caplet's
    // strike past its displacement) ends the domain: its end is approached by bisection, and without a sign change by
    // then there is no root.
    private static double? ZeroNpvShift(Func<double, double> npvAt, double npv0, double estimate)
    {
        const double MaxShift = 1.0;
        if (npv0 == 0.0)
        {
            return 0.0;
        }

        double last = 0.0;
        double step = estimate;
        while (Math.Abs(step) <= MaxShift)
        {
            if (!TryNpv(npvAt, step, out double npv))
            {
                double valid = last, invalid = step, npvValid = npv0;
                for (int i = 0; i < 100; ++i)
                {
                    double mid = 0.5 * (valid + invalid);
                    if (mid == valid || mid == invalid)
                    {
                        break;
                    }

                    if (TryNpv(npvAt, mid, out double npvMid))
                    {
                        valid = mid;
                        npvValid = npvMid;
                    }
                    else
                    {
                        invalid = mid;
                    }
                }

                if (npvValid == 0.0)
                {
                    return valid;
                }

                return Math.Sign(npvValid) != Math.Sign(npv0) ? Solve(npvAt, last, valid) : null;
            }

            if (npv == 0.0)
            {
                return step;
            }

            if (Math.Sign(npv) != Math.Sign(npv0))
            {
                return Solve(npvAt, last, step);
            }

            last = step;
            step *= 2.0;
        }

        return null;
    }

    private static bool TryNpv(Func<double, double> npvAt, double shift, out double npv)
    {
        try
        {
            npv = npvAt(shift);
            return true;
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            npv = 0.0;
            return false;
        }
    }

    private static double Solve(Func<double, double> f, double a, double b) =>
        new Brent().Solve(f, 1.0e-12, 0.5 * (a + b), Math.Min(a, b), Math.Max(a, b));

    // The failure's text as QuantLib's what() gives it: without the parameter suffix .NET appends to an
    // ArgumentException's message. The bootstraps' wrapped texts call it too.
    internal static string QuantLibText(Exception e)
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
