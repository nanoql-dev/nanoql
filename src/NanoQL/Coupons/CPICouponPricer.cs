// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2022 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cpicouponpricer.hpp ql/cashflows/cpicouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The base pricer of CPI coupons: the swaplet is the fixed rate times the index ratio; a volatility-dependent
/// optionlet needs a derived pricer.
/// </summary>
/// <remarks>
/// <para>
/// An empty handle is <see langword="null"/>. A price discounts on the nominal curve, at the payment date when it is
/// after the curve's reference date, at 1 otherwise; with no nominal curve, a price refuses "no nominal term structure
/// provided", while a rate needs none.
/// </para>
/// <para>
/// QuantLib casts the coupon without a check and then reads through the null pointer, which is undefined; here a coupon
/// that isn't a CPI coupon is refused, "CPI coupon needed". An optionlet whose fixing date is at or before the
/// evaluation date pays its intrinsic value on the index ratio, the quantity the live optionlet's forward reads, where
/// QuantLib's reads the index's level against a strike on the ratio; both are documented divergences. An extension
/// point.
/// </para>
/// </remarks>
public class CPICouponPricer : InflationCouponPricer
{
    // Written at construction and on a fresh copy (SetCapletVolatility), never after.
    private CPIVolatilitySurface? capletVol;

    /// <summary>Initializes a new instance of the <see cref="CPICouponPricer"/> class.</summary>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public CPICouponPricer(YieldTermStructure? nominalTermStructure = null)
        : this(null, nominalTermStructure)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CPICouponPricer"/> class.</summary>
    /// <param name="capletVol">The optionlet volatility, or <see langword="null"/> for none.</param>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public CPICouponPricer(CPIVolatilitySurface? capletVol, YieldTermStructure? nominalTermStructure)
    {
        this.capletVol = capletVol;
        NominalTermStructure = nominalTermStructure;
    }

    /// <summary>Gets the optionlet volatility, or <see langword="null"/> for none.</summary>
    public virtual CPIVolatilitySurface? CapletVolatility => capletVol;

    /// <summary>Gets the nominal curve the prices discount on, or <see langword="null"/> for none.</summary>
    public virtual YieldTermStructure? NominalTermStructure { get; }

