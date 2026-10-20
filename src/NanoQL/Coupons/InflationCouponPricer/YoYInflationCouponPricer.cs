// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcouponpricer.hpp ql/cashflows/inflationcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The base pricer of year-on-year inflation coupons, capped and floored ones included: the swaplet is the gearing
/// times the coupon's fixing plus the spread; a volatility-dependent optionlet needs a derived pricer.
/// </summary>
/// <remarks>
/// <para>
/// An empty handle is <see langword="null"/>. A price discounts on the nominal curve, at the payment date when it is
/// after the curve's reference date, at 1 otherwise; with no nominal curve, a price refuses "no nominal term structure
/// provided", while a rate needs none.
/// </para>
/// <para>
/// An optionlet whose fixing date is at or before the volatility's base date pays its intrinsic value on the fixing.
/// QuantLib reads the volatility's base date before checking that there is a volatility, so its "missing optionlet
/// volatility" is unreachable and a missing one throws the empty handle's text; here the check comes first, a
/// documented divergence. An extension point: the Black, unit-displaced Black and Bachelier pricers derive from it.
/// </para>
/// </remarks>
public class YoYInflationCouponPricer : InflationCouponPricer
{
    // Written at construction and on a fresh copy (SetCapletVolatility), never after.
    private YoYOptionletVolatilitySurface? capletVol;

