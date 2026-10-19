// SPDX-FileCopyrightText: Copyright (C) 2005 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bondhelpers.hpp ql/termstructures/yield/bondhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over the prices of fixed-rate bonds: a <see cref="BondHelper"/> on the
/// <see cref="FixedRateBond"/> its arguments describe.
/// </summary>
/// <remarks>The bond takes no first-period day counter, as QuantLib's helper passes none.</remarks>
public sealed class FixedRateBondHelper : BondHelper
{
    /// <summary>Initializes a new instance of the <see cref="FixedRateBondHelper"/> class.</summary>
    /// <param name="price">The bond's quoted price, per 100 of notional.</param>
    /// <param name="settlementDays">The bond's settlement days.</param>
    /// <param name="faceAmount">The bond's notional.</param>
    /// <param name="schedule">The coupons' schedule.</param>
    /// <param name="coupons">The coupon rates, simple and annual.</param>
    /// <param name="dayCounter">The coupons' day counter.</param>
    /// <param name="context">The evaluation context, which gives the earliest date.</param>
    /// <param name="paymentConv">The business-day convention of the payment dates.</param>
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
    /// <param name="priceType">Whether the price is clean or dirty.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="schedule"/>, <paramref name="coupons"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">The issue date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The bond can't be built.</exception>
    public FixedRateBondHelper(
        double price,
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        IEnumerable<double> coupons,
        DayCounter dayCounter,
        EvaluationContext context,
        BusinessDayConvention paymentConv = BusinessDayConvention.Following,
        double redemption = 100.0,
        Date? issueDate = null,
        Calendar? paymentCalendar = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false,
        BondPriceType priceType = BondPriceType.Clean)
        : base(
            price,
            BondOf(
                context,
                settlementDays,
                faceAmount,
                schedule,
                coupons,
                dayCounter,
                paymentConv,
                redemption,
                issueDate,
                paymentCalendar,
                exCouponPeriod,
                exCouponCalendar,
                exCouponConvention,
                exCouponEndOfMonth),
            context,
            priceType)
    {
    }

    // The bond, built after the context is checked, as every member taking it checks it first.
    private static FixedRateBond BondOf(
        EvaluationContext context,
        int settlementDays,
        double faceAmount,
        Schedule schedule,
        IEnumerable<double> coupons,
        DayCounter dayCounter,
        BusinessDayConvention paymentConvention,
        double redemption,
        Date? issueDate,
        Calendar? paymentCalendar,
        Period exCouponPeriod,
        Calendar? exCouponCalendar,
        BusinessDayConvention exCouponConvention,
        bool exCouponEndOfMonth)
    {
        ArgumentNullException.ThrowIfNull(context);

        // the bond names it paymentConvention
        EnumArguments.Check(paymentConvention, "paymentConv");
        return new FixedRateBond(
            settlementDays,
            faceAmount,
            schedule,
            coupons,
            dayCounter,
            paymentConvention,
            redemption,
            issueDate,
            paymentCalendar,
            exCouponPeriod,
            exCouponCalendar,
            exCouponConvention,
            exCouponEndOfMonth);
    }
}