    /// <summary>A copy of the pricer with another optionlet volatility: QuantLib's
    /// <c>setCapletVolatility</c>.</summary>
    /// <param name="capletVol">The volatility.</param>
    /// <returns>The copy, of the pricer's own type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capletVol"/> is null, QuantLib's "empty capletVol
    /// handle".</exception>
    public virtual CPICouponPricer SetCapletVolatility(CPIVolatilitySurface capletVol)
    {
        if (capletVol is null)
        {
            throw new ArgumentNullException(nameof(capletVol), "empty capletVol handle");
        }

        var copy = (CPICouponPricer)MemberwiseClone();
        copy.capletVol = capletVol;
        return copy;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    /// <exception cref="NanoQLException">The pricer has no nominal curve.</exception>
    public override double SwapletPrice(InflationCoupon coupon, EvaluationContext context)
    {
        CPICoupon c = Initialize(coupon, context);
        double discount = Discount(c) ?? throw new NanoQLException("no nominal term structure provided");
        return SwapletRate(c, context) * c.AccrualPeriod() * discount;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    public override double SwapletRate(InflationCoupon coupon, EvaluationContext context) =>
        SwapletRate(Initialize(coupon, context), context);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    public override double CapletPrice(InflationCoupon coupon, double effectiveCap, EvaluationContext context)
    {
        CPICoupon c = Initialize(coupon, context);
        return c.FixedRate * OptionletPrice(c, OptionType.Call, effectiveCap, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    public override double CapletRate(InflationCoupon coupon, double effectiveCap, EvaluationContext context)
    {
        CPICoupon c = Initialize(coupon, context);
        return c.FixedRate * OptionletRate(c, OptionType.Call, effectiveCap, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    public override double FloorletPrice(InflationCoupon coupon, double effectiveFloor, EvaluationContext context)
    {
        CPICoupon c = Initialize(coupon, context);
        return c.FixedRate * OptionletPrice(c, OptionType.Put, effectiveFloor, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    public override double FloorletRate(InflationCoupon coupon, double effectiveFloor, EvaluationContext context)
    {
        CPICoupon c = Initialize(coupon, context);
        return c.FixedRate * OptionletRate(c, OptionType.Put, effectiveFloor, context);
    }

    /// <summary>The rate accrued to a settlement date: the fixed rate times the index ratio there.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="settlementDate">The settlement date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentException">The coupon isn't a CPI coupon.</exception>
    public virtual double AccruedRate(InflationCoupon coupon, Date settlementDate, EvaluationContext context)
    {
        CPICoupon c = Initialize(coupon, context);
        return c.FixedRate * c.IndexRatio(settlementDate, context);
    }

    /// <summary>An optionlet's price: its rate, accrued and discounted on the nominal curve.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="optionType">A call for a caplet, a put for a floorlet.</param>
    /// <param name="effStrike">The effective strike.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">The pricer has no nominal curve.</exception>
    protected virtual double OptionletPrice(
        CPICoupon coupon, OptionType optionType, double effStrike, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        double discount = Discount(coupon) ?? throw new NanoQLException("no nominal term structure provided");
        return OptionletRate(coupon, optionType, effStrike, context) * coupon.AccrualPeriod() * discount;
    }

    /// <summary>
    /// An optionlet's rate: its intrinsic value on the index ratio at the accrual end when the fixing date is at or
    /// before the evaluation date, else <see cref="OptionletPriceImp"/> on that ratio and the volatility's total
    /// variance.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="optionType">A call for a caplet, a put for a floorlet.</param>
    /// <param name="effStrike">The effective strike.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The optionlet isn't fixed and the pricer has no optionlet
    /// volatility.</exception>
    protected virtual double OptionletRate(
        CPICoupon coupon, OptionType optionType, double effStrike, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        Date fixingDate = coupon.FixingDate();
        if (fixingDate <= context.EvaluationDate)
        {
            // the amount is determined; QuantLib reads the index's level here, against a strike on the ratio
            double a, b;
            if (optionType == OptionType.Call)
            {
                a = coupon.IndexRatio(coupon.AccrualEndDate, context);
                b = effStrike;
            }
            else
            {
                a = effStrike;
                b = coupon.IndexRatio(coupon.AccrualEndDate, context);
            }

            double diff = a - b;
            return diff < 0.0 ? 0.0 : diff;
        }

        // not yet determined, use Black/DD1/Bachelier/whatever from Impl
        CPIVolatilitySurface vol = CapletVolatility ?? throw new NanoQLException("missing optionlet volatility");
        double stdDev = Math.Sqrt(vol.TotalVariance(fixingDate, effStrike));
        return OptionletPriceImp(optionType, effStrike, coupon.IndexRatio(coupon.AccrualEndDate, context), stdDev);
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

    // QuantLib's initialize casts without a check (cpicouponpricer.cpp:111).
    private static CPICoupon Initialize(InflationCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(coupon);
        return coupon as CPICoupon ?? throw new ArgumentException("CPI coupon needed", nameof(coupon));
    }

    // A null discount where the nominal curve is missing, QuantLib's Null<Real>(); 1 at or before the curve's reference
    // date.
    private double? Discount(CPICoupon coupon)
    {
        YieldTermStructure? curve = NominalTermStructure;
        if (curve is null)
        {
            return null;
        }

        Date paymentDate = coupon.Date;
        return paymentDate > curve.ReferenceDate ? curve.Discount(paymentDate) : 1.0;
    }

    // QuantLib's swapletRate: the accrued rate at the accrual end.
    private double SwapletRate(CPICoupon coupon, EvaluationContext context) =>
        AccruedRate(coupon, coupon.AccrualEndDate, context);
}
