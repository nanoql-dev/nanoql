// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcoupon.hpp ql/cashflows/inflationcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>QuantLib's <c>InflationCoupon::setPricer</c>, which changes a coupon, as a copy of the receiver's
/// type.</summary>
public static class InflationCouponExtensions
{
    /// <summary>A copy of an inflation coupon holding another pricer.</summary>
    /// <typeparam name="T">The coupon's type, which the copy keeps.</typeparam>
    /// <param name="coupon">The coupon; it is unchanged.</param>
    /// <param name="pricer">The pricer, of the kind the coupon accepts.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="coupon"/> is null.</exception>
    /// <exception cref="ArgumentException">The coupon refuses the pricer, with QuantLib's "pricer given is wrong
    /// type".</exception>
    public static T SetPricer<T>(this T coupon, InflationCouponPricer? pricer)
        where T : InflationCoupon
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return (T)coupon.WithPricer(pricer);
    }
}
