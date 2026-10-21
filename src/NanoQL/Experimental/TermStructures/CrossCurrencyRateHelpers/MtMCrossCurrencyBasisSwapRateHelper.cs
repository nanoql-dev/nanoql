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
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The rate helper for bootstrapping over mark-to-market cross-currency basis swaps.</summary>
/// <remarks>
/// <para>
/// A helper for a cross-currency swap with a resetting notional: at each accrual-period boundary the notional on the
/// mark-to-market leg is reset to reflect changes in the FX rate, reducing the counterparty and FX risk of the
/// structure. For more details see N. Moreni, A. Pallavicini (2015), FX Modelling in Collateralized Markets: foreign
/// measures, basis curves and pricing formulae.
/// </para>
/// <para>
/// The coupon payment lag applies to both legs; notional exchanges remain on the effective and maturity dates. In both
/// frequency parameters, <see cref="Frequency.NoFrequency"/> is read as none.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class MtMCrossCurrencyBasisSwapRateHelper : CrossCurrencyBasisSwapRateHelperBase
{
    private readonly bool isFxBaseCurrencyLegResettable;

    // Written by the constructor and on a re-dated copy only.
    private MtMCrossCurrencyBasisSwap swap = null!;

    /// <summary>Initializes a new instance of the <see cref="MtMCrossCurrencyBasisSwapRateHelper"/> class.</summary>
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
    /// <param name="isFxBaseCurrencyLegResettable">Whether the base-currency leg's notional resets.</param>
    /// <param name="context">The evaluation context the helper's dates and legs are built from.</param>
    /// <param name="paymentFrequency">The base-currency leg's payment frequency; the index's tenor when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLag">The coupons' payment lag, in days, on both legs.</param>
    /// <param name="quoteCurrencyPaymentFrequency">The quote-currency leg's payment frequency; the base-currency leg's
    /// when <see langword="null"/>, and the index's tenor if that is <see langword="null"/> too.</param>
    /// <param name="fxResetFixingDays">The business days by which each FX fixing precedes its accrual-start value
    /// date.</param>
    /// <param name="fxResetFixingCalendar">The calendar of the FX fixing offset; for a non-zero fixing lag, the
    /// resettable leg's schedule calendar when <see langword="null"/>.</param>
    /// <param name="useIndexedCoupons">Whether Ibor coupons are indexed or at par; the context's at-par switch when
    /// <see langword="null"/>.</param>
    /// <param name="baseStubIndexSelection">The base-currency Ibor leg's stub-index selection, whose candidate indexes
    /// must use exogenous forwarding curves; none when <see langword="null"/>.</param>
    /// <param name="quoteStubIndexSelection">The quote-currency Ibor leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An index, the calendar or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Fixing days are negative, or the convention isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException">An overnight leg has no payment frequency, the tenor is shorter than a
    /// coupon period, or the swap can't be built.</exception>
    public MtMCrossCurrencyBasisSwapRateHelper(
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
        bool isFxBaseCurrencyLegResettable,
        EvaluationContext context,
        Frequency? paymentFrequency = null,
        int paymentLag = 0,
        Frequency? quoteCurrencyPaymentFrequency = null,
        int fxResetFixingDays = 0,
        Calendar? fxResetFixingCalendar = null,
        bool? useIndexedCoupons = null,
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
            false,
            baseStubIndexSelection,
            quoteStubIndexSelection)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fxResetFixingDays);
        this.isFxBaseCurrencyLegResettable = isFxBaseCurrencyLegResettable;
        FxResetFixingDays = fxResetFixingDays;
        FxResetFixingCalendar = fxResetFixingCalendar;
        BuildSwap(context);
    }

    /// <summary>
    /// Gets the underlying par swap: unit notionals, zero spreads and spot FX of 1, so that its fair spread on the
    /// basis leg reproduces the helper's quote; it pays the base-currency leg. QuantLib's swap carries a
    /// <c>DiscountingMtMCrossCurrencyBasisSwapEngine</c> on the quote currency, its leg's discount curve, the base
    /// currency, its leg's, and a spot of 1, the flows on the settlement date included, which
    /// <see cref="DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate"/> takes as its arguments.
    /// </summary>
    public MtMCrossCurrencyBasisSwap Swap => swap;

    /// <summary>Gets the number of business days from an FX value date to its fixing date.</summary>
    public int FxResetFixingDays { get; }

    /// <summary>Gets the calendar used to determine FX fixing dates, <see langword="null"/> for none.</summary>
    public Calendar? FxResetFixingCalendar { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The helper isn't bound to a curve, has no collateral curve, or the swap's fair
    /// spread on the basis leg isn't available.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (YieldTermStructure termStructure, YieldTermStructure collateral) = RequireCurves();
        MtMCrossCurrencyBasisSwapResults results = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap,
            context,
            QuoteCcyIdx.Currency,
            QuoteCcyLegDiscountHandle(termStructure, collateral),
            BaseCcyIdx.Currency,
            BaseCcyLegDiscountHandle(termStructure, collateral),
            1.0,
            includeSettlementDateFlows: true);
        return IsBasisOnFxBaseCurrencyLeg
            ? results.FairFxBaseSpread ?? throw new NanoQLException("Fair FX-base spread is not available")
            : results.FairFxQuoteSpread ?? throw new NanoQLException("Fair FX-quote spread is not available");
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        base.InitializeDates(context);
        BuildSwap(context);
    }

    private void BuildSwap(EvaluationContext context) =>
        swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
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
            isFxBaseCurrencyLegResettable,
            context,
            FxResetFixingDays,
            FxResetFixingCalendar,
            PaymentLag,
            PaymentLag,
            Convention,
            Convention,
            useIndexedCoupons: UseIndexedCoupons,
            fxBaseStubIndexSelection: BaseStubIndexSelection,
            fxQuoteStubIndexSelection: QuoteStubIndexSelection);
}
