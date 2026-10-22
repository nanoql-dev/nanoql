// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cmscoupon.hpp ql/cashflows/cmscoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A CMS coupon: a floating-rate coupon fixing on a swap index.</summary>
/// <remarks>
/// <para>
/// This class does not perform any date adjustment, i.e., the start and end date passed upon construction should be
/// already rolled to a business day.
/// </para>
/// <para>
/// Its rate needs a CMS coupon pricer, which QuantLib's <c>CmsCouponPricer</c> family gives and which waits for batch 3
/// (S-cms): until then <see cref="CouponPricerFunctions"/> refuses every pricer for it, as QuantLib's visitor refuses a
/// pricer that isn't a <c>CmsCouponPricer</c>, and its rate refuses with QuantLib's "pricer not set".
/// </para>
/// </remarks>
public sealed class CmsCoupon : FloatingRateCoupon
{
    /// <summary>Initializes a new instance of the <see cref="CmsCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="fixingDays">The business days from the fixing date to the accrual start (or end, in arrears); the
    /// index's fixing days when <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="index">The swap index.</param>
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
    public CmsCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int? fixingDays,
        SwapIndex index,
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
        SwapIndex = index;
    }

    /// <summary>Gets the swap index the coupon fixes on.</summary>
    public SwapIndex SwapIndex { get; }
}
