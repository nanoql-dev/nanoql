// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

// <helper>

/// <summary>
/// A negotiable certificate of deposit as a bootstrap helper: its quote is the NCD's rate, and the curve implies the
/// coupon at which a bond over the NCD's schedule, from the evaluation date, is worth par.
/// </summary>
public sealed class NcdRateHelper : RelativeDateRateHelper
{
    private readonly Period tenor;
    private readonly Frequency frequency;
    private readonly Calendar calendar;
    private readonly DayCounter dayCounter;

    // Set by InitializeDates, which also runs on a copy of the helper, so not readonly.
    private FixedRateBond zeroCouponBond = null!;
    private FixedRateBond unitCouponBond = null!;

    /// <summary>Initializes a new instance of the <see cref="NcdRateHelper"/> class.</summary>
    /// <param name="rate">The NCD's rate, the helper's quote.</param>
    /// <param name="tenor">The NCD's tenor from the evaluation date.</param>
    /// <param name="frequency">The coupons' frequency.</param>
    /// <param name="calendar">The calendar the schedule's dates are adjusted on.</param>
    /// <param name="dayCounter">The coupons' day counter.</param>
    /// <param name="context">The context whose evaluation date the NCD starts on.</param>
    public NcdRateHelper(
        double rate, Period tenor, Frequency frequency, Calendar calendar, DayCounter dayCounter,
        EvaluationContext context)
        : base(rate, context)
    {
        this.tenor = tenor;
        this.frequency = frequency;
        this.calendar = calendar;
        this.dayCounter = dayCounter;
        InitializeDates(context);
    }

    /// <summary>The bond an NCD pays as: a coupon at each period's end, and par at maturity.</summary>
    /// <param name="start">The NCD's start, its evaluation date.</param>
    /// <param name="tenor">The NCD's tenor.</param>
    /// <param name="coupon">The coupon rate.</param>
    /// <param name="frequency">The coupons' frequency.</param>
    /// <param name="calendar">The calendar the schedule's dates are adjusted on.</param>
    /// <param name="dayCounter">The coupons' day counter.</param>
    /// <returns>The bond, settling on its start.</returns>
    public static FixedRateBond MakeBond(
        Date start, Period tenor, double coupon, Frequency frequency, Calendar calendar, DayCounter dayCounter)
    {
        Schedule schedule = new MakeSchedule()
            .From(start)
            .To(start + tenor)
            .WithFrequency(frequency)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .Backwards()
            .ToSchedule();
        return new FixedRateBond(
            0, 100.0, schedule, [coupon], dayCounter, BusinessDayConvention.ModifiedFollowing, 100.0, start);
    }

    /// <summary>The coupon at which the NCD's bond is worth par on the curve being bootstrapped.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The par coupon.</returns>
    public override double ImpliedQuote(EvaluationContext context)
    {
        RequireTermStructure();

        // A bond's dirty price is affine in its coupon, so the prices at coupons 0 and 1 give the par coupon.
        double p0 = Dirty(zeroCouponBond, context);
        double p1 = Dirty(unitCouponBond, context);
        return (100.0 - p0) / (p1 - p0);
    }

    /// <summary>Builds the NCD's bonds from the evaluation date, and sets the helper's dates from them.</summary>
    /// <param name="context">The context whose evaluation date the NCD starts on.</param>
    protected override void InitializeDates(EvaluationContext context)
    {
        Date start = context.EvaluationDate;
        zeroCouponBond = MakeBond(start, tenor, 0.0, frequency, calendar, dayCounter);
        unitCouponBond = MakeBond(start, tenor, 1.0, frequency, calendar, dayCounter);
        earliestDate = start;
        maturityDate = unitCouponBond.MaturityDate();
        latestDate = unitCouponBond.Cashflows[^1].Date;
    }

    private double Dirty(FixedRateBond bond, EvaluationContext context) =>
        bond.DirtyPrice(DiscountingBondEngine.Calculate(bond, context, TermStructure!), context);
}

// </helper>
