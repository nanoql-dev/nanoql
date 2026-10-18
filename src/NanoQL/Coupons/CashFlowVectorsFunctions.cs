// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cashflowvectors.hpp ql/cashflows/cashflowvectors.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: FloatingDigitalLeg, which waits for B3.4 with the digital coupons

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using static NanoQL.Utilities.VectorsFunctions;

namespace NanoQL.Coupons;

/// <summary>Cash-flow vector builders: QuantLib's free functions of <c>cashflowvectors.hpp</c>.</summary>
public static class CashFlowVectorsFunctions
{
    /// <summary>
    /// A leg of floating-rate coupons, one per schedule period: QuantLib's <c>FloatingLeg</c> template, with its two coupon
    /// types given as the factories that build them. A period whose gearing is zero is a fixed-rate coupon paying the
    /// spread, floored and capped; a period with a cap or a floor is built by <paramref name="cappedFlooredCoupon"/>, any
    /// other by <paramref name="floatingCoupon"/>.
    /// </summary>
    /// <remarks>
    /// A per-period vector (nominals, fixing days, gearings, spreads, caps, floors) gives each period its element, the
    /// last element past its end, and the default when empty: a nominal and a gearing of 1, the index's fixing days, a
    /// spread of 0, and no cap or floor (<see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>). Each factory
    /// receives QuantLib's constructor arguments: the payment date, nominal, accrual start and end, fixing days, index,
    /// gearing, spread (then the cap and the floor), reference period start and end, payment day counter, in-arrears
    /// flag, ex-coupon date and fixing convention.
    /// </remarks>
    /// <typeparam name="TIndex">The index type.</typeparam>
    /// <param name="floatingCoupon">The factory of a coupon with neither cap nor floor.</param>
    /// <param name="cappedFlooredCoupon">The factory of a capped or floored coupon.</param>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="nominals">The nominals.</param>
    /// <param name="index">The index.</param>
    /// <param name="paymentDayCounter">The accrual day counter, or <see langword="null"/>, QuantLib's empty day
    /// counter, which leaves each floating coupon the index's.</param>
    /// <param name="paymentAdj">The business-day convention of the payment dates.</param>
    /// <param name="fixingDays">The fixing days, null elements taking the index's.</param>
    /// <param name="gearings">The gearings.</param>
    /// <param name="spreads">The spreads.</param>
    /// <param name="caps">The caps, null elements for none.</param>
    /// <param name="floors">The floors, null elements for none.</param>
    /// <param name="isInArrears">Whether the coupons fix at the end of their periods.</param>
    /// <param name="isZero">Whether every coupon is paid on the last payment date.</param>
    /// <param name="paymentLag">The business days from each accrual end to its payment date.</param>
    /// <param name="paymentCalendar">The calendar of the payment dates; the schedule's when <see langword="null"/>.</param>
    /// <param name="exCouponPeriod">The ex-coupon period, counted back from each payment date; a zero-length period
    /// means no ex-coupon dates.</param>
    /// <param name="exCouponCalendar">The calendar the ex-coupon period is counted on; the schedule's when
    /// <see langword="null"/>.</param>
    /// <param name="exCouponAdjustment">The business-day convention of the ex-coupon dates.</param>
    /// <param name="exCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing dates.</param>
    /// <returns>The leg.</returns>
    /// <exception cref="ArgumentException">The schedule is empty; no nominal is given; a vector has more elements than
    /// the schedule has periods; the leg is both in arrears and zero; or a period has a zero gearing and no payment day
    /// counter is given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A fixing-days element is negative, or a convention isn't a member
    /// of its type.</exception>
    public static ImmutableArray<CashFlow> FloatingLeg<TIndex>(
        Func<Date, double, Date, Date, int?, TIndex, double, double, Date, Date, DayCounter?, bool, Date?,
            BusinessDayConvention, CashFlow> floatingCoupon,
        Func<Date, double, Date, Date, int?, TIndex, double, double, double?, double?, Date, Date, DayCounter?, bool,
            Date?, BusinessDayConvention, CashFlow> cappedFlooredCoupon,
        Schedule schedule,
        IEnumerable<double> nominals,
        TIndex index,
        DayCounter? paymentDayCounter,
        BusinessDayConvention paymentAdj,
        IEnumerable<int?> fixingDays,
        IEnumerable<double> gearings,
        IEnumerable<double> spreads,
        IEnumerable<double?> caps,
        IEnumerable<double?> floors,
        bool isInArrears,
        bool isZero,
        int paymentLag = 0,
        Calendar? paymentCalendar = null,
        Period exCouponPeriod = default,
        Calendar? exCouponCalendar = null,
        BusinessDayConvention exCouponAdjustment = BusinessDayConvention.Unadjusted,
        bool exCouponEndOfMonth = false,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding)
        where TIndex : InterestRateIndex
    {
        ArgumentNullException.ThrowIfNull(floatingCoupon);
        ArgumentNullException.ThrowIfNull(cappedFlooredCoupon);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(nominals);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(fixingDays);
        ArgumentNullException.ThrowIfNull(gearings);
        ArgumentNullException.ThrowIfNull(spreads);
        ArgumentNullException.ThrowIfNull(caps);
        ArgumentNullException.ThrowIfNull(floors);
        if (schedule.Empty)
        {
            // QuantLib's period count wraps below zero for an empty schedule.
            throw new ArgumentException("empty schedule", nameof(schedule));
        }

        ImmutableArray<int?> days = [.. fixingDays];
        foreach (int? d in days)
        {
            if (d is int count)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(fixingDays));
            }
        }

        EnumArguments.Check(paymentAdj, nameof(paymentAdj));
        EnumArguments.Check(exCouponAdjustment, nameof(exCouponAdjustment));
        EnumArguments.Check(fixingConvention, nameof(fixingConvention));
        return Build(
            floatingCoupon,
            cappedFlooredCoupon,
            schedule,
            [.. nominals],
            index,
            paymentDayCounter,
            paymentAdj,
            days,
            [.. gearings],
            [.. spreads],
            [.. caps],
            [.. floors],
            isInArrears,
            isZero,
            paymentLag,
            paymentCalendar,
            exCouponPeriod,
            exCouponCalendar,
            exCouponAdjustment,
            exCouponEndOfMonth,
            fixingConvention,
            stored: false);
    }

    /// <summary>
    /// The rate of a zero-gearing period's fixed coupon: the spread, floored and then capped, with QuantLib's
    /// <c>std::max</c> and <c>std::min</c>, which keep the floor or the cap when the spread is NaN.
    /// </summary>
    /// <param name="spreads">The spreads.</param>
    /// <param name="caps">The caps.</param>
    /// <param name="floors">The floors.</param>
    /// <param name="i">The period.</param>
    /// <returns>The rate.</returns>
    internal static double EffectiveFixedRate(
        ImmutableArray<double> spreads,
        ImmutableArray<double?> caps,
        ImmutableArray<double?> floors,
        int i)
    {
        double result = Get(spreads, i, 0.0);
        double? floor = Get(floors, i, null);
        if (floor is double f)
        {
            // std::max(floor, result)
            result = f < result ? result : f;
        }

        double? cap = Get(caps, i, null);
        if (cap is double c)
        {
            // std::min(cap, result)
            result = result < c ? result : c;
        }

        return result;
    }

    /// <summary>Whether a period has neither a cap nor a floor.</summary>
    /// <param name="caps">The caps.</param>
    /// <param name="floors">The floors.</param>
    /// <param name="i">The period.</param>
    /// <returns><see langword="true"/> when it has neither.</returns>
    internal static bool NoOption(ImmutableArray<double?> caps, ImmutableArray<double?> floors, int i) =>
        Get(caps, i, null) is null && Get(floors, i, null) is null;

    /// <summary>
    /// The leg, from checked arguments. The size checks refuse with <see cref="ArgumentException"/> for a caller's
    /// arguments, and with <see cref="NanoQLException"/> for a builder's stored settings.
    /// </summary>
    internal static ImmutableArray<CashFlow> Build<TIndex>(
        Func<Date, double, Date, Date, int?, TIndex, double, double, Date, Date, DayCounter?, bool, Date?,
            BusinessDayConvention, CashFlow> floatingCoupon,
        Func<Date, double, Date, Date, int?, TIndex, double, double, double?, double?, Date, Date, DayCounter?, bool,
            Date?, BusinessDayConvention, CashFlow> cappedFlooredCoupon,
        Schedule schedule,
        ImmutableArray<double> nominals,
        TIndex index,
        DayCounter? paymentDayCounter,
        BusinessDayConvention paymentAdj,
        ImmutableArray<int?> fixingDays,
        ImmutableArray<double> gearings,
        ImmutableArray<double> spreads,
        ImmutableArray<double?> caps,
        ImmutableArray<double?> floors,
        bool isInArrears,
        bool isZero,
        int paymentLag,
        Calendar? paymentCalendar,
        Period exCouponPeriod,
        Calendar? exCouponCalendar,
        BusinessDayConvention exCouponAdjustment,
        bool exCouponEndOfMonth,
        BusinessDayConvention fixingConvention,
        bool stored)
        where TIndex : InterestRateIndex
    {
        int n = schedule.Size - 1;
        if (nominals.IsEmpty)
        {
            throw Refused("no notional given", nameof(nominals), stored);
        }

        if (nominals.Length > n)
        {
            throw Refused($"too many nominals ({nominals.Length}), only {n} required", nameof(nominals), stored);
        }

        if (gearings.Length > n)
        {
            throw Refused($"too many gearings ({gearings.Length}), only {n} required", nameof(gearings), stored);
        }

        if (spreads.Length > n)
        {
            throw Refused($"too many spreads ({spreads.Length}), only {n} required", nameof(spreads), stored);
        }

        if (caps.Length > n)
        {
            throw Refused($"too many caps ({caps.Length}), only {n} required", nameof(caps), stored);
        }

        if (floors.Length > n)
        {
            throw Refused($"too many floors ({floors.Length}), only {n} required", nameof(floors), stored);
        }

        if (isZero && isInArrears)
        {
            throw Refused("in-arrears and zero features are not compatible", nameof(isZero), stored);
        }

        ImmutableArray<CashFlow>.Builder leg = ImmutableArray.CreateBuilder<CashFlow>(n);

        // the following is not always correct
        Calendar calendar = schedule.Calendar;

        paymentCalendar ??= calendar;

        Date? exCouponDate = null;
        Date lastPaymentDate = paymentCalendar.Advance(schedule.Date(n), paymentLag, TimeUnit.Days, paymentAdj);

        for (int i = 0; i < n; ++i)
        {
            Date start = schedule.Date(i), refStart = start;
            Date end = schedule.Date(i + 1), refEnd = end;
            Date paymentDate =
                isZero ? lastPaymentDate : paymentCalendar.Advance(end, paymentLag, TimeUnit.Days, paymentAdj);
            if (i == 0 && schedule.HasIsRegular && schedule.HasTenor && !schedule.IsRegular(i + 1))
            {
                BusinessDayConvention bdc = schedule.BusinessDayConvention;
                refStart = calendar.Adjust(end - schedule.Tenor(), bdc);
            }

            if (i == n - 1 && schedule.HasIsRegular && schedule.HasTenor && !schedule.IsRegular(i + 1))
            {
                BusinessDayConvention bdc = schedule.BusinessDayConvention;
                refEnd = calendar.Adjust(start + schedule.Tenor(), bdc);
            }

            if (exCouponPeriod != default)
            {
                exCouponCalendar ??= calendar;
                exCouponDate = exCouponCalendar.Advance(
                    paymentDate, -exCouponPeriod, exCouponAdjustment, exCouponEndOfMonth);
            }

            if (Get(gearings, i, 1.0) == 0.0)
            {
                // fixed coupon; QuantLib builds it on an empty day counter and fails only when it is priced, where
                // nanoql's FixedRateCoupon needs one, so the leg is refused here with QuantLib's text
                DayCounter dayCounter = paymentDayCounter
                    ?? throw Refused("no day counter implementation provided", nameof(paymentDayCounter), stored);
                leg.Add(new FixedRateCoupon(
                    paymentDate,
                    Get(nominals, i, 1.0),
                    EffectiveFixedRate(spreads, caps, floors, i),
                    dayCounter,
                    start,
                    end,
                    refStart,
                    refEnd,
                    exCouponDate));
            }
            else
            {
                // floating coupon
                if (NoOption(caps, floors, i))
                {
                    leg.Add(floatingCoupon(
                        paymentDate,
                        Get(nominals, i, 1.0),
                        start,
                        end,
                        Get(fixingDays, i, (int?)index.FixingDays),
                        index,
                        Get(gearings, i, 1.0),
                        Get(spreads, i, 0.0),
                        refStart,
                        refEnd,
                        paymentDayCounter,
                        isInArrears,
                        exCouponDate,
                        fixingConvention));
                }
                else
                {
                    leg.Add(cappedFlooredCoupon(
                        paymentDate,
                        Get(nominals, i, 1.0),
                        start,
                        end,
                        Get(fixingDays, i, (int?)index.FixingDays),
                        index,
                        Get(gearings, i, 1.0),
                        Get(spreads, i, 0.0),
                        Get(caps, i, null),
                        Get(floors, i, null),
                        refStart,
                        refEnd,
                        paymentDayCounter,
                        isInArrears,
                        exCouponDate,
                        fixingConvention));
                }
            }
        }

        return leg.MoveToImmutable();
    }

    private static Exception Refused(string message, string paramName, bool stored) =>
        stored ? new NanoQLException(message) : new ArgumentException(message, paramName);
}
