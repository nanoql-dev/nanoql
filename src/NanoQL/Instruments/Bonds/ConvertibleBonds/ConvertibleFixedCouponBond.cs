// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/convertiblebonds.hpp ql/instruments/bonds/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A convertible fixed-coupon bond: fixed-rate coupons on a notional of 100 over the schedule, paid on the schedule's
/// convention, then its redemption.
/// </summary>
/// <remarks>
/// Most members inherited from <see cref="Bond"/>, such as the yield and the yield-based prices, refer to the
/// underlying plain-vanilla bond and don't take convertibility and callability into account, as QuantLib warns.
/// </remarks>
public sealed class ConvertibleFixedCouponBond : ConvertibleBond
{
    /// <summary>Initializes a new instance of the <see cref="ConvertibleFixedCouponBond"/> class.</summary>
    /// <param name="exercise">The conversion's exercise.</param>
    /// <param name="conversionRatio">The number of shares one bond converts into.</param>
    /// <param name="callability">The calls, soft calls and puts, copied, the last one the latest.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="coupons">The coupon rates; the last one repeats for any further coupons.</param>
    /// <param name="dayCounter">The coupons' day counter.</param>
    /// <param name="schedule">The schedule, whose calendar is the bond's and whose end date its maturity.</param>
    /// <param name="redemption">The redemption, per 100 of notional.</param>
    /// <param name="exCouponPeriod">The ex-coupon period; none by default.</param>
    /// <param name="exCouponCalendar">The ex-coupon calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="exCouponConvention">The ex-coupon dates' business-day convention.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <exception cref="ArgumentNullException">
    /// The exercise, the callabilities, the coupons, the day counter or the schedule is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="exCouponConvention"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>; a callability is <see langword="null"/>; or the last callability is after
    /// the maturity date.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty, no coupon rate is given, or the leg makes more than one redemption.
    /// </exception>
    /// <remarks>The notional is 100 whatever the redemption, as QuantLib forces it.</remarks>
    public ConvertibleFixedCouponBond(
        Exercise exercise,
        double conversionRatio,
        IEnumerable<Callability> callability,
        Date? issueDate,
        int settlementDays,
        IEnumerable<double> coupons,
        DayCounter dayCounter,
        Schedule schedule,
        double redemption = 100,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false)
        : base(exercise, conversionRatio, callability, issueDate, settlementDays, schedule, redemption)
    {
        ArgumentNullException.ThrowIfNull(coupons);
        ArgumentNullException.ThrowIfNull(dayCounter);

        // !!! notional forcibly set to 100
        cashflows = new FixedRateLeg(schedule)
            .WithNotionals(100.0)
            .WithCouponRates(coupons, dayCounter)
            .WithPaymentAdjustment(schedule.BusinessDayConvention)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .ToLeg();

        AddRedemptionsToCashflows([redemption]);

        if (redemptions.Length != 1)
        {
            throw new NanoQLException("multiple redemptions created");
        }
    }
}
