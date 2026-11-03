// SPDX-FileCopyrightText: Copyright (C) 2007, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/iborcoupon.hpp ql/cashflows/iborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A coupon paying an Ibor index's fixing, geared and spread.</summary>
/// <remarks>
/// <para>
/// Unsealed, as in QuantLib, where <see cref="StubIborCoupon"/> derives from it. The fixing date is computed when the
/// coupon is built. The fixing's other dates and times (<see cref="FixingValueDate"/>, <see cref="FixingEndDate"/>,
/// <see cref="FixingMaturityDate"/>, <see cref="SpanningTime"/>, <see cref="SpanningTimeIndexMaturity"/>), which
/// QuantLib caches in the coupon, are computed on each call from the coupon's own <see cref="IborCouponPricer"/>,
/// whose at-par choice decides the end date and the spanning time.
/// </para>
/// <para>
/// A pricer reaches the fixing through <see cref="IndexFixing(EvaluationContext, bool)"/> with its own choice; the
/// public <see cref="IndexFixing(EvaluationContext)"/> passes the coupon's own pricer's. A subclass that fixes
/// differently overrides the former, which the public member and every pricer call.
/// </para>
/// <para>
/// QuantLib's <c>IborCoupon::Settings</c> is the context's <see cref="EvaluationContext.UseAtParCoupons"/>, which a
/// pricer reads when it is built without an explicit choice.
/// </para>
/// </remarks>
public class IborCoupon : FloatingRateCoupon
{
    private readonly Date fixingDate;

    /// <summary>Initializes a new instance of the <see cref="IborCoupon"/> class.</summary>
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
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>.</param>
    /// <param name="isInArrears">Whether the index fixes at the end of the accrual period.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> to set one later through
    /// <see cref="FloatingRateCouponExtensions.SetPricer"/>.</param>
    /// <exception cref="ArgumentException">The gearing is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="fixingConvention"/> isn't a member of its type.</exception>
    public IborCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int? fixingDays,
        IborIndex index,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        bool isInArrears = false,
        Date? exCouponDate = null,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding,
        FloatingRateCouponPricer? pricer = null)
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
            fixingConvention,
            pricer)
    {
        IborIndex = index;
        fixingDate = base.FixingDate();
    }

    /// <summary>Gets the Ibor index.</summary>
    public IborIndex IborIndex { get; }

    /// <summary>Gets the fixing date computed when the coupon was built, which QuantLib's pricers read directly.</summary>
    internal Date StoredFixingDate => fixingDate;

    /// <inheritdoc/>
    public override Date FixingDate() => fixingDate;

    /// <summary>
    /// Whether the coupon has fixed: its fixing date is before the evaluation date, or on it when the context enforces
    /// today's historic fixings or holds a fixing for that date.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the coupon has fixed.</returns>
    public virtual bool HasFixed(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date today = context.EvaluationDate;

        if (fixingDate > today)
        {
            return false;
        }
        else if (fixingDate < today)
        {
            return true;
        }
        else
        {
            // fixingDate_ == today
            if (context.EnforcesTodaysHistoricFixings)
            {
                return true;
            }
            else
            {
                return Index.HasHistoricalFixing(fixingDate, context);
            }
        }
    }

    /// <summary>
    /// The index fixing, with the coupon's own pricer's at-par choice: the stored fixing once the coupon has fixed,
    /// otherwise the forecast over the period the coupon's fixing spans.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer, a fixing it needs is missing, or the period
    /// has no positive length.</exception>
    public sealed override double IndexFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // With no Ibor pricer there is no choice to pass: this class's fixing refuses the case, as QuantLib's does,
        // and a subclass whose fixing reads no choice (the stub coupon) fixes without one.
        return IndexFixing(context, (Pricer as IborCouponPricer)?.UseIndexedCoupon ?? false);
    }

    /// <summary>
    /// The start of the deposit underlying the index fixing: the index's fixing days after the fixing date.
    /// </summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer, or the period has no positive length.</exception>
    public Date FixingValueDate() => CachedData().FixingValueDate;

    /// <summary>The end of the deposit underlying the index fixing: the index's maturity from the value date.</summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer, or the period has no positive length.</exception>
    public Date FixingMaturityDate() => CachedData().FixingMaturityDate;

    /// <summary>
    /// The end of the period the coupon's fixing spans: the index's maturity for an indexed or in-arrears coupon, and
    /// for an at-par coupon the value date of the next fixing.
    /// </summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer, or the period has no positive length.</exception>
    public Date FixingEndDate() => CachedData().FixingEndDate;

    /// <summary>The deposit's period, from the value date to the maturity, as a year fraction.</summary>
    /// <returns>The year fraction, by the index's day counter.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer, or the period has no positive length.</exception>
    public double SpanningTimeIndexMaturity() => CachedData().SpanningTimeIndexMaturity;

    /// <summary>The period the coupon's fixing spans, from the value date to the end date, as a year fraction.</summary>
    /// <returns>The year fraction, by the index's day counter.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer, or the period has no positive length.</exception>
    public double SpanningTime() => CachedData().SpanningTime;

    /// <summary>
    /// The index fixing with a given at-par choice: the member every pricer calls with its own choice, and that a
    /// subclass fixing differently overrides (QuantLib's virtual <c>indexFixing()</c>).
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="useIndexedCoupon">Whether the fixing spans the index's maturity rather than the at-par period.</param>
    /// <returns>The fixing.</returns>
    /// <exception cref="NanoQLException">The coupon has no Ibor pricer of its own, a fixing it needs is missing, or the
    /// period has no positive length.</exception>
    protected internal virtual double IndexFixing(EvaluationContext context, bool useIndexedCoupon)
    {
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib reads the coupon's own pricer here, before its cache.
        _ = OwnPricer();
        IborCouponPricer.CachedData cached = IborCouponPricer.InitializeCachedData(this, useIndexedCoupon);

        /* instead of just returning index_->fixing(fixingValueDate_)
           its logic is duplicated here using a specialized iborIndex
           forecastFixing overload which
           1) allows to save date/time recalculations, and
           2) takes into account par coupon needs
        */

        if (HasFixed(context))
        {
            return Index.PastFixing(fixingDate, context)
                ?? throw new NanoQLException($"Missing {Index.Name} fixing for {fixingDate}");
        }
        else if (IborIndex.ForecastsThroughFixingDate)
        {
            // an index whose forecast is its own, which QuantLib's coupon misses
            return IborIndex.ForecastFixing(fixingDate, context);
        }
        else
        {
            return IborIndex.ForecastFixing(cached.FixingValueDate, cached.FixingEndDate, cached.SpanningTime);
        }
    }

    // The coupon's own Ibor pricer, which its cached quantities follow.
    private IborCouponPricer OwnPricer() =>
        Pricer as IborCouponPricer
        ?? throw new NanoQLException("IborCoupon: pricer not set or not derived from IborCouponPricer");

    private IborCouponPricer.CachedData CachedData() =>
        IborCouponPricer.InitializeCachedData(this, OwnPricer().UseIndexedCoupon);
}
