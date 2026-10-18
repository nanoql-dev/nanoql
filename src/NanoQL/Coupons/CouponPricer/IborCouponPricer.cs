// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/couponpricer.hpp ql/cashflows/couponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The base class of pricers for Ibor coupons, capped, floored or not.</summary>
/// <remarks>
/// <para>
/// The pricer resolves, when it is built, whether it prices indexed or at-par coupons (<see cref="UseIndexedCoupon"/>):
/// from an explicit choice, or from the context's <see cref="EvaluationContext.UseAtParCoupons"/>, read once, as
/// QuantLib reads <c>IborCoupon::Settings</c> in its constructor. A coupon priced by it keeps that choice whatever the
/// switch says later.
/// </para>
/// <para>
/// A pricer holds no per-coupon state: each member computes, inside the call, what QuantLib's <c>initialize(coupon)</c>
/// stores in the pricer and in the coupon's cache, with the pricer's own choice. So two coupons sharing one pricer
/// never read each other's values, and a pricer computes with its own choice on a coupon whose own pricer differs.
/// </para>
/// </remarks>
public abstract class IborCouponPricer : FloatingRateCouponPricer
{
    // Written only by the constructors and on a fresh copy (SetCapletVolatility).
    private OptionletVolatilityStructure? capletVol;

    /// <summary>
    /// Initializes a new instance of the <see cref="IborCouponPricer"/> class with the context's at-par switch:
    /// QuantLib's constructor without an explicit choice, which reads <c>IborCoupon::Settings</c>.
    /// </summary>
    /// <param name="context">The evaluation context, whose <see cref="EvaluationContext.UseAtParCoupons"/> is read once:
    /// the pricer prices indexed coupons when it is <see langword="false"/>.</param>
    /// <param name="v">The caplet volatility, or <see langword="null"/>, QuantLib's empty handle.</param>
    protected IborCouponPricer(EvaluationContext context, OptionletVolatilityStructure? v = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        capletVol = v;
        UseIndexedCoupon = !context.UseAtParCoupons;
    }

    /// <summary>Initializes a new instance of the <see cref="IborCouponPricer"/> class with an explicit choice.</summary>
    /// <param name="v">The caplet volatility, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="useIndexedCoupon">Whether the pricer prices indexed coupons, rather than at-par ones.</param>
    protected IborCouponPricer(OptionletVolatilityStructure? v, bool useIndexedCoupon)
    {
        capletVol = v;
        UseIndexedCoupon = useIndexedCoupon;
    }

    /// <summary>
    /// Gets a value indicating whether the pricer prices indexed coupons, whose fixing runs to the index's maturity,
    /// rather than at-par ones, whose fixing runs to the next fixing's value date.
    /// </summary>
    public bool UseIndexedCoupon { get; }

    /// <summary>Gets the caplet volatility, or <see langword="null"/> for none.</summary>
    public OptionletVolatilityStructure? CapletVolatility => capletVol;

    /// <summary>A copy of the pricer holding another caplet volatility, where QuantLib's changes the pricer.</summary>
    /// <param name="v">The caplet volatility, or <see langword="null"/> for none.</param>
    /// <returns>The copy, of the pricer's own type; the pricer is unchanged.</returns>
    public IborCouponPricer SetCapletVolatility(OptionletVolatilityStructure? v = null)
    {
        var copy = (IborCouponPricer)MemberwiseClone();
        copy.capletVol = v;
        return copy;
    }

