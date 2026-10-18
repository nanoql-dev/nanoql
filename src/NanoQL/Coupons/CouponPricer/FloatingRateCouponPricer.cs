// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/couponpricer.hpp ql/cashflows/couponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The base class of pricers for floating-rate coupons.</summary>
/// <remarks>
/// A pricer is a stateless function of the coupon and the context: each member computes, inside the call, the
/// per-coupon values that QuantLib's <c>initialize(coupon)</c> stores in the pricer before its queries. So one pricer
/// may serve any number of coupons, on any thread, and a query never depends on another coupon's earlier one.
/// </remarks>
public abstract class FloatingRateCouponPricer
{
    /// <summary>The swaplet's price: the coupon's amount, discounted.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context);

    /// <summary>The swaplet's rate: the coupon's rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context);

    /// <summary>The price of a caplet on the coupon's rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The cap on the index fixing.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context);

    /// <summary>The rate of a caplet on the coupon's rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The cap on the index fixing.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context);

    /// <summary>The price of a floorlet on the coupon's rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The floor on the index fixing.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context);

    /// <summary>The rate of a floorlet on the coupon's rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The floor on the index fixing.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context);
}
