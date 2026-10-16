// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/coupon.hpp ql/cashflows/coupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: accruedPeriod(d) and accruedDays(d), which wait for the slot that ports the cash-flow functions using them

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The base class of coupons accruing over a fixed period: still abstract, it gives derived classes the accrual-period
/// calculations.
/// </summary>
/// <remarks>QuantLib's <c>coupon_cast</c> is C#'s <c>cashFlow as Coupon</c>.</remarks>
public abstract class Coupon : CashFlow
{
    private readonly Date paymentDate;
    private readonly double nominal;
    private readonly Date? exCouponDate;

    /// <summary>Initializes a new instance of the <see cref="Coupon"/> class.</summary>
    /// <param name="paymentDate">The payment date, which must already be a business day.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="accrualStartDate">The start of the accrual period.</param>
    /// <param name="accrualEndDate">The end of the accrual period.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    protected Coupon(
        Date paymentDate,
        double nominal,
        Date accrualStartDate,
        Date accrualEndDate,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
    {
        Date.ThrowIfDefault(paymentDate, nameof(paymentDate));
        Date.ThrowIfDefault(accrualStartDate, nameof(accrualStartDate));
        Date.ThrowIfDefault(accrualEndDate, nameof(accrualEndDate));
        Date.ThrowIfDefault(refPeriodStart, nameof(refPeriodStart));
        Date.ThrowIfDefault(refPeriodEnd, nameof(refPeriodEnd));
        Date.ThrowIfDefault(exCouponDate, nameof(exCouponDate));
        this.paymentDate = paymentDate;
        this.nominal = nominal;
        AccrualStartDate = accrualStartDate;
        AccrualEndDate = accrualEndDate;
        ReferencePeriodStart = refPeriodStart ?? accrualStartDate;
        ReferencePeriodEnd = refPeriodEnd ?? accrualEndDate;
        this.exCouponDate = exCouponDate;
    }

    /// <inheritdoc/>
    public override Date Date => paymentDate;

    /// <inheritdoc/>
    public override Date? ExCouponDate => exCouponDate;

    /// <summary>Gets the start of the accrual period.</summary>
    public Date AccrualStartDate { get; }

    /// <summary>Gets the end of the accrual period.</summary>
    public Date AccrualEndDate { get; }

    /// <summary>Gets the start of the reference period.</summary>
    public Date ReferencePeriodStart { get; }

    /// <summary>Gets the end of the reference period.</summary>
    public Date ReferencePeriodEnd { get; }

    /// <summary>Gets the day counter used for accrual.</summary>
    public abstract DayCounter DayCounter { get; }

    /// <summary>The nominal.</summary>
    /// <returns>The nominal.</returns>
    public virtual double Nominal() => nominal;

    /// <summary>The coupon's rate.</summary>
    /// <returns>The rate.</returns>
    public abstract double Rate();

    /// <summary>The accrual period as a fraction of a year, by the coupon's day counter.</summary>
    /// <returns>The accrual period.</returns>
    public double AccrualPeriod() =>
        DayCounter.YearFraction(AccrualStartDate, AccrualEndDate, ReferencePeriodStart, ReferencePeriodEnd);

    /// <summary>The number of days in the accrual period, by the coupon's day counter.</summary>
    /// <returns>The day count.</returns>
    public int AccrualDays() => DayCounter.DayCount(AccrualStartDate, AccrualEndDate);

    /// <summary>The amount accrued up to a date.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The accrued amount.</returns>
    public abstract double AccruedAmount(Date d);
}
