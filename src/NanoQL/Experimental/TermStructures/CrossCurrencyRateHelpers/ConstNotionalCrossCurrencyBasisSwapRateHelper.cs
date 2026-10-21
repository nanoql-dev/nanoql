// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2025 Uzair Beg
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/termstructures/crosscurrencyratehelpers.hpp ql/experimental/termstructures/crosscurrencyratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The rate helper for bootstrapping over constant-notional cross-currency basis swaps.</summary>
/// <remarks>
/// <para>
/// Unlike marked-to-market cross-currency swaps, both notionals, expressed in base and quote currency, remain constant
/// throughout the lifetime of the swap.
/// </para>
/// <para>
/// A note on the conventions used: consider the currency pair EUR-USD. EUR is the base currency, while USD is the quote
/// currency; the quote currency is the amount paid in that currency for one unit of base currency. Hence, for a
/// cross-currency swap there is a base-currency leg and a quote-currency leg, and the instrument's parameters
/// (collateral currency, basis, resetting or constant-notional legs) are defined relative to them. For example, for
/// EUR-USD basis swaps the collateral is paid in the quote currency (USD), and the basis is given on the base-currency
/// leg (EUR). For more details see N. Moreni, A. Pallavicini (2015), FX Modelling in Collateralized Markets: foreign
/// measures, basis curves and pricing formulae.
/// </para>
/// <para>
/// The coupon payment lag applies to both legs; notional exchanges remain on the effective and maturity dates unless
/// <c>paymentLagOnNotionalExchanges</c> is set, when both are lagged like the coupons. In both frequency parameters,
/// <see cref="Frequency.NoFrequency"/> is read as none.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ConstNotionalCrossCurrencyBasisSwapRateHelper : CrossCurrencyBasisSwapRateHelperBase
{
    // Written by the constructor and on a re-dated copy only.
    private ConstNotionalCrossCurrencyBasisSwap swap = null!;

    /// <summary>Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyBasisSwapRateHelper"/>
    /// class.</summary>
    /// <param name="basis">The quoted basis.</param>
    /// <param name="tenor">The swap's tenor, from the settlement date.</param>
    /// <param name="fixingDays">The settlement days.</param>
    /// <param name="calendar">The calendar of the settlement and the schedules.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedules follow the end-of-month rule.</param>
    /// <param name="baseCurrencyIndex">The base-currency leg's index.</param>
    /// <param name="quoteCurrencyIndex">The quote-currency leg's index.</param>
    /// <param name="collateralCurve">The collateral currency's discount curve, or <see langword="null"/>.</param>
    /// <param name="isFxBaseCurrencyCollateralCurrency">Whether the collateral currency is the base currency.</param>
    /// <param name="isBasisOnFxBaseCurrencyLeg">Whether the basis is quoted on the base-currency leg.</param>
    /// <param name="context">The evaluation context the helper's dates and legs are built from.</param>
    /// <param name="paymentFrequency">The base-currency leg's payment frequency; the index's tenor when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLag">The coupons' payment lag, in days, on both legs.</param>
    /// <param name="quoteCurrencyPaymentFrequency">The quote-currency leg's payment frequency; the base-currency leg's
    /// when <see langword="null"/>, and the index's tenor if that is <see langword="null"/> too.</param>
    /// <param name="useIndexedCoupons">Whether Ibor coupons are indexed or at par; the context's at-par switch when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLagOnNotionalExchanges">Whether both notional exchanges are lagged like the coupons.</param>
    /// <param name="baseStubIndexSelection">The base-currency Ibor leg's stub-index selection, whose candidate indexes
    /// must use exogenous forwarding curves; none when <see langword="null"/>.</param>
    /// <param name="quoteStubIndexSelection">The quote-currency Ibor leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An index, the calendar or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or the convention isn't
    /// a member of its enum.</exception>
    /// <exception cref="ArgumentException">An overnight leg has no payment frequency, the tenor is shorter than a
    /// coupon period, or the swap can't be built.</exception>
    public ConstNotionalCrossCurrencyBasisSwapRateHelper(
        double basis,
        Period tenor,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        IborIndex baseCurrencyIndex,
        IborIndex quoteCurrencyIndex,
        YieldTermStructure? collateralCurve,
        bool isFxBaseCurrencyCollateralCurrency,
        bool isBasisOnFxBaseCurrencyLeg,
        EvaluationContext context,
        Frequency? paymentFrequency = null,
        int paymentLag = 0,
        Frequency? quoteCurrencyPaymentFrequency = null,
        bool? useIndexedCoupons = null,
        bool paymentLagOnNotionalExchanges = false,
        StubIndexSelection? baseStubIndexSelection = null,
        StubIndexSelection? quoteStubIndexSelection = null)
        : base(
            basis,
            tenor,
            fixingDays,
            calendar,
            convention,
            endOfMonth,
            baseCurrencyIndex,
            quoteCurrencyIndex,
            collateralCurve,
            isFxBaseCurrencyCollateralCurrency,
            isBasisOnFxBaseCurrencyLeg,
            context,
            paymentFrequency,
            paymentLag,
            quoteCurrencyPaymentFrequency,
            useIndexedCoupons,
            paymentLagOnNotionalExchanges,
            baseStubIndexSelection,
            quoteStubIndexSelection)
    {
        BuildSwap(context);
    }

    /// <summary>
    /// Gets the underlying par swap: unit notionals, zero spreads and spot FX of 1, so that its fair spread on the
    /// basis leg reproduces the helper's quote; it pays the base-currency leg. QuantLib's swap carries a
    /// <c>DiscountingConstNotionalCrossCurrencySwapEngine</c> on the quote currency, its leg's discount curve, the base
    /// currency, its leg's, and a spot of 1, the flows on the settlement date included, which
    /// <see cref="PricingEngines.DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(ConstNotionalCrossCurrencyBasisSwap, EvaluationContext, Currency?, YieldTermStructure, Currency?, YieldTermStructure, double, bool?, Date?, Date?, Date?)"/>
    /// takes as its arguments.
    /// </summary>
    public ConstNotionalCrossCurrencyBasisSwap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The helper isn't bound to a curve, has no collateral curve, or its basis leg's
    /// BPS is zero ("null BPS").</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (YieldTermStructure termStructure, YieldTermStructure collateral) = RequireCurves();

        (double npvBaseCcy, double bpsBaseCcy) = CrossCurrencyRateHelpersFunctions.NpvBpsConstNotionalLeg(
            BaseCcyIborLeg,
            InitialNotionalExchangeDate,
            FinalNotionalExchangeDate,
            BaseCcyLegDiscountHandle(termStructure, collateral),
            context);
        (double npvQuoteCcy, double bpsQuoteCcy) = CrossCurrencyRateHelpersFunctions.NpvBpsConstNotionalLeg(
            QuoteCcyIborLeg,
            InitialNotionalExchangeDate,
            FinalNotionalExchangeDate,
            QuoteCcyLegDiscountHandle(termStructure, collateral),
            context);

        double bps = IsBasisOnFxBaseCurrencyLeg ? -bpsBaseCcy : bpsQuoteCcy;
        if (!(Math.Abs(bps) > 0.0))
        {
            throw new NanoQLException("null BPS");
        }

        return -(npvQuoteCcy - npvBaseCcy) / bps;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        base.InitializeDates(context);
        BuildSwap(context);
    }

    private void BuildSwap(EvaluationContext context) =>
        swap = new ConstNotionalCrossCurrencyBasisSwap(
            1.0,
            BaseCcyIdx.Currency,
            BaseCcySchedule,
            BaseCcyIdx,
            0.0,
            1.0,
            1.0,
            QuoteCcyIdx.Currency,
            QuoteCcySchedule,
            QuoteCcyIdx,
            0.0,
            1.0,
            context,
            PaymentLag,
            PaymentLag,
            useIndexedCoupons: UseIndexedCoupons,
            paymentLagOnNotionalExchanges: PaymentLagOnNotionalExchanges,
            payStubIndexSelection: BaseStubIndexSelection,
            recStubIndexSelection: QuoteStubIndexSelection);
}
