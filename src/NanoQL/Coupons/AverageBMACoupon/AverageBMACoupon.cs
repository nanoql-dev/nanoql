// SPDX-FileCopyrightText: Copyright (C) 2007 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/averagebmacoupon.hpp ql/cashflows/averagebmacoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A coupon paying a BMA index, whose rate is a weighted average of the relevant fixings: each fixing weighs the
/// actual calendar days for which it is valid within the interest period. Before the weights are computed, the fixing
/// schedule is adjusted for the index's fixing-day gap.
/// </summary>
/// <remarks>
/// The coupon has no single fixing: <see cref="FixingDate"/>, <see cref="IndexFixing"/> and
/// <see cref="ConvexityAdjustment"/> throw QuantLib's texts, and <see cref="FixingDates"/> and
/// <see cref="IndexFixings"/> give the averaged dates and fixings. Its pricer is set at construction, as QuantLib
/// sets it; it gives the swaplet rate only, and refuses the prices and the caplet and floorlet rates.
/// </remarks>
public sealed class AverageBMACoupon : FloatingRateCoupon
{
    // QuantLib's file-scope bmaCutoffDays, "to be verified", which nothing changes.
    private const int BmaCutoffDays = 0;

    private readonly Schedule fixingSchedule;

    /// <summary>Initializes a new instance of the <see cref="AverageBMACoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="index">The BMA index.</param>
    /// <param name="gearing">The gearing applied to the averaged fixing.</param>
    /// <param name="spread">The spread added to the geared average.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>, QuantLib's empty
    /// day counter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the gearing is zero.</exception>
    /// <exception cref="NanoQLException">The fixing schedule can't be built.</exception>
    public AverageBMACoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        BMAIndex index,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null)
        : base(
            paymentDate,
            nominal,
            startDate,
            endDate,
            CheckIndex(index).FixingDays,
            index,
            gearing,
            spread,
            refPeriodStart,
            refPeriodEnd,
            dayCounter,
            false,
            pricer: AverageBMACouponPricer.Instance)
    {
        Calendar cal = index.FixingCalendar;
        int fixingDays = index.FixingDays;
        fixingDays += BmaCutoffDays;
        Date fixingStart = cal.Advance(startDate, -fixingDays, TimeUnit.Days, BusinessDayConvention.Preceding);

        // make sure that the value date associated to fixingStart is <= startDate
        fixingStart = AdjustToPreviousValidFixingDate(fixingStart, index);
        while (index.ValueDate(fixingStart) > startDate && fixingStart > Date.MinDate)
        {
            fixingStart = AdjustToPreviousValidFixingDate(fixingStart - 1, index);
        }

        fixingSchedule = index.FixingSchedule(fixingStart, endDate);
    }

    /// <summary>Not applicable here: use <see cref="FixingDates"/> instead.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always: the coupon has no single fixing date.</exception>
    public override Date FixingDate() => throw new NanoQLException("no single fixing date for average-BMA coupon");

    /// <summary>The fixing dates of the rates to be averaged.</summary>
    /// <returns>The fixing schedule's dates.</returns>
    public ImmutableArray<Date> FixingDates() => fixingSchedule.Dates;

    /// <summary>Not applicable here: use <see cref="IndexFixings"/> instead.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">Always: the coupon has no single fixing.</exception>
    public override double IndexFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        throw new NanoQLException("no single fixing for average-BMA coupon");
    }

    /// <summary>The fixings of the underlying index to be averaged, one per fixing date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A fixing that must be stored is missing, or can't be forecast.</exception>
    public ImmutableArray<double> IndexFixings(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var fixings = ImmutableArray.CreateBuilder<double>(fixingSchedule.Size);
        for (int i = 0; i < fixingSchedule.Size; ++i)
        {
            fixings.Add(Index.Fixing(fixingSchedule.Date(i), context));
        }

        return fixings.MoveToImmutable();
    }

    /// <summary>Not applicable here.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">Always: the adjustment isn't defined for the coupon.</exception>
    public override double ConvexityAdjustment(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        throw new NanoQLException("not defined for average-BMA coupon");
    }

    // QuantLib reads the index's fixing days before its base checks the index, which is undefined for a null index:
    // the refusal takes the base's text.
    private static BMAIndex CheckIndex(BMAIndex index) =>
        index ?? throw new ArgumentNullException(nameof(index), "no index provided");

    private static Date AdjustToPreviousValidFixingDate(Date d, BMAIndex index)
    {
        while (!index.IsValidFixingDate(d) && d > Date.MinDate)
        {
            d--;
        }

        return d;
    }
}
