// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/multipleresetscoupon.hpp ql/cashflows/multipleresetscoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A coupon paying a rate compounded or averaged from several fixings over its accrual period, one per period of its
/// reset schedule, each fixing its fixing days before its period's start.
/// </summary>
/// <remarks>
/// The reset schedule's first and last dates are the coupon's accrual start and end. Its rate is its pricer's: the
/// compounding pricer's or the averaging pricer's. The coupon spread is added to the final rate, the rate spread to
/// each fixing.
/// </remarks>
public sealed class MultipleResetsCoupon : FloatingRateCoupon
{
    private readonly ImmutableArray<Date> valueDates;
    private readonly ImmutableArray<Date> fixingDates;
    private readonly ImmutableArray<double> dt;

    /// <summary>Initializes a new instance of the <see cref="MultipleResetsCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="resetSchedule">The schedule of the resets: each period the underlying period of one fixing; its
    /// first and last dates the coupon's accrual start and end.</param>
    /// <param name="fixingDays">The fixing days before each period's start.</param>
    /// <param name="index">The index whose fixings are compounded or averaged.</param>
    /// <param name="gearing">The multiplier of the final rate.</param>
    /// <param name="couponSpread">The spread added to the final rate.</param>
    /// <param name="rateSpread">The spread added to each fixing.</param>
    /// <param name="refPeriodStart">The reference period's start, the accrual start by default.</param>
    /// <param name="refPeriodEnd">The reference period's end, the accrual end by default.</param>
    /// <param name="dayCounter">The day counter, the index's by default.</param>
    /// <param name="exCouponDate">The ex-coupon date, or <see langword="null"/> for none.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none; an argument QuantLib's constructor doesn't
    /// have, since its <c>setPricer</c> changes the coupon in place.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resetSchedule"/> or <paramref name="index"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">The gearing is 0, with QuantLib's "Null gearing not allowed".</exception>
    /// <exception cref="NanoQLException">The schedule is empty, with QuantLib's text.</exception>
    public MultipleResetsCoupon(
        Date paymentDate,
        double nominal,
        Schedule resetSchedule,
        int fixingDays,
        IborIndex index,
        double gearing = 1.0,
        double couponSpread = 0.0,
        double rateSpread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        Date? exCouponDate = null,
        FloatingRateCouponPricer? pricer = null)
        : base(
            paymentDate,
            nominal,
            NotNull(resetSchedule).Front(),
            resetSchedule.Back(),
            fixingDays,
            index,
            gearing,
            couponSpread,
            refPeriodStart,
            refPeriodEnd,
            dayCounter,
            false,
            exCouponDate,
            pricer: pricer)
    {
        RateSpread = rateSpread;
        valueDates = resetSchedule.Dates;

        // fixing dates
        int n = valueDates.Length - 1;
        if (FixingDays == 0)
        {
            fixingDates = valueDates[..^1];
        }
        else
        {
            var fixings = ImmutableArray.CreateBuilder<Date>(n);
            for (int i = 0; i < n; ++i)
            {
                fixings.Add(index.FixingCalendar.Advance(valueDates[i], -FixingDays, TimeUnit.Days));
            }

            fixingDates = fixings.MoveToImmutable();
        }

        // accrual times of sub-periods
        var times = ImmutableArray.CreateBuilder<double>(n);
        DayCounter dc = index.DayCounter;
        for (int i = 0; i < n; ++i)
        {
            times.Add(dc.YearFraction(valueDates[i], valueDates[i + 1]));
        }

        dt = times.MoveToImmutable();
    }

    /// <summary>Gets the fixing dates of the rates compounded or averaged.</summary>
    public ImmutableArray<Date> FixingDates => fixingDates;

    /// <summary>Gets the accrual times of the reset periods.</summary>
    public ImmutableArray<double> Dt => dt;

    /// <summary>Gets the value dates of the rates compounded or averaged, the reset schedule's dates.</summary>
    public ImmutableArray<Date> ValueDates => valueDates;

    /// <summary>Gets the spread added to each fixing.</summary>
    public double RateSpread { get; }

    /// <summary>The date the coupon is fully determined: its last fixing date.</summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The reset schedule has a single date, so no fixing, where QuantLib reads past
    /// an empty vector.</exception>
    public override Date FixingDate() =>
        !fixingDates.IsEmpty ? fixingDates[^1] : throw new NanoQLException("no fixing dates: the reset schedule has a single date");

    private static Schedule NotNull(Schedule resetSchedule) =>
        resetSchedule ?? throw new ArgumentNullException(nameof(resetSchedule));
}
