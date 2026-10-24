// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mario Pucci
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/rangeaccrual.hpp ql/cashflows/rangeaccrual.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A range-accrual floating-rate coupon: the geared index rate plus the spread, accrued over the share of its
/// observation dates on which the index fixes between a lower and an upper trigger.
/// </summary>
/// <remarks>
/// The observation dates are the observation schedule's dates but its first and last, the coupon's start and end. The
/// start, end and observation times are measured on the coupon's day counter from the index's forwarding curve's
/// reference date, when the coupon is built, as QuantLib's. QuantLib's deprecated constructor taking a shared
/// schedule, and its <c>observationsSchedule</c> inspector, aren't ported.
/// </remarks>
public sealed class RangeAccrualFloatersCoupon : FloatingRateCoupon
{
    private readonly ImmutableArray<Date> observationDates;
    private readonly ImmutableArray<double> observationTimes;

    /// <summary>Initializes a new instance of the <see cref="RangeAccrualFloatersCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="index">The index observed and paid.</param>
    /// <param name="startDate">The accrual start date, S.</param>
    /// <param name="endDate">The accrual end date, T.</param>
    /// <param name="fixingDays">The fixing days before each observation.</param>
    /// <param name="dayCounter">The day counter, which also measures the coupon's times; QuantLib's empty day counter
    /// when <see langword="null"/>, which can't measure them.</param>
    /// <param name="gearing">The gearing.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="refPeriodStart">The reference period's start, the accrual start by default.</param>
    /// <param name="refPeriodEnd">The reference period's end, the accrual end by default.</param>
    /// <param name="observationSchedule">The observation schedule, from the start to the end.</param>
    /// <param name="lowerTrigger">The lower trigger, l.</param>
    /// <param name="upperTrigger">The upper trigger, u.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none; an argument QuantLib's constructor doesn't
    /// have, since its <c>setPricer</c> changes the coupon in place.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="observationSchedule"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The lower trigger isn't below the upper, or the schedule's start or end
    /// isn't the coupon's, each with QuantLib's text; or the gearing is 0.</exception>
    /// <exception cref="NanoQLException">The index has no forwarding curve, or no day counter was given, each with
    /// QuantLib's text.</exception>
    public RangeAccrualFloatersCoupon(
        Date paymentDate,
        double nominal,
        IborIndex index,
        Date startDate,
        Date endDate,
        int fixingDays,
        DayCounter? dayCounter,
        double gearing,
        double spread,
        Date? refPeriodStart,
        Date? refPeriodEnd,
        Schedule observationSchedule,
        double lowerTrigger,
        double upperTrigger,
        FloatingRateCouponPricer? pricer = null)
        : base(
            paymentDate,
            nominal,
            startDate,
            endDate,
            fixingDays,
            index,
            gearing,
            spread,
            refPeriodStart,
            refPeriodEnd,
            dayCounter,
            pricer: pricer)
    {
        ArgumentNullException.ThrowIfNull(observationSchedule);
        ObservationSchedule = observationSchedule;
        LowerTrigger = lowerTrigger;
        UpperTrigger = upperTrigger;
        if (!(lowerTrigger < upperTrigger))
        {
            throw new ArgumentException("lowerTrigger_>=upperTrigger", nameof(lowerTrigger));
        }

        if (!(observationSchedule.StartDate() == startDate))
        {
            throw new ArgumentException("incompatible start date", nameof(observationSchedule));
        }

        if (!(observationSchedule.EndDate() == endDate))
        {
            throw new ArgumentException("incompatible end date", nameof(observationSchedule));
        }

        // remove the end date and the start date
        ImmutableArray<Date> dates = observationSchedule.Dates;
        observationDates = dates[1..^1];

        YieldTermStructure rateCurve = index.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        DayCounter dc = dayCounter ?? throw new NanoQLException("no day counter implementation provided");
        Date referenceDate = rateCurve.ReferenceDate;
        StartTime = dc.YearFraction(referenceDate, startDate);
        EndTime = dc.YearFraction(referenceDate, endDate);
        observationTimes = [.. observationDates.Select(d => dc.YearFraction(referenceDate, d))];
    }

    /// <summary>Gets the start time, S, from the forwarding curve's reference date.</summary>
    public double StartTime { get; }

    /// <summary>Gets the end time, T, from the forwarding curve's reference date.</summary>
    public double EndTime { get; }

    /// <summary>Gets the lower trigger.</summary>
    public double LowerTrigger { get; }

    /// <summary>Gets the upper trigger.</summary>
    public double UpperTrigger { get; }

    /// <summary>Gets the number of observation dates.</summary>
    public int ObservationsNo => observationDates.Length;

    /// <summary>Gets the observation dates, the schedule's dates but its first and last.</summary>
    public ImmutableArray<Date> ObservationDates => observationDates;

    /// <summary>Gets the observation times, from the forwarding curve's reference date.</summary>
    public ImmutableArray<double> ObservationTimes => observationTimes;

    /// <summary>Gets the observation schedule.</summary>
    public Schedule ObservationSchedule { get; }

    /// <summary>The coupon's price with every observation counted: its accrual times the geared fixing plus the
    /// spread, times its nominal and the discount at its payment date.</summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public double PriceWithoutOptionality(YieldTermStructure discountCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(context);
        return AccrualPeriod() * ((Gearing * IndexFixing(context)) + Spread) * Nominal(context) * discountCurve.Discount(Date);
    }
}
