// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2025 Uzair Beg
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/termstructures/crosscurrencyratehelpers.hpp ql/experimental/termstructures/crosscurrencyratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The base of the cross-currency basis swap rate helpers.</summary>
/// <remarks>
/// Its legs are floating legs on unit notionals, the base currency's and the quote currency's, each on its index's
/// own forecasting curve; the bootstrapped curve discounts the leg whose currency isn't the collateral currency. A
/// payment frequency of <see cref="Frequency.NoFrequency"/> is read as none, the schedule then following the index's
/// tenor.
/// </remarks>
[Experimental("NQL0001")]
public abstract class CrossCurrencyBasisSwapRateHelperBase : CrossCurrencySwapRateHelperBase
{
    /// <summary>Initializes a new instance of the <see cref="CrossCurrencyBasisSwapRateHelperBase"/> class.</summary>
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
    /// <param name="paymentLag">The coupons' payment lag, in days.</param>
    /// <param name="quoteCurrencyPaymentFrequency">The quote-currency leg's payment frequency; the base-currency leg's
    /// when <see langword="null"/>, and the index's tenor if that is <see langword="null"/> too.</param>
    /// <param name="useIndexedCoupons">Whether Ibor coupons are indexed or at par; the context's at-par switch when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLagOnNotionalExchanges">Whether the notional exchanges are lagged like the coupons.</param>
    /// <param name="baseStubIndexSelection">The base-currency Ibor leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <param name="quoteStubIndexSelection">The quote-currency Ibor leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An index, the calendar or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or the convention isn't
    /// a member of its enum.</exception>
    /// <exception cref="ArgumentException">An overnight leg has no payment frequency, or the tenor is shorter than a
    /// coupon period.</exception>
    protected CrossCurrencyBasisSwapRateHelperBase(
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
            collateralCurve,
            paymentLag,
            paymentLagOnNotionalExchanges,
            context)
    {
        ArgumentNullException.ThrowIfNull(baseCurrencyIndex);
        ArgumentNullException.ThrowIfNull(quoteCurrencyIndex);
        BaseCcyIdx = baseCurrencyIndex;
        QuoteCcyIdx = quoteCurrencyIndex;
        IsFxBaseCurrencyCollateralCurrency = isFxBaseCurrencyCollateralCurrency;
        IsBasisOnFxBaseCurrencyLeg = isBasisOnFxBaseCurrencyLeg;
        PaymentFrequency = CrossCurrencyRateHelpersFunctions.NormalizedPaymentFrequency(paymentFrequency);
        QuoteCcyPaymentFrequency = CrossCurrencyRateHelpersFunctions.NormalizedPaymentFrequency(
            quoteCurrencyPaymentFrequency);
        UseIndexedCoupons = useIndexedCoupons;
        BaseStubIndexSelection = baseStubIndexSelection ?? new StubIndexSelection();
        QuoteStubIndexSelection = quoteStubIndexSelection ?? new StubIndexSelection();
        InitializeLegs(context);
    }

    /// <summary>Gets the base-currency leg's index.</summary>
    protected IborIndex BaseCcyIdx { get; }

    /// <summary>Gets the quote-currency leg's index.</summary>
    protected IborIndex QuoteCcyIdx { get; }

    /// <summary>Gets a value indicating whether the collateral currency is the base currency.</summary>
    protected bool IsFxBaseCurrencyCollateralCurrency { get; }

    /// <summary>Gets a value indicating whether the basis is quoted on the base-currency leg.</summary>
    protected bool IsBasisOnFxBaseCurrencyLeg { get; }

    /// <summary>Gets the base-currency leg's payment frequency, or <see langword="null"/> for the index's
    /// tenor.</summary>
    protected Frequency? PaymentFrequency { get; }

    /// <summary>Gets the quote-currency leg's payment frequency, or <see langword="null"/> for the base
    /// leg's.</summary>
    protected Frequency? QuoteCcyPaymentFrequency { get; }

    /// <summary>Gets whether Ibor coupons are indexed or at par, or <see langword="null"/> for the context's
    /// switch.</summary>
    protected bool? UseIndexedCoupons { get; }

    /// <summary>Gets the base-currency Ibor leg's stub-index selection.</summary>
    protected StubIndexSelection BaseStubIndexSelection { get; }

    /// <summary>Gets the quote-currency Ibor leg's stub-index selection.</summary>
    protected StubIndexSelection QuoteStubIndexSelection { get; }

    /// <summary>Gets the base-currency leg's schedule.</summary>
    protected Schedule BaseCcySchedule { get; private set; } = null!;

    /// <summary>Gets the quote-currency leg's schedule.</summary>
    protected Schedule QuoteCcySchedule { get; private set; } = null!;

    /// <summary>Gets the base-currency leg, on a unit notional.</summary>
    protected ImmutableArray<CashFlow> BaseCcyIborLeg { get; private set; }

    /// <summary>Gets the quote-currency leg, on a unit notional.</summary>
    protected ImmutableArray<CashFlow> QuoteCcyIborLeg { get; private set; }

    /// <summary>Gets the base-currency leg's discount curve: the collateral curve, or the curve being built.</summary>
    /// <param name="termStructure">The curve being built.</param>
    /// <param name="collateral">The collateral curve.</param>
    /// <returns>The curve.</returns>
    protected YieldTermStructure BaseCcyLegDiscountHandle(
        YieldTermStructure termStructure, YieldTermStructure collateral) =>
        IsFxBaseCurrencyCollateralCurrency ? collateral : termStructure;

    /// <summary>Gets the quote-currency leg's discount curve: the collateral curve, or the curve being built.</summary>
    /// <param name="termStructure">The curve being built.</param>
    /// <param name="collateral">The collateral curve.</param>
    /// <returns>The curve.</returns>
    protected YieldTermStructure QuoteCcyLegDiscountHandle(
        YieldTermStructure termStructure, YieldTermStructure collateral) =>
        IsFxBaseCurrencyCollateralCurrency ? termStructure : collateral;

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        InitializeLegs(context);
    }

    // QuantLib's CrossCurrencyBasisSwapRateHelperBase::initializeDates: the two schedules and legs, and the dates.
    private void InitializeLegs(EvaluationContext context)
    {
        Date evaluationDate = context.EvaluationDate;
        BaseCcySchedule = CrossCurrencyRateHelpersFunctions.FloatingLegSchedule(
            evaluationDate, Tenor, FixingDays, Calendar, Convention, EndOfMonth, BaseCcyIdx, PaymentFrequency);
        BaseCcyIborLeg = CrossCurrencyRateHelpersFunctions.BuildFloatingLeg(
            BaseCcySchedule, BaseCcyIdx, PaymentLag, UseIndexedCoupons, BaseStubIndexSelection, context);

        // If no quote-currency payment frequency was given, fall back to the base-currency payment frequency (which
        // may itself be unset, in which case the quote-currency leg uses its own index tenor).
        Frequency? effectiveQuoteCcyFreq = QuoteCcyPaymentFrequency ?? PaymentFrequency;
        QuoteCcySchedule = CrossCurrencyRateHelpersFunctions.FloatingLegSchedule(
            evaluationDate, Tenor, FixingDays, Calendar, Convention, EndOfMonth, QuoteCcyIdx, effectiveQuoteCcyFreq);
        QuoteCcyIborLeg = CrossCurrencyRateHelpersFunctions.BuildFloatingLeg(
            QuoteCcySchedule, QuoteCcyIdx, PaymentLag, UseIndexedCoupons, QuoteStubIndexSelection, context);

        InitializeDatesFromLegs(BaseCcyIborLeg, QuoteCcyIborLeg);
    }
}
