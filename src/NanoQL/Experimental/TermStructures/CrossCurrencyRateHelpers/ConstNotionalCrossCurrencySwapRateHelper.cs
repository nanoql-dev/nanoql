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
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The rate helper for bootstrapping fixed-vs-floating cross-currency par swaps.</summary>
/// <remarks>
/// <para>
/// This helper represents a par cross-currency swap exchanging a fixed-rate leg against a floating-rate leg in a
/// different currency. Since the swap is quoted at par, the FX spot cancels out and is not required. The
/// <c>collateralOnFixedLeg</c> flag determines which leg is discounted on the given collateral curve, while the other
/// leg's discount curve is the one being bootstrapped.
/// </para>
/// <para>
/// The payment lag, in days, applies to the coupons of both legs; notional exchanges remain on the effective and
/// maturity dates. The convention applies to the schedules and payments of both legs, and the calendar is used to roll
/// and to pay on both. The floating leg's payment frequency defaults to its index's tenor, which is only meaningful
/// for an Ibor index: an overnight index has no payment frequency of its own, so one must be given;
/// <see cref="Frequency.NoFrequency"/> is read as none.
/// </para>
/// <para>
/// The fixed leg's currency is QuantLib's empty currency, <see langword="null"/>, which the engine matches as the
/// foreign currency.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ConstNotionalCrossCurrencySwapRateHelper : CrossCurrencySwapRateHelperBase
{
    private readonly Frequency fixedFrequency;
    private readonly DayCounter fixedDayCount;
    private readonly IborIndex floatIndex;
    private readonly bool collateralOnFixedLeg;
    private readonly bool? useIndexedCoupons;
    private readonly Frequency? floatPaymentFrequency;
    private readonly StubIndexSelection floatStubIndexSelection;

    // Written by the constructor and on a re-dated copy only.
    private ConstNotionalCrossCurrencyFixedVsFloatingSwap xccySwap = null!;

    /// <summary>Initializes a new instance of the <see cref="ConstNotionalCrossCurrencySwapRateHelper"/>
    /// class.</summary>
    /// <param name="fixedRate">The quoted fixed rate.</param>
    /// <param name="tenor">The swap's tenor, from the settlement date.</param>
    /// <param name="fixingDays">The settlement days.</param>
    /// <param name="calendar">The calendar of the settlement, the schedules and the payments.</param>
    /// <param name="convention">The business-day convention of the schedules and payments.</param>
    /// <param name="endOfMonth">Whether the schedules follow the end-of-month rule.</param>
    /// <param name="fixedFrequency">The fixed leg's frequency.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="floatIndex">The floating leg's index.</param>
    /// <param name="collateralCurve">The collateral currency's discount curve, or <see langword="null"/>.</param>
    /// <param name="collateralOnFixedLeg">Whether the collateral curve discounts the fixed leg.</param>
    /// <param name="context">The evaluation context the helper's dates and legs are built from.</param>
    /// <param name="paymentLag">The coupons' payment lag, in days, on both legs.</param>
    /// <param name="useIndexedCoupons">Whether an Ibor leg's coupons are indexed or at par; the context's at-par switch
    /// when <see langword="null"/>.</param>
    /// <param name="floatPaymentFrequency">The floating leg's payment frequency; the index's tenor when
    /// <see langword="null"/>.</param>
    /// <param name="floatStubIndexSelection">The floating Ibor leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">The index, the day counter, the calendar or the context is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or the convention or
    /// the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">An overnight leg has no payment frequency, the tenor is shorter than a
    /// coupon period, or the swap can't be built.</exception>
    public ConstNotionalCrossCurrencySwapRateHelper(
        double fixedRate,
        Period tenor,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        Frequency fixedFrequency,
        DayCounter fixedDayCount,
        IborIndex floatIndex,
        YieldTermStructure? collateralCurve,
        bool collateralOnFixedLeg,
        EvaluationContext context,
        int paymentLag = 0,
        bool? useIndexedCoupons = null,
        Frequency? floatPaymentFrequency = null,
        StubIndexSelection? floatStubIndexSelection = null)
        : base(
            fixedRate, tenor, fixingDays, calendar, convention, endOfMonth, collateralCurve, paymentLag, false, context)
    {
        EnumArguments.Check(fixedFrequency, nameof(fixedFrequency));
        ArgumentNullException.ThrowIfNull(fixedDayCount);
        if (floatIndex is null)
        {
            throw new ArgumentNullException(nameof(floatIndex), "floating index required");
        }

        this.fixedFrequency = fixedFrequency;
        this.fixedDayCount = fixedDayCount;
        this.floatIndex = floatIndex;
        this.collateralOnFixedLeg = collateralOnFixedLeg;
        this.useIndexedCoupons = useIndexedCoupons;
        this.floatPaymentFrequency =
            CrossCurrencyRateHelpersFunctions.NormalizedPaymentFrequency(floatPaymentFrequency);
        this.floatStubIndexSelection = floatStubIndexSelection ?? new StubIndexSelection();
        BuildSwap(context);
    }

    /// <summary>
    /// Gets the underlying swap: a payer swap on unit notionals, its fixed leg at 1%, in QuantLib's empty currency, its
    /// floating leg at a zero spread. QuantLib's swap carries a <c>DiscountingConstNotionalCrossCurrencySwapEngine</c>
    /// on the floating index's currency, the floating leg's discount curve, the empty currency, the fixed leg's, and a
    /// spot of 1, the flows on the settlement date included.
    /// </summary>
    public ConstNotionalCrossCurrencyFixedVsFloatingSwap Swap => xccySwap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The helper isn't bound to a curve, has no collateral curve, or the swap's fair
    /// rate isn't available.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (YieldTermStructure termStructure, YieldTermStructure collateral) = RequireCurves();
        YieldTermStructure fixedLegDiscount = collateralOnFixedLeg ? collateral : termStructure;
        YieldTermStructure floatingLegDiscount = collateralOnFixedLeg ? termStructure : collateral;
        ConstNotionalCrossCurrencyFixedVsFloatingSwapResults results =
            DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
                xccySwap,
                context,
                floatIndex.Currency,
                floatingLegDiscount,
                null,
                fixedLegDiscount,
                1.0,
                includeSettlementDateFlows: true);
        return results.FairFixedRate ?? throw new NanoQLException("Fair fixed rate is not available");
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BuildSwap(context);
    }

    // QuantLib's initializeDates: both legs roll on the helper's calendar and convention. The floating index supplies
    // its fixing calendar for fixings, not for the accrual schedule: taking the roll convention or calendar from the
    // index would make the two legs diverge at a month-end roll and move the implied pillar.
    private void BuildSwap(EvaluationContext context)
    {
        const double nominal = 1.0;
        Schedule fixedSch = CrossCurrencyRateHelpersFunctions.LegSchedule(
            context.EvaluationDate, Tenor, new Period(fixedFrequency), FixingDays, Calendar, Convention, EndOfMonth);

        // An overnight index has no payment frequency of its own, so FloatingLegSchedule requires one to have been
        // supplied.
        Schedule floatSch = CrossCurrencyRateHelpersFunctions.FloatingLegSchedule(
            context.EvaluationDate,
            Tenor,
            FixingDays,
            Calendar,
            Convention,
            EndOfMonth,
            floatIndex,
            floatPaymentFrequency);

        xccySwap = new ConstNotionalCrossCurrencyFixedVsFloatingSwap(
            SwapType.Payer,
            nominal,
            null,
            fixedSch,
            CrossCurrencyRateHelpersFunctions.SampleFixedRate,
            fixedDayCount,
            Convention,
            PaymentLag,
            Calendar,
            nominal,
            floatIndex.Currency,
            floatSch,
            floatIndex,
            0.0,
            Convention,
            PaymentLag,
            Calendar,
            context,
            useIndexedCoupons: useIndexedCoupons,
            floatStubIndexSelection: floatStubIndexSelection);

        InitializeDatesFromLegs(xccySwap.Leg(0), xccySwap.Leg(1));
    }
}
