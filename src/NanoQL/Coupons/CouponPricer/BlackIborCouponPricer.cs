// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/couponpricer.hpp ql/cashflows/couponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The Black-formula pricer for Ibor coupons, capped, floored or not.</summary>
/// <remarks>
/// <para>
/// A fixing on or before the evaluation date gives the caplet and floorlet their intrinsic values; a later one is
/// priced with Black's formula on a shifted lognormal volatility, or Bachelier's on a normal one. On a shifted
/// lognormal volatility a strike at or below −shift, on an adjusted fixing above −shift, which QuantLib's Black formula
/// refuses, prices a caplet at the adjusted fixing less the strike and a floorlet at 0, the formula's limits there
/// (QuantLib's check, its Black formula at −shift + 1e-12), so a capped coupon struck there pays its cap and a digital
/// coupon struck at 0 on an unshifted volatility replicates to probability 1. The timing adjustment corrects the fixing
/// of an in-arrears coupon (<see cref="BlackIborCouponPricerTimingAdjustment.Black76"/>), or of any coupon not paid at
/// the index's maturity (<see cref="BlackIborCouponPricerTimingAdjustment.BivariateLognormal"/>, with the correlation
/// given).
/// </para>
/// <para>
/// Unsealed, as in QuantLib, whose subclasses override <see cref="AdjustedFixing"/>. Every member computes the
/// coupon's values inside the call (see <see cref="IborCouponPricer"/>).
/// </para>
/// </remarks>
public class BlackIborCouponPricer : IborCouponPricer
{
    private readonly BlackIborCouponPricerTimingAdjustment timingAdjustment;
    private readonly double correlation;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackIborCouponPricer"/> class with the context's at-par switch:
    /// QuantLib's constructor without an explicit choice.
    /// </summary>
    /// <param name="context">The evaluation context, whose <see cref="EvaluationContext.UseAtParCoupons"/> is read
    /// once.</param>
    /// <param name="v">The caplet volatility, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="timingAdjustment">The timing adjustment.</param>
    /// <param name="correlation">The correlation the bivariate lognormal adjustment uses.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timingAdjustment"/> isn't a member of its
    /// type.</exception>
    public BlackIborCouponPricer(
        EvaluationContext context,
        OptionletVolatilityStructure? v = null,
        BlackIborCouponPricerTimingAdjustment timingAdjustment = BlackIborCouponPricerTimingAdjustment.Black76,
        double correlation = 1.0)
        : base(context, v)
    {
        EnumArguments.Check(timingAdjustment, nameof(timingAdjustment));
        this.timingAdjustment = timingAdjustment;
        this.correlation = correlation;
    }

    /// <summary>Initializes a new instance of the <see cref="BlackIborCouponPricer"/> class with an explicit choice.</summary>
    /// <param name="v">The caplet volatility, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="timingAdjustment">The timing adjustment.</param>
    /// <param name="correlation">The correlation the bivariate lognormal adjustment uses.</param>
    /// <param name="useIndexedCoupon">Whether the pricer prices indexed coupons, rather than at-par ones.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timingAdjustment"/> isn't a member of its
    /// type.</exception>
    public BlackIborCouponPricer(
        OptionletVolatilityStructure? v,
        BlackIborCouponPricerTimingAdjustment timingAdjustment,
        double correlation,
        bool useIndexedCoupon)
        : base(v, useIndexedCoupon)
    {
        EnumArguments.Check(timingAdjustment, nameof(timingAdjustment));
        this.timingAdjustment = timingAdjustment;
        this.correlation = correlation;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The index has no forecasting curve, or the coupon can't be priced.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BlackData data = InitializeBlack(coupon);

        // past or future fixing is managed in InterestRateIndex::fixing()
        double discount = data.Discount ?? throw new NanoQLException("no forecast curve provided");
        return SwapletRate(data, context) * data.Ibor.AccrualPeriod * discount;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The coupon can't be priced.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SwapletRate(InitializeBlack(coupon), context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The index has no forecasting curve, or the caplet can't be priced.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BlackData data = InitializeBlack(coupon);
        double discount = data.Discount ?? throw new NanoQLException("no forecast curve provided");
        return CapletRate(data, effectiveCap, context) * data.Ibor.AccrualPeriod * discount;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The caplet can't be priced.</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CapletRate(InitializeBlack(coupon), effectiveCap, context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The index has no forecasting curve, or the floorlet can't be priced.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BlackData data = InitializeBlack(coupon);
        double discount = data.Discount ?? throw new NanoQLException("no forecast curve provided");
        return FloorletRate(data, effectiveFloor, context) * data.Ibor.AccrualPeriod * discount;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't an Ibor coupon.</exception>
    /// <exception cref="NanoQLException">The floorlet can't be priced.</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return FloorletRate(InitializeBlack(coupon), effectiveFloor, context);
    }