    /// <summary>Initializes a new instance of the <see cref="YoYInflationCouponPricer"/> class.</summary>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public YoYInflationCouponPricer(YieldTermStructure? nominalTermStructure = null)
        : this(null, nominalTermStructure)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="YoYInflationCouponPricer"/> class.</summary>
    /// <param name="capletVol">The optionlet volatility, or <see langword="null"/> for none.</param>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public YoYInflationCouponPricer(
        YoYOptionletVolatilitySurface? capletVol, YieldTermStructure? nominalTermStructure)
    {
        this.capletVol = capletVol;
        NominalTermStructure = nominalTermStructure;
    }

    /// <summary>Gets the optionlet volatility, or <see langword="null"/> for none.</summary>
    public virtual YoYOptionletVolatilitySurface? CapletVolatility => capletVol;

    /// <summary>Gets the nominal curve the prices discount on, or <see langword="null"/> for none.</summary>
    public virtual YieldTermStructure? NominalTermStructure { get; }

    /// <summary>A copy of the pricer with another optionlet volatility: QuantLib's
    /// <c>setCapletVolatility</c>.</summary>
    /// <param name="capletVol">The volatility.</param>
    /// <returns>The copy, of the pricer's own type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capletVol"/> is null, QuantLib's "empty capletVol
    /// handle".</exception>
    public virtual YoYInflationCouponPricer SetCapletVolatility(YoYOptionletVolatilitySurface capletVol)
    {
        if (capletVol is null)
        {
            throw new ArgumentNullException(nameof(capletVol), "empty capletVol handle");
        }

        var copy = (YoYInflationCouponPricer)MemberwiseClone();
        copy.capletVol = capletVol;
        return copy;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a year-on-year inflation coupon.</exception>
    /// <exception cref="NanoQLException">The pricer has no nominal curve.</exception>
    public override double SwapletPrice(InflationCoupon coupon, EvaluationContext context)
    {
        Data data = Initialize(coupon, context);
        double discount = data.Discount ?? throw new NanoQLException("no nominal term structure provided");
        return SwapletRate(data, context) * data.Coupon.AccrualPeriod() * discount;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a year-on-year inflation coupon.</exception>
    public override double SwapletRate(InflationCoupon coupon, EvaluationContext context) =>
        SwapletRate(Initialize(coupon, context), context);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a year-on-year inflation coupon.</exception>
    public override double CapletPrice(InflationCoupon coupon, double effectiveCap, EvaluationContext context)
    {
        Data data = Initialize(coupon, context);
        return data.Gearing * OptionletPrice(data.Coupon, OptionType.Call, effectiveCap, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a year-on-year inflation coupon.</exception>
    public override double CapletRate(InflationCoupon coupon, double effectiveCap, EvaluationContext context)
    {
        Data data = Initialize(coupon, context);
        return data.Gearing * OptionletRate(data.Coupon, OptionType.Call, effectiveCap, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a year-on-year inflation coupon.</exception>
    public override double FloorletPrice(InflationCoupon coupon, double effectiveFloor, EvaluationContext context)
    {
        Data data = Initialize(coupon, context);
        return data.Gearing * OptionletPrice(data.Coupon, OptionType.Put, effectiveFloor, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a year-on-year inflation coupon.</exception>
    public override double FloorletRate(InflationCoupon coupon, double effectiveFloor, EvaluationContext context)
    {
        Data data = Initialize(coupon, context);
        return data.Gearing * OptionletRate(data.Coupon, OptionType.Put, effectiveFloor, context);
    }

    /// <summary>An optionlet's price: its rate, accrued and discounted on the nominal curve.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="optionType">A call for a caplet, a put for a floorlet.</param>
    /// <param name="effStrike">The effective strike.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">The pricer has no nominal curve.</exception>
    protected virtual double OptionletPrice(
        YoYInflationCoupon coupon, OptionType optionType, double effStrike, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        double discount = Discount(coupon) ?? throw new NanoQLException("no nominal term structure provided");
        return OptionletRate(coupon, optionType, effStrike, context) * coupon.AccrualPeriod() * discount;
    }

    /// <summary>
    /// An optionlet's rate: its intrinsic value on the fixing when the fixing date is at or before the volatility's
    /// base date, else <see cref="OptionletPriceImp"/> on the adjusted fixing and the volatility's total variance.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="optionType">A call for a caplet, a put for a floorlet.</param>
    /// <param name="effStrike">The effective strike.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The pricer has no optionlet volatility.</exception>
    protected virtual double OptionletRate(
        YoYInflationCoupon coupon, OptionType optionType, double effStrike, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        Date fixingDate = coupon.FixingDate();

        // QuantLib reads the volatility's base date first, which makes this check unreachable.
        YoYOptionletVolatilitySurface vol =
            CapletVolatility ?? throw new NanoQLException("missing optionlet volatility");
        if (fixingDate <= vol.BaseDate())
        {
            // the amount is determined
            double a, b;
            if (optionType == OptionType.Call)
            {
                a = coupon.IndexFixing(context);
                b = effStrike;
            }
            else
            {
                a = effStrike;
                b = coupon.IndexFixing(context);
            }

            double diff = a - b;
            return diff < 0.0 ? 0.0 : diff;
        }

        // not yet determined, use Black/DD1/Bachelier/whatever from Impl
        double stdDev = Math.Sqrt(vol.TotalVariance(fixingDate, effStrike, new Period(0, TimeUnit.Days)));
        return OptionletPriceImp(optionType, effStrike, AdjustedFixing(coupon, context), stdDev);
    }

    /// <summary>
    /// An optionlet's rate, not discounted and not accrued, from the strike, the forward and the standard deviation,
    /// which a derived pricer computes; QuantLib's misleading name is kept.
    /// </summary>
    /// <param name="optionType">A call for a caplet, a put for a floorlet.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">Always, here: a volatility-dependent price needs a derived pricer.</exception>
    protected virtual double OptionletPriceImp(OptionType optionType, double strike, double forward, double stdDev) =>
        throw new NanoQLException("you must implement this to get a vol-dependent price");

    /// <summary>The fixing the optionlets are priced on: the coupon's fixing, with no convexity adjustment.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="fixing">A fixing to adjust, or <see langword="null"/> for the coupon's own.</param>
    /// <returns>The fixing.</returns>
    protected virtual double AdjustedFixing(YoYInflationCoupon coupon, EvaluationContext context, double? fixing = null)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return fixing ?? coupon.IndexFixing(context);
    }

    private static Data Initialize(InflationCoupon coupon, EvaluationContext context, YieldTermStructure? curve)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(coupon);
        var c = coupon as YoYInflationCoupon
            ?? throw new ArgumentException("year-on-year inflation coupon needed", nameof(coupon));
        return new Data(c, c.Gearing, c.Spread, DiscountOf(c, curve));
    }

    // A null discount where the nominal curve is missing, QuantLib's Null<Real>(); 1 at or before the curve's reference
    // date.
    private static double? DiscountOf(YoYInflationCoupon coupon, YieldTermStructure? curve)
    {
        if (curve is null)
        {
            return null;
        }

        Date paymentDate = coupon.Date;
        return paymentDate > curve.ReferenceDate ? curve.Discount(paymentDate) : 1.0;
    }

    private Data Initialize(InflationCoupon coupon, EvaluationContext context) =>
        Initialize(coupon, context, NominalTermStructure);

    private double? Discount(YoYInflationCoupon coupon) => DiscountOf(coupon, NominalTermStructure);

    // QuantLib's swapletRate: the gearing times the adjusted fixing plus the spread.
    private double SwapletRate(Data data, EvaluationContext context) =>
        (data.Gearing * AdjustedFixing(data.Coupon, context)) + data.Spread;

    // What QuantLib's initialize stores.
    private readonly record struct Data(YoYInflationCoupon Coupon, double Gearing, double Spread, double? Discount);
}
