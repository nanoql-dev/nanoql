// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2017 BN Algorithms Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebond.hpp ql/experimental/callablebonds/callablebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A callable or puttable fixed-rate bond: simple annually compounded coupon rates on a schedule, and a single
/// redemption.
/// </summary>
/// <remarks>
/// It is built through <see cref="Bond"/>'s construction contract, on the schedule's calendar, so its issue date isn't
/// checked against the first payment, as in QuantLib.
/// </remarks>
[Experimental("NQL0001")]
public sealed class CallableFixedRateBond : CallableBond
{
    /// <summary>Initializes a new instance of the <see cref="CallableFixedRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="schedule">The coupons' schedule, whose last date is the maturity date and whose calendar settles.</param>
    /// <param name="coupons">The coupon rates, simple and annual; the last one repeats for any further coupons.</param>
    /// <param name="accrualDayCounter">The coupons' day counter, and the payments'.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="putCallSchedule">The calls and puts, copied; none by default.</param>
    /// <param name="exCouponPeriod">The ex-coupon period; a zero-length one, the default, means no ex-coupon dates.</param>
    /// <param name="exCouponCalendar">
    /// The calendar the ex-coupon period is counted on, or <see langword="null"/>, QuantLib's empty calendar.
    /// </param>
    /// <param name="exCouponConvention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="schedule"/>, <paramref name="coupons"/> or <paramref name="accrualDayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or a convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>; a callability is <see langword="null"/>, or after the maturity date.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty (QuantLib reads its last date unchecked), or has one date; no coupon rate is given; or a
    /// date is computed on QuantLib's empty calendar where it can't give one.
    /// </exception>
    public CallableFixedRateBond(
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        IEnumerable<double> coupons,
        DayCounter accrualDayCounter,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        double redemption = 100.0,
        Date? issueDate = null,
        IEnumerable<Callability>? putCallSchedule = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false)
        : base(
            settlementDays,
            EndDateOf(schedule),
            schedule.Calendar,
            accrualDayCounter,
            faceAmount,
            FrequencyOf(schedule),
            issueDate,
            putCallSchedule)
    {
        ArgumentNullException.ThrowIfNull(coupons);
        ArgumentNullException.ThrowIfNull(accrualDayCounter);
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(exCouponConvention, nameof(exCouponConvention));

        cashflows = new FixedRateLeg(schedule)
            .WithNotionals(faceAmount)
            .WithCouponRates(coupons, accrualDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .ToLeg();

        AddRedemptionsToCashflows([redemption]);
    }

    // QuantLib passes schedule.dates().back(), which an empty schedule leaves undefined: its end date refuses it.
    private static Date EndDateOf(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.EndDate();
    }

    private static Frequency FrequencyOf(Schedule schedule) =>
        schedule.HasTenor ? schedule.Tenor().Frequency() : Frequency.NoFrequency;
}
