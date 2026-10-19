// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/amortizingfixedratebond.hpp ql/instruments/bonds/amortizingfixedratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// An amortizing fixed-rate bond: fixed-rate coupons on a schedule of notionals, and a payment at each notional step.
/// </summary>
/// <remarks>
/// It reads its schedule's tenor first, so a schedule built from dates alone is refused, where
/// <see cref="FixedRateBond"/> takes no frequency for one, as in QuantLib. It has no single redemption: each notional
/// step but the last is an amortizing payment, the last a redemption, so <see cref="Bond.Redemption"/> refuses it.
/// <see cref="AmortizingFixedRateBondFunctions"/> builds a French amortization's schedule and notionals.
/// </remarks>
public sealed class AmortizingFixedRateBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="AmortizingFixedRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="notionals">
    /// The coupons' notionals; the last one repeats for any further coupons, and those beyond the coupons are ignored.
    /// </param>
    /// <param name="schedule">The coupons' schedule, whose calendar is the bond's.</param>
    /// <param name="coupons">The coupon rates, simple and annual; the last one repeats for any further coupons.</param>
    /// <param name="accrualDayCounter">The coupons' day counter.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="exCouponPeriod">The ex-coupon period; a zero-length one, the default, means no ex-coupon dates.</param>
    /// <param name="exCouponCalendar">
    /// The calendar the ex-coupon period is counted on, or <see langword="null"/>, QuantLib's empty calendar.
    /// </param>
    /// <param name="exCouponConvention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <param name="redemptions">
    /// The payments at the notional steps, in base 100: step <c>i</c> from 1 takes element <c>i</c>, or the last one
    /// past the list's end, so element 0 is read only for a one-element list; <see langword="null"/> for QuantLib's
    /// default, 100.
    /// </param>
    /// <param name="paymentLag">The business days from each accrual end to its payment date.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="notionals"/>, <paramref name="schedule"/>, <paramref name="coupons"/> or
    /// <paramref name="accrualDayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or a convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">The issue date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">
    /// The schedule has no tenor, or is empty, or has one date; no notional or coupon rate is given; or a date is
    /// computed on QuantLib's empty calendar where it can't give one.
    /// </exception>
    public AmortizingFixedRateBond(
        int settlementDays,
        IEnumerable<double> notionals,
        Schedule schedule,
        IEnumerable<double> coupons,
        DayCounter accrualDayCounter,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        Date? issueDate = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false,
        IEnumerable<double>? redemptions = null,
        int paymentLag = 0)
        : base(settlementDays, CalendarOf(schedule), issueDate)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        ArgumentNullException.ThrowIfNull(coupons);
        ArgumentNullException.ThrowIfNull(accrualDayCounter);
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(exCouponConvention, nameof(exCouponConvention));
        Frequency = schedule.Tenor().Frequency();
        DayCounter = accrualDayCounter;

        maturityDate = schedule.EndDate();

        cashflows = new FixedRateLeg(schedule)
            .WithNotionals(notionals)
            .WithCouponRates(coupons, accrualDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .WithPaymentLag(paymentLag)
            .ToLeg();

        AddRedemptionsToCashflows(redemptions ?? [100.0]);

        if (Cashflows.IsEmpty)
        {
            throw new NanoQLException("bond with no cashflows!");
        }
    }

    /// <summary>Gets the schedule's frequency.</summary>
    public Frequency Frequency { get; }

    /// <summary>Gets the coupons' day counter.</summary>
    public DayCounter DayCounter { get; }

    // The settlement calendar: the schedule's.
    private static Calendar? CalendarOf(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }
}
