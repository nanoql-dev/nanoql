// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/multipleresetscoupon.hpp ql/cashflows/multipleresetscoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;

namespace NanoQL.Coupons;

/// <summary>
/// The base of the multiple-resets coupon pricers: each reset's fixing plus the rate spread, which the compounding and
/// averaging pricers combine into the coupon's rate. Its prices and options are QuantLib's refusals.
/// </summary>
/// <remarks>
/// QuantLib's <c>initialize</c> keeps the coupon and its fixings in members; here <see cref="Fixings"/> computes them
/// from the coupon and the context on each call. An extension point: the compounding and averaging pricers derive.
/// </remarks>
public abstract class MultipleResetsPricer : FloatingRateCouponPricer
{
    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "MultipleResetsPricer::swapletPrice not
    /// implemented".</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        throw new NanoQLException("MultipleResetsPricer::swapletPrice not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "MultipleResetsPricer::capletPrice not
    /// implemented".</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw new NanoQLException("MultipleResetsPricer::capletPrice not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "MultipleResetsPricer::capletRate not
    /// implemented".</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw new NanoQLException("MultipleResetsPricer::capletRate not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "MultipleResetsPricer::floorletPrice not
    /// implemented".</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw new NanoQLException("MultipleResetsPricer::floorletPrice not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always, QuantLib's "MultipleResetsPricer::floorletRate not
    /// implemented".</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw new NanoQLException("MultipleResetsPricer::floorletRate not implemented");

    /// <summary>The coupon and each reset's fixing plus the rate spread: QuantLib's <c>initialize</c>.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The coupon and its fixings, one per fixing date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The coupon isn't a multiple-resets coupon, QuantLib's "sub-periods coupon
    /// required".</exception>
    /// <exception cref="NanoQLException">The coupon's accrual period is 0, QuantLib's "null accrual
    /// period".</exception>
    protected static (MultipleResetsCoupon Coupon, double[] Fixings) Fixings(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        var c = coupon as MultipleResetsCoupon
            ?? throw new ArgumentException("sub-periods coupon required", nameof(coupon));

        // the coupon's constructor takes an IborIndex, so QuantLib's "IborIndex required" can't arise
        var index = (IborIndex)c.Index;
        if (c.AccrualPeriod() == 0.0)
        {
            throw new NanoQLException("null accrual period");
        }

        var fixings = new double[c.FixingDates.Length];
        for (int i = 0; i < fixings.Length; i++)
        {
            fixings[i] = index.Fixing(c.FixingDates[i], context) + c.RateSpread;
        }

        return (c, fixings);
    }
}
