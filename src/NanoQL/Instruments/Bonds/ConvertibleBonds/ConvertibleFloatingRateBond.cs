// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/convertiblebonds.hpp ql/instruments/bonds/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A convertible floating-rate bond: Ibor coupons on a notional of 100 over the schedule, paid on the schedule's
/// convention, then its redemption.
/// </summary>
/// <remarks>
/// <para>
/// Most members inherited from <see cref="Bond"/>, such as the yield and the yield-based prices, refer to the
/// underlying plain-vanilla bond and don't take convertibility and callability into account, as QuantLib warns.
/// </para>
/// <para>
/// The coupons' amounts read the context the engine prices under, as every Ibor coupon's do; the convertible engine
/// reads them as known amounts at its coupon times.
/// </para>
/// </remarks>
public sealed class ConvertibleFloatingRateBond : ConvertibleBond
{
    /// <summary>Initializes a new instance of the <see cref="ConvertibleFloatingRateBond"/> class.</summary>
    /// <param name="exercise">The conversion's exercise.</param>
    /// <param name="conversionRatio">The number of shares one bond converts into.</param>
    /// <param name="callability">The calls, soft calls and puts, copied, the last one the latest.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="index">The coupons' index.</param>
    /// <param name="fixingDays">The coupons' fixing days.</param>
    /// <param name="spreads">The coupons' spreads; the last one repeats for any further coupons.</param>
    /// <param name="dayCounter">The coupons' payment day counter.</param>
    /// <param name="schedule">The schedule, whose calendar is the bond's and whose end date its maturity.</param>
    /// <param name="context">The evaluation context, whose at-par switch the default pricer reads.</param>
    /// <param name="redemption">The redemption, per 100 of notional.</param>
    /// <param name="exCouponPeriod">The ex-coupon period; none by default.</param>
    /// <param name="exCouponCalendar">The ex-coupon calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="exCouponConvention">The ex-coupon dates' business-day convention.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <exception cref="ArgumentNullException">
    /// The exercise, the callabilities, the index, the spreads, the schedule or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> or <paramref name="fixingDays"/> is negative, or
    /// <paramref name="exCouponConvention"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>; a callability is <see langword="null"/>; or the last callability is after
    /// the maturity date.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The schedule is empty, the leg can't be built, or it makes more than one redemption.
    /// </exception>
    /// <remarks>The notional is 100 whatever the redemption, as QuantLib forces it.</remarks>
    public ConvertibleFloatingRateBond(
        Exercise exercise,
        double conversionRatio,
        IEnumerable<Callability> callability,
        Date? issueDate,
        int settlementDays,
        IborIndex index,
        int fixingDays,
        IEnumerable<double> spreads,
        DayCounter? dayCounter,
        Schedule schedule,
        EvaluationContext context,
        double redemption = 100,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponConvention = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false)
        : base(exercise, conversionRatio, callability, issueDate, settlementDays, schedule, redemption)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        ArgumentNullException.ThrowIfNull(spreads);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's leg builder refuses a null index under its own parameter's name
        if (index is null)
        {
            throw new ArgumentNullException(nameof(index), "no index provided");
        }

        // !!! notional forcibly set to 100
        cashflows = new IborLeg(schedule, index)
            .WithPaymentDayCounter(dayCounter)
            .WithNotionals(100.0)
            .WithPaymentAdjustment(schedule.BusinessDayConvention)
            .WithFixingDays(fixingDays)
            .WithSpreads(spreads)
            .WithExCouponPeriod(exCouponPeriod, exCouponCalendar, exCouponConvention, exCouponEndOfMonth)
            .ToLeg(context);

        AddRedemptionsToCashflows([redemption]);

        if (redemptions.Length != 1)
        {
            throw new NanoQLException("multiple redemptions created");
        }
    }
}
