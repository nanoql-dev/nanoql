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
/// The base of the range-accrual coupon pricers: the coupon's rate is its price over its accrual and the discount at
/// its payment date; its options are QuantLib's refusals.
/// </summary>
/// <remarks>
/// QuantLib's <c>initialize</c> keeps the coupon's terms, its discount and the index's fixings at its observation
/// schedule's dates in members; here they are a <see cref="State"/>, built per call from the coupon and the context,
/// from which a derived pricer prices. An extension point: the BGM pricer derives from it.
/// </remarks>
public abstract class RangeAccrualPricer : FloatingRateCouponPricer
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a range-accrual coupon.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletPrice(Initialize(coupon, context), context);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a range-accrual coupon.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context)
    {
        State s = Initialize(coupon, context);
        return SwapletPrice(s, context) / (s.AccrualFactor * s.Discount);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "RangeAccrualPricer::capletPrice not
    /// implemented".</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw new NanoQLException("RangeAccrualPricer::capletPrice not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "RangeAccrualPricer::capletRate not
    /// implemented".</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw new NanoQLException("RangeAccrualPricer::capletRate not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "RangeAccrualPricer::floorletPrice not
    /// implemented".</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw new NanoQLException("RangeAccrualPricer::floorletPrice not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "RangeAccrualPricer::floorletRate not
    /// implemented".</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw new NanoQLException("RangeAccrualPricer::floorletRate not implemented");

    /// <summary>The coupon's price from its state: QuantLib's <c>swapletPrice</c> after <c>initialize</c>.</summary>
    /// <param name="state">The coupon's state.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    protected abstract double SwapletPrice(State state, EvaluationContext context);

    /// <summary>The coupon's state: QuantLib's <c>initialize</c>.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The state.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The coupon isn't a range-accrual coupon, QuantLib's "range-accrual coupon
    /// required".</exception>
    /// <exception cref="NanoQLException">The index has no forwarding curve.</exception>
    protected static State Initialize(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        var c = coupon as RangeAccrualFloatersCoupon
            ?? throw new ArgumentException("range-accrual coupon required", nameof(coupon));

        // the coupon's constructor takes an IborIndex
        var index = (IborIndex)c.Index;
        YieldTermStructure rateCurve = index.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        double discount = rateCurve.Discount(c.Date);
        double accrualFactor = c.AccrualPeriod();

        ImmutableArray<Date> observationDates = c.ObservationSchedule.Dates;
        if (!(observationDates.Length == c.ObservationsNo + 2))
        {
            throw new NanoQLException("incompatible size of initialValues vector");
        }

        Calendar calendar = index.FixingCalendar;
        var initialValues = ImmutableArray.CreateBuilder<double>(observationDates.Length);
        foreach (Date d in observationDates)
        {
            initialValues.Add(index.Fixing(calendar.Advance(d, -c.FixingDays, TimeUnit.Days), context));
        }

        return new State(c, discount, accrualFactor, initialValues.MoveToImmutable());
    }

    /// <summary>A range-accrual coupon's state for one call: QuantLib's pricer members after
    /// <c>initialize</c>.</summary>
    protected sealed class State
    {
        internal State(RangeAccrualFloatersCoupon coupon, double discount, double accrualFactor, ImmutableArray<double> initialValues)
        {
            Coupon = coupon;
            Gearing = coupon.Gearing;
            Spread = coupon.Spread;
            Discount = discount;
            AccrualFactor = accrualFactor;
            SpreadLegValue = Spread * accrualFactor * discount;
            StartTime = coupon.StartTime;
            EndTime = coupon.EndTime;
            ObservationTimes = coupon.ObservationTimes;
            LowerTrigger = coupon.LowerTrigger;
            UpperTrigger = coupon.UpperTrigger;
            ObservationsNo = coupon.ObservationsNo;
            InitialValues = initialValues;
        }

        /// <summary>Gets the coupon.</summary>
        public RangeAccrualFloatersCoupon Coupon { get; }

        /// <summary>Gets the start time, S.</summary>
        public double StartTime { get; }

        /// <summary>Gets the end time, T.</summary>
        public double EndTime { get; }

        /// <summary>Gets the accrual factor, T − S on the coupon's day counter.</summary>
        public double AccrualFactor { get; }

        /// <summary>Gets the observation times, U.</summary>
        public ImmutableArray<double> ObservationTimes { get; }

        /// <summary>Gets the index's fixings at the observation schedule's dates, its start and end among
        /// them.</summary>
        public ImmutableArray<double> InitialValues { get; }

        /// <summary>Gets the number of observation dates.</summary>
        public int ObservationsNo { get; }

        /// <summary>Gets the lower trigger.</summary>
        public double LowerTrigger { get; }

        /// <summary>Gets the upper trigger.</summary>
        public double UpperTrigger { get; }

        /// <summary>Gets the forwarding curve's discount at the payment date.</summary>
        public double Discount { get; }

        /// <summary>Gets the gearing.</summary>
        public double Gearing { get; }

        /// <summary>Gets the spread.</summary>
        public double Spread { get; }

        /// <summary>Gets the spread's value, the spread times the accrual and the discount.</summary>
        public double SpreadLegValue { get; }
    }
}
