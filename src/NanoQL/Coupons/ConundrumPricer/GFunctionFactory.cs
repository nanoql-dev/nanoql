// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The factory of Hagan's g-functions, one per yield-curve model
/// (<see cref="GFunctionFactoryYieldCurveModel"/>).</summary>
public static class GFunctionFactory
{
    /// <summary>The standard model's g-function.</summary>
    /// <param name="q">The fixed leg's payments a year.</param>
    /// <param name="delta">The payment's distance from the swap's start, in fixed-leg periods.</param>
    /// <param name="swapLength">The swap's length in years, where QuantLib's is a whole number of the tenor's units
    /// (the S-cms plan's CMR5 (i)); the model presumes swapLength·q equal periods.</param>
    /// <returns>The g-function.</returns>
    public static GFunction NewGFunctionStandard(int q, double delta, double swapLength) =>
        new GFunctionStandard(q, delta, swapLength);

    /// <summary>The exact-yield model's g-function of a coupon: its swap's fixed leg's accruals.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context the coupon's swap is built under.</param>
    /// <returns>The g-function.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The swap index has no forwarding curve.</exception>
    public static GFunction NewGFunctionExactYield(CmsCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        return new GFunctionExactYield(coupon, context);
    }

    /// <summary>The g-function of the forwarding curve's shifts for a coupon, shaped by a mean reversion; a parallel
    /// shift at a reversion of 0.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <param name="context">The evaluation context the coupon's swap is built and priced under.</param>
    /// <returns>The g-function.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The swap index has no forwarding curve, or its swap can't be
    /// priced.</exception>
    public static GFunction NewGFunctionWithShifts(CmsCoupon coupon, double meanReversion, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        return new GFunctionWithShifts(coupon, meanReversion, context);
    }
}