    /// <summary>
    /// The dates and times of a coupon's fixing, QuantLib's <c>initializeCachedData</c>: the deposit's value and
    /// maturity dates, the end of the period the coupon's fixing spans (the index's maturity for an indexed or
    /// in-arrears coupon, else the next fixing's value date, at least a day after the value date), and the two periods
    /// as year fractions of the index's day counter.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="useIndexedCoupon">Whether the coupon is priced as an indexed coupon.</param>
    /// <returns>The dates and times.</returns>
    /// <exception cref="NanoQLException">The period the fixing spans has no positive length.</exception>
    internal static CachedData InitializeCachedData(IborCoupon coupon, bool useIndexedCoupon)
    {
        IborIndex index = coupon.IborIndex;
        Calendar calendar = index.FixingCalendar;
        Date fixingValueDate = calendar.Advance(coupon.StoredFixingDate, index.FixingDays, TimeUnit.Days);
        Date fixingMaturityDate = index.MaturityDate(fixingValueDate);
        Date fixingEndDate;

        if (useIndexedCoupon)
        {
            fixingEndDate = fixingMaturityDate;
        }
        else
        {
            if (coupon.IsInArrears)
            {
                fixingEndDate = fixingMaturityDate;
            }
            else
            {
                // par coupon approximation
                Date nextFixingDate = calendar.Advance(coupon.AccrualEndDate, -coupon.FixingDays, TimeUnit.Days);
                fixingEndDate = calendar.Advance(nextFixingDate, index.FixingDays, TimeUnit.Days);

                // make sure the estimation period contains at least one day
                Date oneDay = fixingValueDate + 1;
                fixingEndDate = fixingEndDate < oneDay ? oneDay : fixingEndDate;
            }
        }

        double spanningTime = index.DayCounter.YearFraction(fixingValueDate, fixingEndDate);
        if (!(spanningTime > 0.0))
        {
            string time = spanningTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
            throw new NanoQLException(
                $"\n cannot calculate forward rate between {fixingValueDate} and {fixingEndDate}:\n non positive time "
                + $"({time}) using {index.DayCounter.Name} daycounter");
        }

        double spanningTimeIndexMaturity = index.DayCounter.YearFraction(fixingValueDate, fixingMaturityDate);
        return new CachedData(fixingValueDate, fixingEndDate, fixingMaturityDate, spanningTime, spanningTimeIndexMaturity);
    }

    /// <summary>
    /// QuantLib's <c>initialize(coupon)</c> for one call: the checks, and the values its queries read, with the
    /// pricer's own at-par choice.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <returns>The coupon as an Ibor coupon, its fixing's dates and times, and its accrual period.</returns>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The fixing's period or the accrual period has no length.</exception>
    private protected CouponData Initialize(FloatingRateCoupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        IborCoupon iborCoupon = coupon as IborCoupon
            ?? throw new ArgumentException("IborCouponPricer: expected IborCoupon", nameof(coupon));
        CachedData cached = InitializeCachedData(iborCoupon, UseIndexedCoupon);
        double accrualPeriod = iborCoupon.AccrualPeriod();
        if (accrualPeriod == 0.0)
        {
            throw new NanoQLException("null accrual period");
        }

        return new CouponData(iborCoupon, cached, accrualPeriod);
    }

    /// <summary>The dates and times of an Ibor coupon's fixing, which QuantLib caches in the coupon.</summary>
    /// <param name="FixingValueDate">The start of the deposit underlying the index fixing.</param>
    /// <param name="FixingEndDate">The end of the period the coupon's fixing spans.</param>
    /// <param name="FixingMaturityDate">The end of the deposit underlying the index fixing.</param>
    /// <param name="SpanningTime">The period the coupon's fixing spans, as a year fraction.</param>
    /// <param name="SpanningTimeIndexMaturity">The deposit's period, as a year fraction.</param>
    internal readonly record struct CachedData(
        Date FixingValueDate,
        Date FixingEndDate,
        Date FixingMaturityDate,
        double SpanningTime,
        double SpanningTimeIndexMaturity);

    /// <summary>The values QuantLib's <c>initialize(coupon)</c> stores in an Ibor pricer.</summary>
    /// <param name="Coupon">The coupon.</param>
    /// <param name="Cached">Its fixing's dates and times, with the pricer's choice.</param>
    /// <param name="AccrualPeriod">Its accrual period.</param>
    private protected readonly record struct CouponData(IborCoupon Coupon, CachedData Cached, double AccrualPeriod);
}
