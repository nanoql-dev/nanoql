// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/fixedratecoupon.hpp ql/cashflows/fixedratecoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A coupon paying a fixed interest rate.</summary>
/// <remarks>The amount is computed on each call, in place of QuantLib's cached lazy calculation.</remarks>
public sealed class FixedRateCoupon : Coupon
{
    private readonly InterestRate rate;

    /// <summary>Initializes a new instance of the <see cref="FixedRateCoupon"/> class from a simple annual rate.</summary>
    /// <param name="paymentDate">The payment date, which must already be a business day.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="rate">The rate, simply compounded and annual.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="accrualStartDate">The start of the accrual period.</param>
    /// <param name="accrualEndDate">The end of the accrual period.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    public FixedRateCoupon(
        Date paymentDate,
        double nominal,
        double rate,
        DayCounter dayCounter,
        Date accrualStartDate,
        Date accrualEndDate,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
        : this(
            paymentDate,
            nominal,
            new InterestRate(rate, dayCounter, Compounding.Simple, Frequency.Annual),
            accrualStartDate,
            accrualEndDate,
            refPeriodStart,
            refPeriodEnd,
            exCouponDate)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FixedRateCoupon"/> class from an interest rate.</summary>
    /// <param name="paymentDate">The payment date, which must already be a business day.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="interestRate">The interest rate, with its day counter and compounding.</param>
    /// <param name="accrualStartDate">The start of the accrual period.</param>
    /// <param name="accrualEndDate">The end of the accrual period.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    public FixedRateCoupon(
        Date paymentDate,
        double nominal,
        InterestRate interestRate,
        Date accrualStartDate,
        Date accrualEndDate,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
        : base(paymentDate, nominal, accrualStartDate, accrualEndDate, refPeriodStart, refPeriodEnd, exCouponDate)
    {
        ArgumentNullException.ThrowIfNull(interestRate);
        rate = interestRate;
    }

    /// <summary>Gets the interest rate.</summary>
    public InterestRate InterestRate => rate;

    /// <inheritdoc/>
    public override DayCounter DayCounter => rate.DayCounter;

    /// <inheritdoc/>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Amount();
    }

    /// <inheritdoc/>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return rate.Rate;
    }

    /// <inheritdoc/>
    public override double AccruedAmount(Date d, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AccruedAmount(d);
    }

    // The amount, which reads no context: the coupon's nominal is its contract nominal (SID10). The credit default
    // swap's constructor, which takes no context, reads its accrual rebate through it.
    internal double Amount() =>
        ContractNominal * (rate.CompoundFactor(AccrualStartDate, AccrualEndDate, ReferencePeriodStart, ReferencePeriodEnd) - 1.0);

    // The accrued amount at a date, which reads no context (SID10).
    internal double AccruedAmount(Date d)
    {
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        if (d <= AccrualStartDate || d > Date)
        {
            // out of coupon range
            return 0.0;
        }
        else if (TradingExCouponOn(d))
        {
            Date end = d > AccrualEndDate ? d : AccrualEndDate;
            return -ContractNominal * (rate.CompoundFactor(d, end, ReferencePeriodStart, ReferencePeriodEnd) - 1.0);
        }
        else
        {
            // usual case
            Date end = d < AccrualEndDate ? d : AccrualEndDate;
            return ContractNominal * (rate.CompoundFactor(AccrualStartDate, end, ReferencePeriodStart, ReferencePeriodEnd) - 1.0);
        }
    }
}
