// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/amortizingcmsratebond.hpp ql/instruments/bonds/amortizingcmsratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// An amortizing CMS-rate bond: CMS coupons on a schedule of notionals, possibly capped or floored, and a payment at
/// each notional step.
/// </summary>
/// <remarks>
/// Its coupons are built by <see cref="CmsLeg"/>, which gives them no pricer: the bond prices once one is set with
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, FloatingRateCouponPricer?)"/>. It has no single redemption:
/// each notional step but the last is an amortizing payment, the last a redemption, so <see cref="Bond.Redemption"/>
/// refuses it.
/// </remarks>
public sealed class AmortizingCmsRateBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="AmortizingCmsRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="notionals">
    /// The coupons' notionals; the last one repeats for any further coupons, and those beyond the coupons are ignored.
    /// </param>
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
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="redemptions">
    /// The payments at the notional steps, in base 100: step <c>i</c> from 1 takes element <c>i</c>, or the last one
    /// past the list's end, so element 0 is read only for a one-element list; <see langword="null"/> for QuantLib's
    /// default, 100.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="notionals"/>, <paramref name="schedule"/> or <paramref name="index"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> or <paramref name="fixingDays"/> is negative, or the convention isn't a member
    /// of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>, or there are more notionals, gearings, spreads, caps or floors than
    /// coupons.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty, no notional is given, or a date is computed on QuantLib's empty calendar where it can't
    /// give one.
    /// </exception>
    public AmortizingCmsRateBond(
        int settlementDays,
        IEnumerable<double> notionals,
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
        Date? issueDate = null,
        IEnumerable<double>? redemptions = null)
        : base(settlementDays, CalendarOf(schedule), issueDate)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        if (fixingDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(fixingDays));
        }

        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        ArgumentNullException.ThrowIfNull(index);

        maturityDate = schedule.EndDate();

        cashflows = new CmsLeg(schedule, index)
            .WithNotionals(notionals)
            .WithPaymentDayCounter(paymentDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithFixingDays(fixingDays)
            .WithGearings(gearings ?? [1.0])
            .WithSpreads(spreads ?? [0.0])
            .WithCaps(caps ?? [])
            .WithFloors(floors ?? [])
            .InArrears(inArrears)
            .ToLeg();

        AddRedemptionsToCashflows(redemptions ?? [100.0]);

        if (Cashflows.IsEmpty)
        {
            throw new NanoQLException("bond with no cashflows!");
        }
    }

    // The settlement calendar: the schedule's.
    private static Calendar? CalendarOf(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }
}
