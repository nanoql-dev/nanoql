// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/overnightindexedcoupon.hpp ql/cashflows/overnightindexedcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A capped or floored overnight-indexed coupon: the cap and the floor apply to the coupon's effective period rate (the
/// default) or to its daily rates.
/// </summary>
/// <remarks>
/// The rate is the underlying's rate plus the floorlet less the caplet, which the coupon's own pricer, an
/// <see cref="OvernightIndexedCouponPricer"/>, computes. <see cref="FloatingRateCouponExtensions.SetPricer"/> sets the
/// coupon's own pricer only, as QuantLib's <c>setPricer</c> does; <see cref="CouponPricerFunctions.SetCouponPricer"/>
/// sets it on the underlying too. Without daily caps and floors, a coupon of non-positive gearing stores its cap as its
/// floor and its floor as its cap, as QuantLib's does; <see cref="Cap"/> and <see cref="Floor"/> read them back.
/// </remarks>
public sealed class CappedFlooredOvernightIndexedCoupon : FloatingRateCoupon
{
    private readonly double? cap;
    private readonly double? floor;

    // Written only by the constructor and on a fresh copy (WithPricers).
    private OvernightIndexedCoupon underlying;

    /// <summary>Initializes a new instance of the <see cref="CappedFlooredOvernightIndexedCoupon"/> class.</summary>
    /// <param name="underlying">The underlying coupon, whose terms and contract nominal the coupon copies.</param>
    /// <param name="cap">The cap, or <see langword="null"/> for none.</param>
    /// <param name="floor">The floor, or <see langword="null"/> for none.</param>
    /// <param name="nakedOption">Whether the coupon pays the options alone, without the underlying's rate.</param>
    /// <param name="dailyCapFloor">Whether the cap and the floor apply to the daily rates.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The underlying compounds its spread daily at a gearing other than 1, or
    /// the cap is below the floor.</exception>
    public CappedFlooredOvernightIndexedCoupon(
        OvernightIndexedCoupon underlying,
        double? cap = null,
        double? floor = null,
        bool nakedOption = false,
        bool dailyCapFloor = false)
        : base(
            (underlying ?? throw new ArgumentNullException(nameof(underlying))).Date,
            underlying.ContractNominal,
            underlying.AccrualStartDate,
            underlying.AccrualEndDate,
            underlying.FixingDays,
            underlying.Index,
            underlying.Gearing,
            underlying.Spread,
            underlying.ReferencePeriodStart,
            underlying.ReferencePeriodEnd,
            underlying.DayCounter,
            false)
    {
        this.underlying = underlying;
        NakedOption = nakedOption;
        DailyCapFloor = dailyCapFloor;

        if (!(!underlying.CompoundSpreadDaily || ComparisonFunctions.CloseEnough(underlying.Gearing, 1.0)))
        {
            throw new ArgumentException(
                "CappedFlooredOvernightIndexedCoupon: if include spread = true, only a gearing 1.0 is allowed - scale "
                + "the notional in this case instead.",
                nameof(underlying));
        }

        if (!dailyCapFloor && !(Gearing > 0.0))
        {
            this.cap = floor;
            this.floor = cap;
        }
        else
        {
            this.cap = cap;
            this.floor = floor;
        }

        // QuantLib compares the stored cap with the floor argument, so at a negative gearing without daily caps and
        // floors the check compares the floor with itself and never fails.
        if (this.cap is double storedCap && this.floor is double storedFloor && !(storedCap >= floor!.Value))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"cap level ({storedCap}) less than floor level ({storedFloor})"),
                nameof(cap));
        }
    }

    /// <summary>Gets the underlying coupon.</summary>
    public OvernightIndexedCoupon Underlying => underlying;

    /// <summary>Gets a value indicating whether the coupon pays the options alone.</summary>
    public bool NakedOption { get; }

    /// <summary>Gets a value indicating whether the cap and the floor apply to the daily rates.</summary>
    public bool DailyCapFloor { get; }

    /// <summary>Gets a value indicating whether the underlying compounds its spread daily.</summary>
    public bool CompoundSpreadDaily => underlying.CompoundSpreadDaily;

    /// <summary>Gets the underlying's averaging method.</summary>
    public RateAveragingType AveragingMethod => underlying.AveragingMethod;

    /// <summary>Gets a value indicating whether a cap is stored.</summary>
    public bool IsCapped => cap is not null;

    /// <summary>Gets a value indicating whether a floor is stored.</summary>
    public bool IsFloored => floor is not null;

    /// <summary>Gets the cap, or <see langword="null"/> for none: the stored floor at a non-positive gearing.</summary>
    public double? Cap => Gearing > 0.0 ? cap : floor;

    /// <summary>Gets the floor, or <see langword="null"/> for none: the stored cap at a non-positive gearing.</summary>
    public double? Floor => Gearing > 0.0 ? floor : cap;

    /// <summary>The effective cap of the fixing: the strike the pricer's caplet takes.</summary>
    /// <param name="context">The evaluation context, which the underlying's effective spread reads.</param>
    /// <returns>The effective cap, or <see langword="null"/> without a cap.</returns>
    public double? EffectiveCap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cap is not double c)
        {
            return null;
        }

        /* We have four cases dependent on dailyCapFloor_ and compoundSpreadDaily. Notation in the formulas:
        g         gearing,
        s         spread,
        A         coupon amount,
        f_i       daily fixings,
        \tau_i    daily accrual fractions,
        \tau      coupon accrual fraction,
        C         cap rate
        F         floor rate
        */
        if (DailyCapFloor)
        {
            if (underlying.CompoundSpreadDaily)
            {
                // A = g \cdot \frac{\prod (1 + \tau_i \min ( \max ( f_i + s , F), C)) - 1}{\tau}
                return c - underlying.Spread;
            }
            else
            {
                // A = g \cdot \frac{\prod (1 + \tau_i \min ( \max ( f_i , F), C)) - 1}{\tau} + s
                return c;
            }
        }
        else
        {
            if (underlying.CompoundSpreadDaily)
            {
                // A = \min \left( \max \left( g \cdot \frac{\prod (1 + \tau_i(f_i + s)) - 1}{\tau}, F \right), C \right)
                return (c / Gearing) - underlying.EffectiveSpread(context);
            }
            else
            {
                // A = \min \left( \max \left( g \cdot \frac{\prod (1 + \tau_i f_i) - 1}{\tau} + s, F \right), C \right)
                return (c - underlying.EffectiveSpread(context)) / Gearing;
            }
        }
    }

    /// <summary>The effective floor of the fixing: the strike the pricer's floorlet takes.</summary>
    /// <param name="context">The evaluation context, which the underlying's effective spread reads.</param>
    /// <returns>The effective floor, or <see langword="null"/> without a floor.</returns>
    public double? EffectiveFloor(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (floor is not double f)
        {
            return null;
        }

        if (DailyCapFloor)
        {
            if (underlying.CompoundSpreadDaily)
            {
                return f - underlying.Spread;
            }
            else
            {
                return f;
            }
        }
        else
        {
            if (underlying.CompoundSpreadDaily)
            {
                return f - underlying.EffectiveSpread(context);
            }
            else
            {
                return (f - underlying.EffectiveSpread(context)) / Gearing;
            }
        }
    }

    /// <summary>The rate: the underlying's rate (none for a naked option), plus the floorlet, less the caplet.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The underlying has no pricer, or the coupon's pricer isn't an
    /// <see cref="OvernightIndexedCouponPricer"/>.</exception>
    public override double Rate(EvaluationContext context) => Calculate(context).Rate;

    /// <summary>The underlying's convexity adjustment.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjustment.</returns>
    public override double ConvexityAdjustment(EvaluationContext context) => underlying.ConvexityAdjustment(context);

    /// <summary>The underlying's fixing date.</summary>
    /// <returns>The date.</returns>
    public override Date FixingDate() => underlying.FixingDate();

    /// <summary>
    /// The effective caplet volatility the pricer uses for the coupon's caplet, which QuantLib keeps after computing
    /// the rate.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The volatility, or <see langword="null"/> where the pricer uses none (no cap, or a determined
    /// rate).</returns>
    public double? EffectiveCapletVolatility(EvaluationContext context)
    {
        (_, OvernightIndexedCouponPricer pricer) = Calculate(context);
        return EffectiveCap(context) is double effectiveCap
            ? pricer.EffectiveCapletVolatility(this, effectiveCap, DailyCapFloor, context)
            : null;
    }

    /// <summary>
    /// The effective floorlet volatility the pricer uses for the coupon's floorlet, which QuantLib keeps after
    /// computing the rate.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The volatility, or <see langword="null"/> where the pricer uses none (no floor, or a determined
    /// rate).</returns>
    public double? EffectiveFloorletVolatility(EvaluationContext context)
    {
        (_, OvernightIndexedCouponPricer pricer) = Calculate(context);
        return EffectiveFloor(context) is double effectiveFloor
            ? pricer.EffectiveFloorletVolatility(this, effectiveFloor, DailyCapFloor, context)
            : null;
    }

    /// <summary>
    /// A copy holding another pricer, which must be an <see cref="OvernightIndexedCouponPricer"/>; the underlying keeps
    /// its own, as QuantLib's <c>setPricer</c> leaves it.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">The pricer isn't an <see cref="OvernightIndexedCouponPricer"/>.</exception>
    protected internal override FloatingRateCoupon WithPricer(FloatingRateCouponPricer? pricer)
    {
        const string Text = "The pricer is required to be an instance of OvernightIndexedCouponPricer";
        if (pricer is null)
        {
            throw new ArgumentNullException(nameof(pricer), Text);
        }

        if (pricer is not OvernightIndexedCouponPricer)
        {
            throw new ArgumentException(Text, nameof(pricer));
        }

        return base.WithPricer(pricer);
    }

    /// <summary>
    /// A copy holding the pricer and the given underlying: <see cref="CouponPricerFunctions"/>' step, which sets the
    /// coupon's pricer and then its underlying's, as QuantLib's visitor visits both. The copy is memberwise, so the
    /// stored cap and floor are kept as they are.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="underlying">The underlying, already holding its pricer.</param>
    /// <returns>The copy.</returns>
    internal CappedFlooredOvernightIndexedCoupon WithPricers(
        OvernightIndexedCouponPricer pricer,
        OvernightIndexedCoupon underlying)
    {
        var copy = (CappedFlooredOvernightIndexedCoupon)WithPricer(pricer);
        copy.underlying = underlying;
        return copy;
    }

    // QuantLib's performCalculations (overnightindexedcoupon.cpp:313-331), on each call.
    private (double Rate, OvernightIndexedCouponPricer Pricer) Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (underlying.Pricer is null)
        {
            throw new NanoQLException("underlying coupon pricer not set");
        }

        double swapletRate = NakedOption ? 0.0 : underlying.Rate(context);
        var cfONPricer = Pricer as OvernightIndexedCouponPricer
            ?? throw new NanoQLException("coupon pricer not an instance of OvernightIndexedCouponPricer");

        double floorletRate = 0.0;
        if (floor is not null)
        {
            floorletRate = cfONPricer.FloorletRate(this, EffectiveFloor(context)!.Value, DailyCapFloor, context);
        }

        double capletRate = 0.0;
        if (cap is not null)
        {
            capletRate = (NakedOption && floor is null ? -1.0 : 1.0)
                * cfONPricer.CapletRate(this, EffectiveCap(context)!.Value, DailyCapFloor, context);
        }

        return (swapletRate + floorletRate - capletRate, cfONPricer);
    }
}
