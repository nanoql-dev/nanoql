// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/cmsspreadcoupon.hpp ql/experimental/coupons/cmsspreadcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A CMS spread coupon: a floating-rate coupon fixing on a swap spread index.</summary>
/// <remarks>
/// <para>
/// This class does not perform any date adjustment, i.e., the start and end date passed upon construction should be
/// already rolled to a business day.
/// </para>
/// <para>
/// Its rate needs a <see cref="CmsSpreadCouponPricer"/>, which <see cref="CouponPricerFunctions"/> sets, refusing
/// any other pricer as QuantLib's visitor does; without one its rate refuses with QuantLib's "pricer not set".
/// Experimental, as in QuantLib.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class CmsSpreadCoupon : FloatingRateCoupon
{
    /// <summary>Initializes a new instance of the <see cref="CmsSpreadCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="fixingDays">The business days from the fixing date to the accrual start (or end, in arrears); the
    /// index's fixing days when <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="index">The swap spread index.</param>
    /// <param name="gearing">The gearing applied to the index fixing.</param>
    /// <param name="spread">The spread added to the geared fixing.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when
    /// <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>.</param>
    /// <param name="isInArrears">Whether the index fixes at the end of the accrual period.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <exception cref="ArgumentException">The gearing is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="fixingConvention"/> isn't a member of its type.</exception>
    public CmsSpreadCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int? fixingDays,
        SwapSpreadIndex index,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        bool isInArrears = false,
        Date? exCouponDate = null,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding)
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
            isInArrears,
            exCouponDate,
            fixingConvention)
    {
        SwapSpreadIndex = index;
    }

    /// <summary>Gets the swap spread index the coupon fixes on.</summary>
    public SwapSpreadIndex SwapSpreadIndex { get; }
}
