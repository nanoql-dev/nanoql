// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/amortizingfloatingratebond.hpp ql/instruments/bonds/amortizingfloatingratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// An amortizing floating-rate bond: Ibor coupons on a schedule of notionals, possibly capped or floored, and a payment
/// at each notional step.
/// </summary>
/// <remarks>
/// Its coupons are built by <see cref="IborLeg"/> when the bond is, so the bond takes the context: a leg with no caps,
/// floors or in-arrears fixing gets QuantLib's default pricer, which reads the context's at-par switch then; a capped,
/// floored or in-arrears bond prices only once a pricer is set with
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, FloatingRateCouponPricer?)"/>. It has no single redemption:
/// each notional step but the last is an amortizing payment, the last a redemption, so <see cref="Bond.Redemption"/>
/// refuses it.
/// </remarks>
public sealed class AmortizingFloatingRateBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="AmortizingFloatingRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="notionals">
    /// The coupons' notionals; the last one repeats for any further coupons, and those beyond the coupons are ignored.
    /// </param>
    /// <param name="schedule">The coupons' schedule, whose calendar is the bond's.</param>
    /// <param name="index">The coupons' index.</param>
    /// <param name="accrualDayCounter">The coupons' day counter, or <see langword="null"/> for the index's.</param>
    /// <param name="context">The evaluation context, whose at-par switch the coupons' default pricer reads.</param>
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
    /// <param name="exCouponPeriod">The ex-coupon period; a zero-length one, the default, means no ex-coupon
    /// dates.</param>
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
    /// <paramref name="notionals"/>, <paramref name="schedule"/>, <paramref name="index"/> or
    /// <paramref name="context"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> or <paramref name="fixingDays"/> is negative, or a convention isn't a member
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
    public AmortizingFloatingRateBond(
        int settlementDays,
        IEnumerable<double> notionals,
        Schedule schedule,
        IborIndex index,
        DayCounter? accrualDayCounter,
        EvaluationContext context,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int? fixingDays = null,
        IEnumerable<double>? gearings = null,
        IEnumerable<double>? spreads = null,
        IEnumerable<double?>? caps = null,
        IEnumerable<double?>? floors = null,
        bool inArrears = false,
        Date? issueDate = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false,
        IEnumerable<double>? redemptions = null,
        int paymentLag = 0)
        : base(settlementDays, CalendarOf(schedule, context), issueDate)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        if (fixingDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(fixingDays));
        }

        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(exCouponConvention, nameof(exCouponConvention));

        maturityDate = schedule.EndDate();

        // QuantLib's leg builder refuses a null index under its own parameter's name
        if (index is null)
        {
            throw new ArgumentNullException(nameof(index), "no index provided");
        }

        cashflows = new IborLeg(schedule, index)
            .WithNotionals(notionals)
            .WithPaymentDayCounter(accrualDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithFixingDays(fixingDays)
            .WithPaymentLag(paymentLag)
            .WithGearings(gearings ?? [1.0])
            .WithSpreads(spreads ?? [0.0])
            .WithCaps(caps ?? [])
            .WithFloors(floors ?? [])
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .InArrears(inArrears)
            .ToLeg(context);

        AddRedemptionsToCashflows(redemptions ?? [100.0]);

        if (Cashflows.IsEmpty)
        {
            throw new NanoQLException("bond with no cashflows!");
        }
    }

    // The settlement calendar, the schedule's, after the context is checked, as every member taking it checks it first.
    private static Calendar? CalendarOf(Schedule schedule, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }
}
