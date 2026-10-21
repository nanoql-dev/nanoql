// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/capflooredcoupon.hpp ql/cashflows/capflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A CMS coupon with a cap, a floor or both on its rate: a <see cref="CappedFlooredCoupon"/> over a
/// <see cref="CmsCoupon"/> it builds.</summary>
/// <remarks>
/// It is built with no pricer, as QuantLib's is. Its rate needs a CMS coupon pricer, which waits for batch 3 (B3.3);
/// until then <see cref="CouponPricerFunctions"/> refuses every pricer for it, as QuantLib's visitor refuses a pricer
/// that isn't a <c>CmsCouponPricer</c>.
/// </remarks>
public sealed class CappedFlooredCmsCoupon : CappedFlooredCoupon
{
    /// <summary>Initializes a new instance of the <see cref="CappedFlooredCmsCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="fixingDays">The business days from the fixing date to the accrual start (or end, in arrears); the
    /// index's fixing days when <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="index">The swap index.</param>
    /// <param name="gearing">The gearing applied to the index fixing.</param>
    /// <param name="spread">The spread added to the geared fixing.</param>
    /// <param name="cap">The cap on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="floor">The floor on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when
    /// <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>.</param>
    /// <param name="isInArrears">Whether the index fixes at the end of the accrual period.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <exception cref="ArgumentException">The gearing is zero, or the cap is less than the floor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="fixingConvention"/> isn't a member of its type.</exception>
    public CappedFlooredCmsCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int? fixingDays,
        SwapIndex index,
        double gearing = 1.0,
        double spread = 0.0,
        double? cap = null,
        double? floor = null,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        bool isInArrears = false,
        Date? exCouponDate = null,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding)
        : base(
            new CmsCoupon(
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
                isInArrears,
                exCouponDate,
                fixingConvention),
            cap,
            floor)
    {
    }
}
