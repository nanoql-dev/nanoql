// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2016 Stefano Fondi
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/overnightindexedcouponpricer.hpp ql/cashflows/overnightindexedcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The base pricer for coupons indexed to an overnight rate, compounded or averaged, capped or floored.</summary>
/// <remarks>
/// <para>
/// A pricer is a stateless function of the coupon and the context (<see cref="FloatingRateCouponPricer"/>): QuantLib's
/// <c>initialize(coupon)</c>, which keeps the coupon (a capped coupon's underlying) in the pricer, and the values
/// QuantLib keeps after a query are computed inside each call from its arguments. A member taking a coupon accepts an
/// <see cref="OvernightIndexedCoupon"/> or a <see cref="CappedFlooredOvernightIndexedCoupon"/>, whose underlying it
/// prices.
/// </para>
/// <para>
/// The caplet volatility and the effective-volatility flag are the pricer's configuration;
/// <see cref="OvernightIndexedCouponPricerExtensions.SetCapletVolatility"/> and
/// <see cref="OvernightIndexedCouponPricerExtensions.SetEffectiveVolatilityInput"/> return modified copies.
/// </para>
/// </remarks>
public abstract class OvernightIndexedCouponPricer : FloatingRateCouponPricer
{
    // Written only by the constructor and on a fresh copy.
    private OptionletVolatilityStructure? capletVol;
    private bool effectiveVolatilityInput;

    /// <summary>Initializes a new instance of the <see cref="OvernightIndexedCouponPricer"/> class.</summary>
    /// <param name="v">The optionlet volatility for caplets and floorlets, or <see langword="null"/> for none.</param>
    /// <param name="effectiveVolatilityInput">Whether the volatility is the effective volatility of the period's rate.</param>
    protected OvernightIndexedCouponPricer(OptionletVolatilityStructure? v = null, bool effectiveVolatilityInput = false)
    {
        capletVol = v;
        this.effectiveVolatilityInput = effectiveVolatilityInput;
    }

    /// <summary>Gets the optionlet volatility used for caplets and floorlets, or <see langword="null"/> for none.</summary>
    public OptionletVolatilityStructure? CapletVolatility => capletVol;

    /// <summary>Gets a value indicating whether the volatility input is the effective volatility.</summary>
    public bool EffectiveVolatilityInput => effectiveVolatilityInput;

    /// <summary>
    /// The effective caplet volatility of the caplet <see cref="CapletRate(FloatingRateCoupon, double, bool, EvaluationContext)"/>
    /// prices: QuantLib's <c>effectiveCapletVolatility()</c> after that <c>capletRate</c>, which this member computes.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="dailyCapFloor">Whether the cap applies to the daily rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The volatility, or <see langword="null"/> where the caplet uses none; this base prices the caplet, so
    /// that it throws where the caplet does, and reports none.</returns>
    public virtual double? EffectiveCapletVolatility(
        FloatingRateCoupon coupon, double effectiveCap, bool dailyCapFloor, EvaluationContext context)
    {
        _ = CapletRate(coupon, effectiveCap, dailyCapFloor, context);
        return null;
    }

    /// <summary>
    /// The effective floorlet volatility of the floorlet <see cref="FloorletRate(FloatingRateCoupon, double, bool, EvaluationContext)"/>
    /// prices: QuantLib's <c>effectiveFloorletVolatility()</c> after that <c>floorletRate</c>, which this member computes.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="dailyCapFloor">Whether the floor applies to the daily rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The volatility, or <see langword="null"/> where the floorlet uses none; this base prices the floorlet,
    /// so that it throws where the floorlet does, and reports none.</returns>
    public virtual double? EffectiveFloorletVolatility(
        FloatingRateCoupon coupon, double effectiveFloor, bool dailyCapFloor, EvaluationContext context)
    {
        _ = FloorletRate(coupon, effectiveFloor, dailyCapFloor, context);
        return null;
    }

    /// <summary>The rate of a caplet on the coupon's period rate, or on its daily rates.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="dailyCapFloor">Whether the cap applies to the daily rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The caplet's rate.</returns>
    public abstract double CapletRate(
        FloatingRateCoupon coupon, double effectiveCap, bool dailyCapFloor, EvaluationContext context);

    /// <summary>The rate of a floorlet on the coupon's period rate, or on its daily rates.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="dailyCapFloor">Whether the floor applies to the daily rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The floorlet's rate.</returns>
    public abstract double FloorletRate(
        FloatingRateCoupon coupon, double effectiveFloor, bool dailyCapFloor, EvaluationContext context);

    /// <summary>The coupon's rate accrued up to a date, which its accrued amount uses.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="date">The date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double AverageRate(FloatingRateCoupon coupon, Date date, EvaluationContext context);

    /// <summary>
    /// The overnight-indexed coupon a member prices, as QuantLib's <c>initialize</c> finds it: the coupon itself, or a
    /// capped or floored coupon's underlying.
    /// </summary>
    /// <param name="coupon">The coupon given.</param>
    /// <returns>The coupon priced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="coupon"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The coupon is of another type.</exception>
    protected static OvernightIndexedCoupon CouponOf(FloatingRateCoupon coupon) => coupon switch
    {
        null => throw new ArgumentNullException(nameof(coupon)),
        CappedFlooredOvernightIndexedCoupon cfCoupon => cfCoupon.Underlying,
        OvernightIndexedCoupon onCoupon => onCoupon,
        _ => throw new ArgumentException("OvernightIndexedCouponPricer: unsupported coupon type", nameof(coupon)),
    };

    /// <summary>The number of fixings up to a date: the first interest date at or after it, before the last.</summary>
    /// <param name="interestDates">The coupon's interest dates.</param>
    /// <param name="date">The date.</param>
    /// <returns>The count.</returns>
    internal static int DetermineNumberOfFixings(ImmutableArray<Date> interestDates, Date date)
    {
        // std::lower_bound over all but the last interest date.
        int lo = 0, hi = interestDates.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (interestDates[mid] < date)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>A copy holding another caplet volatility: QuantLib's <c>setCapletVolatility</c>.</summary>
    /// <param name="v">The volatility, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    internal OvernightIndexedCouponPricer WithCapletVolatility(OptionletVolatilityStructure? v)
    {
        var copy = (OvernightIndexedCouponPricer)MemberwiseClone();
        copy.capletVol = v;
        return copy;
    }

    /// <summary>A copy holding another effective-volatility flag: QuantLib's <c>setEffectiveVolatilityInput</c>.</summary>
    /// <param name="effectiveVolatilityInput">The flag.</param>
    /// <returns>The copy.</returns>
    internal OvernightIndexedCouponPricer WithEffectiveVolatilityInput(bool effectiveVolatilityInput)
    {
        var copy = (OvernightIndexedCouponPricer)MemberwiseClone();
        copy.effectiveVolatilityInput = effectiveVolatilityInput;
        return copy;
    }
}
