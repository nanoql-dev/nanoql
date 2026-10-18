// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/floatingratecoupon.hpp ql/cashflows/floatingratecoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// <see cref="FloatingRateCoupon"/>'s modifier, which returns a modified copy of the receiver, keeping its static type,
/// where QuantLib's <c>setPricer</c> changes the coupon.
/// </summary>
public static class FloatingRateCouponExtensions
{
    /// <summary>
    /// A copy of the coupon holding another pricer. A capped or floored Ibor coupon's copy carries the pricer into a
    /// copy of its underlying, as QuantLib's <c>CappedFlooredCoupon::setPricer</c> sets both; a capped overnight
    /// coupon's sets its own pricer only, as QuantLib's does.
    /// </summary>
    /// <typeparam name="T">The coupon's type.</typeparam>
    /// <param name="coupon">The coupon.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public static T SetPricer<T>(this T coupon, FloatingRateCouponPricer? pricer)
        where T : FloatingRateCoupon
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return (T)coupon.WithPricer(pricer);
    }
}
