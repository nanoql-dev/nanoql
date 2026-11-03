// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2022 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cpicoupon.hpp ql/cashflows/cpicoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A coupon paying the performance of a zero-inflation (CPI) index: a fixed rate times the ratio of the index observed
/// at the accrual end, with the lag, to its base value.
/// </summary>
/// <remarks>
/// <para>
/// The base is the given base CPI or, without one, the index observed on the base date plus the lag. It accepts a
/// <see cref="CPICouponPricer"/>. No convexity adjustment is made for lags other than the one the curve was built with.
/// </para>
/// <para>
/// QuantLib's <see cref="CPILeg"/> gives every coupon <c>start − lag</c> as its base date, which loses the start's day
/// from a month-end start, and its notional flow a base on another convention, so a leg's coupons and notional could
/// price on two base CPIs. Here the leg sets one internal base observation for every flow (with neither a base CPI nor
/// a base date, the schedule's start; with a base date, the base date plus the lag; each at the lag), while
/// <see cref="BaseDate"/> stays QuantLib's; the public constructors keep QuantLib's convention. A documented
/// divergence.
/// </para>
/// </remarks>
public sealed class CPICoupon : InflationCoupon
{
    // The observation whose lagged fixing is the base, when no base CPI is given: QuantLib's baseDate + lag, with the
    // lag, from the public constructors; the leg's own from CPILeg.
    private readonly (Date Date, Period Lag)? baseObservation;

