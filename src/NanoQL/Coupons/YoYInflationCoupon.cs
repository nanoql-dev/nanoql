// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/yoyinflationcoupon.hpp ql/cashflows/yoyinflationcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A coupon paying a year-on-year inflation rate, with a gearing and a spread.</summary>
/// <remarks>
/// The fixing is the index's year-on-year rate observed on the accrual end with the lag, flat or interpolated
/// (<see cref="CPI.LaggedYoYRate"/>). It accepts a <see cref="YoYInflationCouponPricer"/>. An extension point: S-cms's
/// capped and floored coupon derives from it.
/// </remarks>
public class YoYInflationCoupon : InflationCoupon
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The accrual start date.</param>
    /// <param name="endDate">The accrual end date.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="index">The year-on-year inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="gearing">The gearing, the multiplier of the fixing.</param>
    /// <param name="spread">The spread over the geared fixing.</param>
    /// <param name="refPeriodStart">The reference period's start, the accrual start by default.</param>
    /// <param name="refPeriodEnd">The reference period's end, the accrual end by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="interpolation"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public YoYInflationCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int fixingDays,
        YoYInflationIndex index,
        Period observationLag,
        CPIInterpolationType interpolation,
        DayCounter dayCounter,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null)
        : base(paymentDate, nominal, startDate, endDate, fixingDays, index, observationLag, dayCounter, refPeriodStart, refPeriodEnd)
    {
        EnumArguments.Check(interpolation, nameof(interpolation));
        YoYIndex = index;
        Interpolation = interpolation;
        Gearing = gearing;
        Spread = spread;
    }

    /// <summary>Gets the gearing, the multiplier of the index's fixing.</summary>
    public double Gearing { get; }

    /// <summary>Gets the spread paid over the geared fixing.</summary>
    public double Spread { get; }

    /// <summary>Gets the year-on-year inflation index.</summary>
    public YoYInflationIndex YoYIndex { get; }

    /// <summary>Gets how the index is observed.</summary>
    public CPIInterpolationType Interpolation { get; }

    /// <summary>The fixing: the index's year-on-year rate observed on the accrual end with the lag.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public override double IndexFixing(EvaluationContext context) =>
        CPI.LaggedYoYRate(YoYIndex, AccrualEndDate, ObservationLag, Interpolation, context);

    /// <summary>The fixing the rate implies: the rate less the spread, over the gearing.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjusted fixing.</returns>
    public double AdjustedFixing(EvaluationContext context) => (Rate(context) - Spread) / Gearing;

    /// <summary>Whether the coupon accepts a pricer: a year-on-year inflation-coupon pricer.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is one.</returns>
    protected override bool CheckPricerImpl(InflationCouponPricer? pricer) => pricer is YoYInflationCouponPricer;
}
