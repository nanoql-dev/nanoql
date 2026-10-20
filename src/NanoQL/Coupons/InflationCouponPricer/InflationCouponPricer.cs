// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcouponpricer.hpp ql/cashflows/inflationcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The base class of inflation-coupon pricers: QuantLib's <c>InflationCouponPricer</c>, which differs from the
/// floating-rate pricers because it deals with inflation indexes and their lags (the option may look three months back
/// and the index two).
/// </summary>
/// <remarks>
/// <para>
/// Pricers price the inverse cap and floorlets too, the usual ones paying when the underlying is above or below a
/// strike, so that conventional caps price simply; the non-inverse ones are from the coupon's side, a capped coupon
/// having a maximum at the strike.
/// </para>
/// <para>
/// Stateless (PR6.9): each member takes the coupon and the context, and computes inside the call what QuantLib's
/// <c>initialize</c> stores. An extension point.
/// </para>
/// </remarks>
public abstract class InflationCouponPricer
{
    /// <summary>The swaplet's price: the coupon's rate, accrued and discounted.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double SwapletPrice(InflationCoupon coupon, EvaluationContext context);

    /// <summary>The swaplet's rate: the coupon's rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double SwapletRate(InflationCoupon coupon, EvaluationContext context);

    /// <summary>The caplet's price at an effective cap.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double CapletPrice(InflationCoupon coupon, double effectiveCap, EvaluationContext context);

    /// <summary>The caplet's rate at an effective cap.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double CapletRate(InflationCoupon coupon, double effectiveCap, EvaluationContext context);

    /// <summary>The floorlet's price at an effective floor.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double FloorletPrice(InflationCoupon coupon, double effectiveFloor, EvaluationContext context);

    /// <summary>The floorlet's rate at an effective floor.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double FloorletRate(InflationCoupon coupon, double effectiveFloor, EvaluationContext context);
}
