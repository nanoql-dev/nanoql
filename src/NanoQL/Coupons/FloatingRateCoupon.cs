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

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The base class of coupons paying a floating rate: an index fixing, geared and spread.</summary>
/// <remarks>
/// <para>
/// Unsealed, as in QuantLib, where the Ibor, overnight and capped coupons derive from it. The coupon holds its pricer,
/// given at construction or by <see cref="FloatingRateCouponExtensions.SetPricer"/>, which returns a copy of the
/// coupon's own type; the coupon is never changed. The rate and the amount are the pricer's, computed on each call
/// from the coupon and the context, in place of QuantLib's cached lazy calculation.
/// </para>
/// <para>
/// A derived coupon that adds state overrides <see cref="WithPricer"/> only when a copy must change more than the
/// pricer (a capped coupon copies its underlying too); the base's copy is memberwise, so the derived state comes
/// along.
/// </para>
/// </remarks>
public class FloatingRateCoupon : Coupon
{
    // Written only by the constructor and on a fresh copy (WithPricer).
    private FloatingRateCouponPricer? pricer;
    private readonly DayCounter dayCounter;

    /// <summary>Initializes a new instance of the <see cref="FloatingRateCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="fixingDays">The business days from the fixing date to the accrual start (or end, in arrears); the
    /// index's fixing days when <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="index">The index.</param>
    /// <param name="gearing">The gearing applied to the index fixing.</param>
    /// <param name="spread">The spread added to the geared fixing.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>, QuantLib's empty
    /// day counter.</param>
    /// <param name="isInArrears">Whether the index fixes at the end of the accrual period.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> to set one later through
    /// <see cref="FloatingRateCouponExtensions.SetPricer"/>; an argument QuantLib's constructor doesn't have, since an
    /// immutable coupon can't be given one afterwards.</param>
    /// <exception cref="ArgumentException">The gearing is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="fixingConvention"/> isn't a member of its type.</exception>
    public FloatingRateCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int? fixingDays,
        InterestRateIndex index,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        bool isInArrears = false,
        Date? exCouponDate = null,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding,
        FloatingRateCouponPricer? pricer = null)
        : base(paymentDate, nominal, startDate, endDate, refPeriodStart, refPeriodEnd, exCouponDate)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (fixingDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(fixingDays));
        }

        EnumArguments.Check(fixingConvention, nameof(fixingConvention));
        if (gearing == 0)
        {
            throw new ArgumentException("Null gearing not allowed", nameof(gearing));
        }

        Index = index;
        FixingDays = fixingDays ?? index.FixingDays;
        Gearing = gearing;
        Spread = spread;
        this.dayCounter = dayCounter ?? index.DayCounter;
        IsInArrears = isInArrears;
        FixingConvention = fixingConvention;
        this.pricer = pricer;
    }

    /// <summary>Gets the index.</summary>
    public InterestRateIndex Index { get; }

    /// <summary>Gets the fixing days.</summary>
    public int FixingDays { get; }

    /// <summary>Gets the gearing applied to the index fixing.</summary>
    public double Gearing { get; }

    /// <summary>Gets the spread added to the geared fixing.</summary>
    public double Spread { get; }

    /// <summary>Gets a value indicating whether the index fixes at the end of the accrual period.</summary>
    public bool IsInArrears { get; }

    /// <summary>Gets the convention adjusting the fixing date.</summary>
    public BusinessDayConvention FixingConvention { get; }

    /// <summary>Gets the pricer, or <see langword="null"/> when none is set.</summary>
    public FloatingRateCouponPricer? Pricer => pricer;

    /// <inheritdoc/>
    public override DayCounter DayCounter => dayCounter;

    /// <summary>
    /// The fixing date: the fixing days before the accrual start, or before the accrual end in arrears, on the index's
    /// fixing calendar.
    /// </summary>
    /// <returns>The fixing date.</returns>
    public virtual Date FixingDate()
    {
        // if isInArrears_ fix at the end of period
        Date refDate = IsInArrears ? AccrualEndDate : AccrualStartDate;
        return Index.FixingCalendar.Advance(refDate, -FixingDays, TimeUnit.Days, FixingConvention);
    }

    /// <summary>The coupon's rate: its pricer's swaplet rate.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">No pricer is set.</exception>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        FloatingRateCouponPricer p = pricer ?? throw new NanoQLException("pricer not set");
        return p.SwapletRate(this, context);
    }

    /// <inheritdoc/>
    public override double Amount(EvaluationContext context) =>
        Rate(context) * AccrualPeriod() * Nominal(context);

    /// <summary>The coupon's amount discounted on a curve, to the curve's reference date.</summary>
    /// <param name="discountingCurve">The discounting curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The discounted amount.</returns>
    public double Price(YieldTermStructure discountingCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountingCurve);
        return Amount(context) * discountingCurve.Discount(Date);
    }

    /// <inheritdoc/>
    public override double AccruedAmount(Date d, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        if (d <= AccrualStartDate || d > Date)
        {
            // out of coupon range
            return 0.0;
        }
        else
        {
            return Nominal(context) * Rate(context) * AccruedPeriod(d);
        }
    }

    /// <summary>The index fixing on the coupon's fixing date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public virtual double IndexFixing(EvaluationContext context) => Index.Fixing(FixingDate(), context);

    /// <summary>The convexity adjustment: the adjusted fixing less the index fixing.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjustment.</returns>
    public virtual double ConvexityAdjustment(EvaluationContext context) =>
        ConvexityAdjustmentImpl(IndexFixing(context), context);

    /// <summary>The fixing the rate implies: the rate less the spread, over the gearing.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjusted fixing.</returns>
    public virtual double AdjustedFixing(EvaluationContext context) => (Rate(context) - Spread) / Gearing;

    /// <summary>
    /// A copy of the coupon holding another pricer: <see cref="FloatingRateCouponExtensions.SetPricer"/>'s step, which
    /// QuantLib's <c>setPricer</c> does in place. The base's copy is memberwise; an override returns a copy of the same
    /// runtime type.
    /// </summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    protected internal virtual FloatingRateCoupon WithPricer(FloatingRateCouponPricer? pricer)
    {
        var copy = (FloatingRateCoupon)MemberwiseClone();
        copy.pricer = pricer;
        return copy;
    }

    /// <summary>The convexity adjustment of a given fixing: 0 at a zero gearing, else the adjusted fixing less it.</summary>
    /// <param name="fixing">The fixing.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjustment.</returns>
    protected double ConvexityAdjustmentImpl(double fixing, EvaluationContext context) =>
        Gearing == 0.0 ? 0.0 : AdjustedFixing(context) - fixing;
}
