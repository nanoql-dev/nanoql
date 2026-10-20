// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcoupon.hpp ql/cashflows/inflationcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The base class of coupons paying a rate from an inflation index, whose pricer computes it: QuantLib's
/// <c>InflationCoupon</c>.
/// </summary>
/// <remarks>
/// <para>
/// The coupon holds its pricer, set at construction by a leg builder or by
/// <see cref="InflationCouponExtensions.SetPricer"/>, which returns a copy; each kind of coupon accepts only its kind
/// of pricer. Pricers are stateless functions of the coupon and the context (S3R4). The rate is computed on each call,
/// where QuantLib caches it.
/// </para>
/// <para>
/// The coupon has no gearing or spread: those belong to year-on-year coupons, not zero-inflation ones. Inflation
/// indexes have no day counters or calendars. An extension point.
/// </para>
/// </remarks>
public abstract class InflationCoupon : Coupon
{
    private readonly DayCounter dayCounter;

    // Written at construction and on a fresh copy (WithPricer), never after.
    private InflationCouponPricer? pricer;

    /// <summary>Initializes a new instance of the <see cref="InflationCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The accrual start date.</param>
    /// <param name="endDate">The accrual end date.</param>
    /// <param name="fixingDays">The fixing days before the reference period's end less the lag.</param>
    /// <param name="index">The inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="refPeriodStart">The reference period's start, the accrual start by default.</param>
    /// <param name="refPeriodEnd">The reference period's end, the accrual end by default; the reference period is
    /// before the lag.</param>
    /// <param name="exCouponDate">The ex-coupon date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    protected InflationCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int fixingDays,
        InflationIndex index,
        Period observationLag,
        DayCounter dayCounter,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        Date? exCouponDate = null)
        : base(paymentDate, nominal, startDate, endDate, refPeriodStart, refPeriodEnd, exCouponDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        Index = index ?? throw new ArgumentNullException(nameof(index), "no index provided");
        this.dayCounter = dayCounter ?? throw new ArgumentNullException(nameof(dayCounter));
        ObservationLag = observationLag;
        FixingDays = fixingDays;
    }

    /// <summary>Gets the inflation index.</summary>
    public InflationIndex Index { get; }

    /// <summary>Gets the observation lag, which is how far the fixing date is behind the reference period's
    /// end.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets the fixing days before the reference period's end less the lag.</summary>
    public int FixingDays { get; }

    /// <summary>Gets the pricer, or <see langword="null"/> when none is set.</summary>
    public InflationCouponPricer? Pricer => pricer;

    /// <inheritdoc/>
    public override DayCounter DayCounter => dayCounter;

    /// <summary>The coupon's rate: its pricer's swaplet rate.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">No pricer is set.</exception>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        InflationCouponPricer p = pricer ?? throw new NanoQLException("pricer not set");
        return p.SwapletRate(this, context);
    }

    /// <summary>The amount: the rate times the accrual period times the nominal.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public override double Amount(EvaluationContext context) => Rate(context) * AccrualPeriod() * Nominal(context);

    /// <summary>The amount accrued to a date: nothing outside the coupon's accrual, else the accrued share.</summary>
    /// <param name="d">The date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The accrued amount.</returns>
    public override double AccruedAmount(Date d, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(d, nameof(d));
        if (d <= AccrualStartDate || d > Date)
        {
            return 0.0;
        }

        return Nominal(context) * Rate(context) * AccruedPeriod(d);
    }

    /// <summary>The amount discounted on a curve, to the curve's reference date.</summary>
    /// <param name="discountingCurve">The discounting curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The discounted amount.</returns>
    public double Price(YieldTermStructure discountingCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountingCurve);
        return Amount(context) * discountingCurve.Discount(Date);
    }

    /// <summary>
    /// The fixing date: the reference period's end less the observation lag, moved back the fixing days on the index's
    /// fixing calendar, ModifiedPreceding, so that it stays in the same month.
    /// </summary>
    /// <returns>The fixing date.</returns>
    public virtual Date FixingDate() =>
        Index.FixingCalendar.Advance(
            ReferencePeriodEnd - ObservationLag, -FixingDays, TimeUnit.Days, BusinessDayConvention.ModifiedPreceding);

    /// <summary>The index's fixing on the fixing date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public virtual double IndexFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Index.Fixing(FixingDate(), context);
    }

    /// <summary>
    /// A copy of the coupon holding another pricer: <see cref="InflationCouponExtensions.SetPricer"/>'s step, which
    /// QuantLib's <c>setPricer</c> does in place. The pricer is checked first, with QuantLib's "pricer given is wrong
    /// type"; the base's copy is memberwise.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">The coupon refuses the pricer, a null one among them.</exception>
    protected internal virtual InflationCoupon WithPricer(InflationCouponPricer? pricer)
    {
        if (!CheckPricerImpl(pricer))
        {
            throw new ArgumentException("pricer given is wrong type", nameof(pricer));
        }

        var copy = (InflationCoupon)MemberwiseClone();
        copy.pricer = pricer;
        return copy;
    }

    /// <summary>Whether the coupon accepts a pricer.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    protected abstract bool CheckPricerImpl(InflationCouponPricer? pricer);
}
