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

/// <summary>An Ibor coupon with a cap, a floor or both on its rate: a <see cref="CappedFlooredCoupon"/> over an
/// <see cref="IborCoupon"/> it builds.</summary>
/// <remarks>
/// It is built with no pricer, as QuantLib's is; <see cref="FloatingRateCouponExtensions.SetPricer"/> gives it and its
/// underlying one, which must be an <see cref="IborCouponPricer"/> for its rate.
/// </remarks>
public sealed class CappedFlooredIborCoupon : CappedFlooredCoupon
{
    /// <summary>Initializes a new instance of the <see cref="CappedFlooredIborCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="fixingDays">The business days from the fixing date to the accrual start (or end, in arrears); the
    /// index's fixing days when <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="index">The index.</param>
    /// <param name="gearing">The gearing applied to the index fixing.</param>
    /// <param name="spread">The spread added to the geared fixing.</param>
    /// <param name="cap">The cap on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="floor">The floor on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>.</param>
    /// <param name="isInArrears">Whether the index fixes at the end of the accrual period.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <exception cref="ArgumentException">The gearing is zero, or the cap is less than the floor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="fixingConvention"/> isn't a member of its type.</exception>
    public CappedFlooredIborCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int? fixingDays,
        IborIndex index,
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
            new IborCoupon(
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
