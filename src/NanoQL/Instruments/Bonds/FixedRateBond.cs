// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2004 M-Dimension Consulting Inc.
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/fixedratebond.hpp ql/instruments/bonds/fixedratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A fixed-rate bond: simple annually compounded coupon rates on a schedule, and a single redemption.</summary>
/// <remarks>
/// Unsealed, an extension point, as in QuantLib, where the Italian BTP derives from it. It is built through
/// <see cref="Bond"/>'s construction contract, so its issue date isn't checked against the first payment, as in
/// QuantLib.
/// </remarks>
public class FixedRateBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="FixedRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="schedule">The coupons' schedule.</param>
    /// <param name="coupons">The coupon rates, simple and annual; the last one repeats for any further coupons.</param>
    /// <param name="accrualDayCounter">The coupons' day counter.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="paymentCalendar">
    /// The calendar of the payment dates and of the settlement, or <see langword="null"/> for the schedule's.
    /// </param>
    /// <param name="exCouponPeriod">The ex-coupon period; a zero-length one, the default, means no ex-coupon dates.</param>
    /// <param name="exCouponCalendar">
    /// The calendar the ex-coupon period is counted on, or <see langword="null"/>, QuantLib's empty calendar.
    /// </param>
    /// <param name="exCouponConvention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <param name="firstPeriodDayCounter">
    /// The first coupon's day counter, or <see langword="null"/> for <paramref name="accrualDayCounter"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="schedule"/>, <paramref name="coupons"/> or <paramref name="accrualDayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or a convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">The issue date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty, or has one date; no coupon rate is given; a date is computed on QuantLib's empty calendar
    /// where it can't give one; or a NaN face amount makes several redemptions.
    /// </exception>
    public FixedRateBond(
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        IEnumerable<double> coupons,
        DayCounter accrualDayCounter,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        double redemption = 100.0,
        Date? issueDate = null,
        Calendar? paymentCalendar = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false,
        DayCounter? firstPeriodDayCounter = null)
        : base(settlementDays, CalendarOf(schedule, paymentCalendar), issueDate)
    {
        ArgumentNullException.ThrowIfNull(coupons);
        ArgumentNullException.ThrowIfNull(accrualDayCounter);
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(exCouponConvention, nameof(exCouponConvention));
        Frequency = schedule.HasTenor ? schedule.Tenor().Frequency() : Frequency.NoFrequency;
        DayCounter = accrualDayCounter;
        FirstPeriodDayCounter = firstPeriodDayCounter;

        maturityDate = schedule.EndDate();

        FixedRateLeg leg = new FixedRateLeg(schedule)
            .WithNotionals(faceAmount)
            .WithCouponRates(coupons, accrualDayCounter);

        // QuantLib passes its empty day counter, which the builder ignores
        if (firstPeriodDayCounter is not null)
        {
            leg = leg.WithFirstPeriodDayCounter(firstPeriodDayCounter);
        }

        cashflows = leg
            .WithPaymentCalendar(Calendar)
            .WithPaymentAdjustment(paymentConvention)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .ToLeg();

        AddRedemptionsToCashflows([redemption]);

        if (Cashflows.IsEmpty)
        {
            throw new NanoQLException("bond with no cashflows!");
        }

        if (redemptions.Length != 1)
        {
            throw new NanoQLException("multiple redemptions created");
        }
    }

    /// <summary>Gets the schedule's frequency, or <see cref="Frequency.NoFrequency"/> for a schedule with no tenor.</summary>
    public Frequency Frequency { get; }

    /// <summary>Gets the coupons' day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the first coupon's day counter, or <see langword="null"/> for the coupons' own.</summary>
    public DayCounter? FirstPeriodDayCounter { get; }

    // The settlement and payment calendar: the given one, or the schedule's.
    private static Calendar? CalendarOf(Schedule schedule, Calendar? paymentCalendar)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return paymentCalendar ?? schedule.Calendar;
    }
}