    /// <summary>
    /// The fixing with the timing adjustment applied: unchanged for a coupon in advance under
    /// <see cref="BlackIborCouponPricerTimingAdjustment.Black76"/>, for a coupon paid at the index's maturity, and
    /// before the volatility's reference date; otherwise adjusted for the variance accumulated to the fixing date.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="fixing">The fixing to adjust; the coupon's index fixing, with the pricer's at-par choice, when
    /// <see langword="null"/>.</param>
    /// <returns>The adjusted fixing.</returns>
    /// <exception cref="NanoQLException">The adjustment needs a volatility or a forecasting curve that isn't
    /// given.</exception>
    protected virtual double AdjustedFixing(IborCoupon coupon, EvaluationContext context, double? fixing = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(coupon);
        double f = fixing ?? coupon.IndexFixing(context, UseIndexedCoupon);

        // if the pay date is equal to the index estimation end date
        // there is no convexity; in all other cases in principle an
        // adjustment has to be applied, but the Black76 method only
        // applies the standard in arrears adjustment; the bivariate
        // lognormal method is more accurate in this regard.
        if (!coupon.IsInArrears && timingAdjustment == BlackIborCouponPricerTimingAdjustment.Black76)
        {
            return f;
        }

        CachedData cached = InitializeCachedData(coupon, UseIndexedCoupon);
        Date d1 = coupon.StoredFixingDate;
        Date d2 = cached.FixingValueDate;
        Date d3 = cached.FixingMaturityDate;
        if (coupon.Date == d3)
        {
            return f;
        }

        OptionletVolatilityStructure vol = CapletVolatility ?? throw new NanoQLException("missing optionlet volatility");
        Date referenceDate = vol.ReferenceDate;

        // no variance has accumulated, so the convexity is zero
        if (d1 <= referenceDate)
        {
            return f;
        }

        double tau = cached.SpanningTimeIndexMaturity;
        double variance = vol.BlackVariance(d1, f);

        double shift = vol.Displacement;
        bool shiftedLn = vol.VolatilityType == VolatilityType.ShiftedLognormal;

        double adjustment = shiftedLn
            ? (f + shift) * (f + shift) * variance * tau / (1.0 + (f * tau))
            : variance * tau / (1.0 + (f * tau));

        if (timingAdjustment == BlackIborCouponPricerTimingAdjustment.BivariateLognormal)
        {
            // QuantLib's "no correlation given" can't arise: the correlation is a double.
            Date d4 = coupon.Date;
            Date d5 = d4 >= d3 ? d3 : d2;
            double tau2 = coupon.IborIndex.DayCounter.YearFraction(d5, d4);
            if (d4 >= d3)
            {
                adjustment = 0.0;
            }

            // if d4 < d2 (payment before index start) we just apply the
            // Black76 in arrears adjustment
            if (tau2 > 0.0)
            {
                YieldTermStructure curve = coupon.IborIndex.ForwardingTermStructure
                    ?? throw new NanoQLException("empty Handle cannot be dereferenced");
                double fixing2 = ((curve.Discount(d5) / curve.Discount(d4)) - 1.0) / tau2;
                adjustment -= shiftedLn
                    ? correlation * tau2 * variance * (f + shift) * (fixing2 + shift) / (1.0 + (fixing2 * tau2))
                    : correlation * tau2 * variance / (1.0 + (fixing2 * tau2));
            }
        }

        return f + adjustment;
    }

    private double SwapletRate(in BlackData data, EvaluationContext context) =>
        (data.Ibor.Coupon.Gearing * AdjustedFixing(data.Ibor.Coupon, context)) + data.Ibor.Coupon.Spread;

    private double CapletRate(in BlackData data, double effectiveCap, EvaluationContext context) =>
        data.Ibor.Coupon.Gearing * OptionletRate(data, OptionType.Call, effectiveCap, context);

    private double FloorletRate(in BlackData data, double effectiveFloor, EvaluationContext context) =>
        data.Ibor.Coupon.Gearing * OptionletRate(data, OptionType.Put, effectiveFloor, context);

    private double OptionletRate(in BlackData data, OptionType optionType, double effStrike, EvaluationContext context)
    {
        IborCoupon coupon = data.Ibor.Coupon;
        Date fixingDate = coupon.StoredFixingDate;
        if (fixingDate <= context.EvaluationDate)
        {
            // the amount is determined
            double a, b;
            if (optionType == OptionType.Call)
            {
                a = coupon.IndexFixing(context, UseIndexedCoupon);
                b = effStrike;
            }
            else
            {
                a = effStrike;
                b = coupon.IndexFixing(context, UseIndexedCoupon);
            }

            // std::max(a - b, 0.0)
            double x = a - b;
            return x < 0.0 ? 0.0 : x;
        }
        else
        {
            // not yet determined, use Black model
            OptionletVolatilityStructure vol = CapletVolatility
                ?? throw new NanoQLException("missing optionlet volatility");
            double shift = vol.Displacement;
            bool shiftedLn = vol.VolatilityType == VolatilityType.ShiftedLognormal;
            if (shiftedLn && effStrike <= -shift)
            {
                // where Black's formula refuses the strike, the caplet is the forward less the strike and the
                // floorlet 0, the formula's limits there; on a forward at or below −shift the model has no rate
                // distribution, and the formula's refusal stands
                double forward = AdjustedFixing(coupon, context);
                if (forward > -shift)
                {
                    return optionType == OptionType.Call ? forward - effStrike : 0.0;
                }
            }

            double stdDev = Math.Sqrt(vol.BlackVariance(fixingDate, effStrike));
            double fixing = shiftedLn
                ? BlackFormulaFunctions.BlackFormula(
                    optionType, effStrike, AdjustedFixing(coupon, context), stdDev, 1.0, shift)
                : BlackFormulaFunctions.BachelierBlackFormula(
                    optionType, effStrike, AdjustedFixing(coupon, context), stdDev, 1.0);
            return fixing;
        }
    }

    // QuantLib's initialize(coupon): the Ibor pricer's values and the discount to the payment date, null without a
    // forecasting curve ("might not be needed, will be checked later").
    private BlackData InitializeBlack(FloatingRateCoupon coupon)
    {
        CouponData data = Initialize(coupon);
        YieldTermStructure? rateCurve = data.Coupon.IborIndex.ForwardingTermStructure;
        double? discount = null;
        if (rateCurve is not null)
        {
            Date paymentDate = data.Coupon.Date;
            discount = paymentDate > rateCurve.ReferenceDate ? rateCurve.Discount(paymentDate) : 1.0;
        }

        return new BlackData(data, discount);
    }

    private readonly record struct BlackData(CouponData Ibor, double? Discount);
}