    /// <summary>Initializes a new instance of the <see cref="CPICoupon"/> class with a base CPI.</summary>
    /// <param name="baseCPI">The base CPI.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The accrual start date.</param>
    /// <param name="endDate">The accrual end date.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="fixedRate">The fixed rate the index ratio inflates.</param>
    /// <param name="refPeriodStart">The reference period's start.</param>
    /// <param name="refPeriodEnd">The reference period's end.</param>
    /// <param name="exCouponDate">The ex-coupon date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The base CPI is below 1e-16 in absolute value, or a date is
    /// <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="observationInterpolation"/> isn't a member of its
    /// type.</exception>
    public CPICoupon(
        double baseCPI,
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        ZeroInflationIndex index,
        Period observationLag,
        CPIInterpolationType observationInterpolation,
        DayCounter dayCounter,
        double fixedRate,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
        : this(
            baseCPI,
            null,
            paymentDate,
            nominal,
            startDate,
            endDate,
            index,
            observationLag,
            observationInterpolation,
            dayCounter,
            fixedRate,
            refPeriodStart,
            refPeriodEnd,
            exCouponDate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CPICoupon"/> class with a base date, on which the base CPI is
    /// observed.
    /// </summary>
    /// <param name="baseDate">The base date.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The accrual start date.</param>
    /// <param name="endDate">The accrual end date.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="fixedRate">The fixed rate the index ratio inflates.</param>
    /// <param name="refPeriodStart">The reference period's start.</param>
    /// <param name="refPeriodEnd">The reference period's end.</param>
    /// <param name="exCouponDate">The ex-coupon date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="observationInterpolation"/> isn't a member of its
    /// type.</exception>
    public CPICoupon(
        Date baseDate,
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        ZeroInflationIndex index,
        Period observationLag,
        CPIInterpolationType observationInterpolation,
        DayCounter dayCounter,
        double fixedRate,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
        : this(
            null,
            baseDate,
            paymentDate,
            nominal,
            startDate,
            endDate,
            index,
            observationLag,
            observationInterpolation,
            dayCounter,
            fixedRate,
            refPeriodStart,
            refPeriodEnd,
            exCouponDate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CPICoupon"/> class with a base CPI, a base date, or both, the base
    /// CPI used when given.
    /// </summary>
    /// <param name="baseCPI">The base CPI, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>.</param>
    /// <param name="baseDate">The base date, or <see langword="null"/>, QuantLib's <c>Date()</c>.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The accrual start date.</param>
    /// <param name="endDate">The accrual end date.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="fixedRate">The fixed rate the index ratio inflates.</param>
    /// <param name="refPeriodStart">The reference period's start.</param>
    /// <param name="refPeriodEnd">The reference period's end.</param>
    /// <param name="exCouponDate">The ex-coupon date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">Neither a base CPI nor a base date is given, the base CPI is below 1e-16 in
    /// absolute value, or a date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="observationInterpolation"/> isn't a member of its
    /// type.</exception>
    public CPICoupon(
        double? baseCPI,
        Date? baseDate,
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        ZeroInflationIndex index,
        Period observationLag,
        CPIInterpolationType observationInterpolation,
        DayCounter dayCounter,
        double fixedRate,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
        : this(
            baseCPI,
            baseDate,
            baseDate is Date d ? (d + observationLag, observationLag) : null,
            paymentDate,
            nominal,
            startDate,
            endDate,
            index,
            observationLag,
            observationInterpolation,
            dayCounter,
            fixedRate,
            refPeriodStart,
            refPeriodEnd,
            exCouponDate)
    {
    }

    // The leg's constructor: the base observation given, one base for every flow of a leg.
    internal CPICoupon(
        double? baseCPI,
        Date? baseDate,
        (Date Date, Period Lag)? baseObservation,
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        ZeroInflationIndex index,
        Period observationLag,
        CPIInterpolationType observationInterpolation,
        DayCounter dayCounter,
        double fixedRate,
        Date? refPeriodStart,
        Date? refPeriodEnd,
        Date? exCouponDate)
        : base(paymentDate, nominal, startDate, endDate, 0, index, observationLag, dayCounter, refPeriodStart, refPeriodEnd, exCouponDate)
    {
        Date.ThrowIfDefault(baseDate, nameof(baseDate));
        EnumArguments.Check(observationInterpolation, nameof(observationInterpolation));
        if (baseCPI is null && baseDate is null)
        {
            throw new ArgumentException(
                "baseCPI and baseDate can not be both null, provide a valid baseCPI or baseDate", nameof(baseCPI));
        }

        if (baseCPI is double cpi && !(Math.Abs(cpi) > 1e-16))
        {
            throw new ArgumentException("|baseCPI_| < 1e-16, future divide-by-zero problem", nameof(baseCPI));
        }

        BaseCPI = baseCPI;
        BaseDate = baseDate;
        FixedRate = fixedRate;
        ObservationInterpolation = observationInterpolation;
        CpiIndex = index;
        this.baseObservation = baseObservation;
    }

    /// <summary>Gets the fixed rate the index ratio inflates.</summary>
    public double FixedRate { get; }

    /// <summary>
    /// Gets the base value of the index, or <see langword="null"/> when the base date gives it; make sure it was
    /// observed with the coupon's interpolation.
    /// </summary>
    public double? BaseCPI { get; }

    /// <summary>Gets the base date of the base fixing, or <see langword="null"/> when a base CPI is given
    /// alone.</summary>
    public Date? BaseDate { get; }

    /// <summary>Gets how the index is observed.</summary>
    public CPIInterpolationType ObservationInterpolation { get; }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex CpiIndex { get; }

    /// <summary>The fixing: the index observed on the accrual end with the lag.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public override double IndexFixing(EvaluationContext context) =>
        CPI.LaggedFixing(CpiIndex, AccrualEndDate, ObservationLag, ObservationInterpolation, context);

    /// <summary>
    /// The ratio of the index observed on a date, with the lag, to the base, with no adjustment.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The ratio.</returns>
    public double IndexRatio(Date d, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(d, nameof(d));
        double i0 = BaseCPI ?? BaseFixing(context);
        double i1 = CPI.LaggedFixing(CpiIndex, d, ObservationLag, ObservationInterpolation, context);
        return i1 / i0;
    }

    /// <summary>The ratio of the end fixing to the base, as the pricer adjusts it: the rate over the fixed
    /// rate.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjusted growth.</returns>
    public double AdjustedIndexGrowth(EvaluationContext context) => Rate(context) / FixedRate;

    /// <summary>The amount accrued to a date, at the pricer's accrued rate for the date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The accrued amount.</returns>
    /// <exception cref="NanoQLException">The coupon has no pricer, or not a CPI-coupon pricer.</exception>
    public override double AccruedAmount(Date d, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(d, nameof(d));
        if (d <= AccrualStartDate || d > Date)
        {
            return 0.0;
        }

        var pricer = Pricer as CPICouponPricer ?? throw new NanoQLException("pricer not set or of wrong type");
        return Nominal(context) * pricer.AccruedRate(this, d, context) * AccruedPeriod(d);
    }

    /// <summary>Whether the coupon accepts a pricer: a CPI-coupon pricer.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is one.</returns>
    protected override bool CheckPricerImpl(InflationCouponPricer? pricer) => pricer is CPICouponPricer;

    // The index observed at the base observation, with its lag.
    private double BaseFixing(EvaluationContext context)
    {
        (Date date, Period lag) = baseObservation!.Value;
        return CPI.LaggedFixing(CpiIndex, date, lag, ObservationInterpolation, context);
    }
}
