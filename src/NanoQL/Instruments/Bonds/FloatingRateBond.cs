// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/floatingratebond.hpp ql/instruments/bonds/floatingratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A floating-rate bond, possibly capped or floored: Ibor coupons on a schedule, and a single redemption.</summary>
/// <remarks>
/// <para>
/// Unsealed, an extension point, as in QuantLib, where the Italian CCTEU derives from it. Its coupons are built by
/// <see cref="IborLeg"/> when the bond is, so the bond takes the context: a leg with no caps, floors or in-arrears
/// fixing gets QuantLib's default pricer, which reads the context's at-par switch then, and the bond keeps that choice.
/// A capped, floored or in-arrears bond gets no pricer, and prices only once one is set with
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, FloatingRateCouponPricer?)"/>.
/// </para>
/// <para>
/// It is built through <see cref="Bond"/>'s construction contract, so its issue date isn't checked against the first
/// payment, as in QuantLib. Its schedule's calendar is its settlement and payment calendar.
/// </para>
/// </remarks>
public class FloatingRateBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="FloatingRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="schedule">The coupons' schedule, whose calendar is the bond's.</param>
    /// <param name="iborIndex">The coupons' index.</param>
    /// <param name="accrualDayCounter">The coupons' day counter, or <see langword="null"/> for the index's.</param>
    /// <param name="context">The evaluation context, whose at-par switch the coupons' default pricer reads.</param>
    /// <param name="paymentConvention">The business-day convention of the payment dates.</param>
    /// <param name="fixingDays">The fixing days, or <see langword="null"/> for the index's.</param>
    /// <param name="gearings">The gearings; <see langword="null"/> for QuantLib's default, 1.</param>
    /// <param name="spreads">The spreads; <see langword="null"/> for QuantLib's default, 0.</param>
    /// <param name="caps">The caps, each <see langword="null"/> for none; <see langword="null"/> for none at all.</param>
    /// <param name="floors">The floors, each <see langword="null"/> for none; <see langword="null"/> for none at all.</param>
    /// <param name="inArrears">Whether the coupons fix in arrears.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="exCouponPeriod">The ex-coupon period; a zero-length one, the default, means no ex-coupon dates.</param>
    /// <param name="exCouponCalendar">
    /// The calendar the ex-coupon period is counted on, or <see langword="null"/>, QuantLib's empty calendar.
    /// </param>
    /// <param name="exCouponConvention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <param name="fixingConvention">The business-day convention adjusting the fixing dates.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="schedule"/> or <paramref name="iborIndex"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> or <paramref name="fixingDays"/> is negative, or a convention isn't a member
    /// of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>, or there are more gearings, spreads, caps or floors than coupons.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty, or a date is computed on QuantLib's empty calendar where it can't give one.
    /// </exception>
    public FloatingRateBond(
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        IborIndex iborIndex,
        DayCounter? accrualDayCounter,
        EvaluationContext context,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int? fixingDays = null,
        IEnumerable<double>? gearings = null,
        IEnumerable<double>? spreads = null,
        IEnumerable<double?>? caps = null,
        IEnumerable<double?>? floors = null,
        bool inArrears = false,
        double redemption = 100.0,
        Date? issueDate = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding)
        : base(settlementDays, CalendarOf(schedule, context), issueDate)
    {
        if (fixingDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(fixingDays));
        }

        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(exCouponConvention, nameof(exCouponConvention));
        EnumArguments.Check(fixingConvention, nameof(fixingConvention));

        maturityDate = schedule.EndDate();

        // QuantLib's leg builder refuses a null index under its own parameter's name
        if (iborIndex is null)
        {
            throw new ArgumentNullException(nameof(iborIndex), "no index provided");
        }

        cashflows = new IborLeg(schedule, iborIndex)
            .WithNotionals(faceAmount)
            .WithPaymentDayCounter(accrualDayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .WithFixingDays(fixingDays)
            .WithGearings(gearings ?? [1.0])
            .WithSpreads(spreads ?? [0.0])
            .WithCaps(caps ?? [])
            .WithFloors(floors ?? [])
            .InArrears(inArrears)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .WithFixingConvention(fixingConvention)
            .ToLeg(context);

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

    // The settlement calendar, the schedule's, after the context is checked, as every member taking it checks it first.
    private static Calendar? CalendarOf(Schedule schedule, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }
}
