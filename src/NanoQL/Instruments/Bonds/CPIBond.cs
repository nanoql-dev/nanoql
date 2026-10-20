// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/cpibond.hpp ql/instruments/bonds/cpibond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A zero-inflation-indexed bond: fixed coupon rates inflated by the ratio of a zero-inflation index to its base, and
/// the inflated notional redeemed at maturity.
/// </summary>
/// <remarks>
/// <para>
/// The cash flows are <see cref="CPILeg"/>'s, its notional flow the redemption, paying the whole inflated notional.
/// It is built through <see cref="Bond"/>'s construction contract, so its issue date isn't checked against the first
/// payment, as in QuantLib. <see cref="GrowthOnly"/> is QuantLib's inspector, false for every bond the constructor
/// builds; QuantLib's deprecated constructor setting it isn't ported.
/// </para>
/// <para>
/// The bond holds QuantLib's default CPI-coupon pricer on its coupons; <see cref="InflationCouponPricerFunctions"/>
/// sets another on a copy.
/// </para>
/// </remarks>
public sealed class CPIBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="CPIBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="baseCPI">The base CPI, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for one
    /// observed at the start of the schedule.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="cpiIndex">The zero-inflation index.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="schedule">The coupons' schedule.</param>
    /// <param name="coupons">The fixed rates the index ratio inflates; the last one repeats for any further
    /// coupons.</param>
    /// <param name="accrualDayCounter">The coupons' day counter.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="paymentCalendar">The calendar of the payment dates and of the settlement, or
    /// <see langword="null"/> for the schedule's.</param>
    /// <param name="exCouponPeriod">The ex-coupon period; a zero-length one, the default, means no ex-coupon
    /// dates.</param>
    /// <param name="exCouponCalendar">The calendar the ex-coupon period is counted on, or <see langword="null"/>,
    /// QuantLib's empty calendar.</param>
    /// <param name="exCouponConvention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/>, <paramref name="cpiIndex"/>,
    /// <paramref name="coupons"/> or <paramref name="accrualDayCounter"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The issue date is <c>default(Date)</c>, or a coupon refuses its
    /// base.</exception>
    /// <exception cref="NanoQLException">The schedule has no tenor or is empty; no fixed rate is given; a coupon with a
    /// cap or floor is asked for; or a date is computed on QuantLib's empty calendar where it can't give
    /// one.</exception>
    public CPIBond(
        int settlementDays,
        double faceAmount,
        double? baseCPI,
        Period observationLag,
        ZeroInflationIndex cpiIndex,
        CPIInterpolationType observationInterpolation,
        Schedule schedule,
        IEnumerable<double> coupons,
        DayCounter accrualDayCounter,
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing,
        Date? issueDate = null,
        Calendar? paymentCalendar = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false)
        : base(settlementDays, CalendarOf(schedule, paymentCalendar), issueDate)
    {
        ArgumentNullException.ThrowIfNull(coupons);
        ArgumentNullException.ThrowIfNull(accrualDayCounter);
        if (cpiIndex is null)
        {
            throw new ArgumentNullException(nameof(cpiIndex), "no index provided");
        }

        EnumArguments.Check(observationInterpolation, nameof(observationInterpolation));
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(exCouponConvention, nameof(exCouponConvention));
        Frequency = schedule.Tenor().Frequency();
        DayCounter = accrualDayCounter;
        GrowthOnly = false;
        BaseCPI = baseCPI;
        ObservationLag = observationLag;
        CpiIndex = cpiIndex;
        ObservationInterpolation = observationInterpolation;

        maturityDate = schedule.EndDate();

        cashflows = new CPILeg(schedule, cpiIndex, baseCPI, observationLag)
            .WithNotionals(faceAmount)
            .WithFixedRates(coupons)
            .WithPaymentDayCounter(accrualDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithPaymentCalendar(Calendar)
            .WithObservationInterpolation(observationInterpolation)
            .WithSubtractInflationNominal(GrowthOnly)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .ToLeg();

        CalculateNotionalsFromCashflows();

        redemptions = [cashflows[^1]];
    }

    /// <summary>Gets the schedule's frequency.</summary>
    public Frequency Frequency { get; }

    /// <summary>Gets the coupons' day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets a value indicating whether the redemption pays only the notional's growth: false, the constructor
    /// paying the whole inflated notional.</summary>
    public bool GrowthOnly { get; }

    /// <summary>Gets the base CPI, or <see langword="null"/> for one observed.</summary>
    public double? BaseCPI { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex CpiIndex { get; }

    /// <summary>Gets how the index is observed.</summary>
    public CPIInterpolationType ObservationInterpolation { get; }

    // The settlement and payment calendar: the given one, or the schedule's.
    private static Calendar? CalendarOf(Schedule schedule, Calendar? paymentCalendar)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return paymentCalendar ?? schedule.Calendar;
    }
}
