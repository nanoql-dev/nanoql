// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/cmsratebond.hpp ql/instruments/bonds/cmsratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A CMS-rate bond, possibly capped or floored: CMS coupons on a schedule, and a single redemption.</summary>
/// <remarks>
/// Its coupons are built by <see cref="CmsLeg"/>, which gives them no pricer: the bond prices once one is set with
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, FloatingRateCouponPricer?)"/>, as QuantLib's
/// <c>setCouponPricer</c> on its cash flows. It is built through <see cref="Bond"/>'s construction contract, so its
/// issue date isn't checked against the first payment, as in QuantLib. Its schedule's calendar is its settlement and
/// payment calendar.
/// </remarks>
public sealed class CmsRateBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="CmsRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="schedule">The coupons' schedule, whose calendar is the bond's.</param>
    /// <param name="index">The coupons' swap index.</param>
    /// <param name="paymentDayCounter">The coupons' day counter.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="fixingDays">The fixing days, or <see langword="null"/> for the index's.</param>
    /// <param name="gearings">The gearings; <see langword="null"/> for QuantLib's default, 1.</param>
    /// <param name="spreads">The spreads; <see langword="null"/> for QuantLib's default, 0.</param>
    /// <param name="caps">The caps, each <see langword="null"/> for none; <see langword="null"/> for none at
    /// all.</param>
    /// <param name="floors">The floors, each <see langword="null"/> for none; <see langword="null"/> for none at
    /// all.</param>
    /// <param name="inArrears">Whether the coupons fix in arrears.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or <paramref name="index"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> or <paramref name="fixingDays"/> is negative, or the convention isn't a member
    /// of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>, or there are more gearings, spreads, caps or floors than coupons.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty, or a date is computed on QuantLib's empty calendar where it can't give one.
    /// </exception>
    public CmsRateBond(
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        SwapIndex index,
        DayCounter? paymentDayCounter,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int? fixingDays = null,
        IEnumerable<double>? gearings = null,
        IEnumerable<double>? spreads = null,
        IEnumerable<double?>? caps = null,
        IEnumerable<double?>? floors = null,
        bool inArrears = false,
        double redemption = 100.0,
        Date? issueDate = null)
        : base(settlementDays, CalendarOf(schedule), issueDate)
    {
        if (fixingDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(fixingDays));
        }

        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        ArgumentNullException.ThrowIfNull(index);

        maturityDate = schedule.EndDate();

        cashflows = new CmsLeg(schedule, index)
            .WithNotionals(faceAmount)
            .WithPaymentDayCounter(paymentDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithFixingDays(fixingDays)
            .WithGearings(gearings ?? [1.0])
            .WithSpreads(spreads ?? [0.0])
            .WithCaps(caps ?? [])
            .WithFloors(floors ?? [])
            .InArrears(inArrears)
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

    // The settlement calendar: the schedule's.
    private static Calendar? CalendarOf(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }
}
