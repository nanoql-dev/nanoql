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
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The base of the cross-currency swap rate helpers: their conventions, collateral curve and dates.</summary>
/// <remarks>
/// The helpers bootstrap the discount curve of the leg that isn't collateralized: bound to the curve being built, they
/// discount that leg on it (QuantLib's relinked handle) and the other on the collateral curve. A collateral curve of
/// <see langword="null"/> is QuantLib's empty handle, with which the implied quote refuses.
/// </remarks>
[Experimental("NQL0001")]
public abstract class CrossCurrencySwapRateHelperBase : RelativeDateRateHelper
{
    /// <summary>Initializes a new instance of the <see cref="CrossCurrencySwapRateHelperBase"/> class.</summary>
    /// <param name="quote">The quote the helper reprices.</param>
    /// <param name="tenor">The swap's tenor, from the settlement date.</param>
    /// <param name="fixingDays">The settlement days.</param>
    /// <param name="calendar">The calendar of the settlement, the schedules and the payments.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedules follow the end-of-month rule.</param>
    /// <param name="collateralCurve">The collateral currency's discount curve, or <see langword="null"/>.</param>
    /// <param name="paymentLag">The coupons' payment lag, in days.</param>
    /// <param name="paymentLagOnNotionalExchanges">Whether the notional exchanges are lagged like the coupons.</param>
    /// <param name="context">The evaluation context the helper's dates are computed from.</param>
    /// <exception cref="ArgumentNullException">The calendar or the context is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or the convention isn't
    /// a member of its enum.</exception>
    protected CrossCurrencySwapRateHelperBase(
        double quote,
        Period tenor,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        YieldTermStructure? collateralCurve,
        int paymentLag,
        bool paymentLagOnNotionalExchanges,
        EvaluationContext context)
        : base(quote, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);

        // QuantLib uses the calendar unchecked, and every use of its empty one fails.
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        Tenor = tenor;
        FixingDays = fixingDays;
        Calendar = calendar;
        Convention = convention;
        EndOfMonth = endOfMonth;
        PaymentLag = paymentLag;
        PaymentLagOnNotionalExchanges = paymentLagOnNotionalExchanges;
        CollateralHandle = collateralCurve;
    }

    /// <summary>Gets the swap's tenor.</summary>
    protected Period Tenor { get; }

    /// <summary>Gets the settlement days.</summary>
    protected int FixingDays { get; }

    /// <summary>Gets the calendar.</summary>
    protected Calendar Calendar { get; }

    /// <summary>Gets the business-day convention.</summary>
    protected BusinessDayConvention Convention { get; }

    /// <summary>Gets a value indicating whether the schedules follow the end-of-month rule.</summary>
    protected bool EndOfMonth { get; }

    /// <summary>Gets the coupons' payment lag, in days.</summary>
    protected int PaymentLag { get; }

    /// <summary>Gets a value indicating whether the notional exchanges are lagged like the coupons.</summary>
    protected bool PaymentLagOnNotionalExchanges { get; }

    /// <summary>Gets the collateral currency's discount curve, or <see langword="null"/>.</summary>
    protected YieldTermStructure? CollateralHandle { get; }

    /// <summary>Gets the date of the initial notional exchange.</summary>
    protected Date InitialNotionalExchangeDate { get; private set; }

    /// <summary>Gets the date of the final notional exchange.</summary>
    protected Date FinalNotionalExchangeDate { get; private set; }

    /// <summary>
    /// The helper's dates from its two legs: the earliest start and the latest maturity, the notional exchanges on them
    /// (lagged like the coupons when <see cref="PaymentLagOnNotionalExchanges"/> is set), and the latest relevant date,
    /// the later of the maturity and the last payment.
    /// </summary>
    /// <param name="firstLeg">The first leg.</param>
    /// <param name="secondLeg">The second leg.</param>
    protected void InitializeDatesFromLegs(ImmutableArray<CashFlow> firstLeg, ImmutableArray<CashFlow> secondLeg)
    {
        Date firstStart = CashFlows.StartDate(firstLeg);
        Date secondStart = CashFlows.StartDate(secondLeg);
        Date earliest = secondStart < firstStart ? secondStart : firstStart;
        earliestDate = earliest;

        Date firstMaturity = CashFlows.MaturityDate(firstLeg);
        Date secondMaturity = CashFlows.MaturityDate(secondLeg);
        Date maturity = firstMaturity < secondMaturity ? secondMaturity : firstMaturity;
        maturityDate = maturity;

        // Principal exchanges settle on the effective and maturity dates: the payment lag applies only to coupons --
        // unless the swap's convention lags the principal flows too, in which case both exchanges move by the same lag
        // as the coupons and the final exchange settles together with the final coupon. Advance with zero days
        // reduces to Adjust, preserving the default behaviour.
        int exchangeLag = PaymentLagOnNotionalExchanges ? PaymentLag : 0;
        InitialNotionalExchangeDate = Calendar.Advance(earliest, exchangeLag, TimeUnit.Days, Convention);
        FinalNotionalExchangeDate = Calendar.Advance(maturity, exchangeLag, TimeUnit.Days, Convention);

        Date firstLast = firstLeg[^1].Date;
        Date secondLast = secondLeg[^1].Date;
        Date lastPaymentDate = firstLast < secondLast ? secondLast : firstLast;
        Date latest = maturity < lastPaymentDate ? lastPaymentDate : maturity;
        latestRelevantDate = latest;
        latestDate = latest;
    }

    /// <summary>The curve being built and the collateral curve, QuantLib's two handles, refusing either
    /// missing.</summary>
    /// <returns>The two curves.</returns>
    /// <exception cref="NanoQLException">The helper isn't bound to a curve, or has no collateral curve.</exception>
    protected (YieldTermStructure TermStructure, YieldTermStructure Collateral) RequireCurves()
    {
        RequireTermStructure();
        YieldTermStructure collateral =
            CollateralHandle ?? throw new NanoQLException("collateral term structure not set");
        return (TermStructure!, collateral);
    }
}
