// SPDX-FileCopyrightText: Copyright (C) 2020 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/blackovernightindexedcouponpricer.hpp ql/cashflows/blackovernightindexedcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The Black pricer of capped and floored averaged overnight-indexed coupons.</summary>
/// <remarks>
/// <para>
/// The methods that are implemented here to price capped / floored average ON coupons are highly experimental and
/// ad-hoc. As soon as a market best practice has evolved, the pricer should be revised.
/// </para>
/// <para>
/// It prices averaged coupons only, refusing a compounded one, and averages exactly (with no approximation and no
/// convexity adjustment). Each member computes, inside the call, what QuantLib's <c>initialize</c> keeps: the coupon's
/// swaplet rate and its forward rate.
/// </para>
/// </remarks>
public sealed class BlackAveragingOvernightIndexedCouponPricer : ArithmeticAveragedOvernightIndexedCouponPricer
{
    /// <summary>Initializes a new instance of the <see cref="BlackAveragingOvernightIndexedCouponPricer"/> class.</summary>
    /// <param name="v">The optionlet volatility, or <see langword="null"/> for none.</param>
    /// <param name="effectiveVolatilityInput">Whether the volatility is the effective volatility of the period's rate.</param>
    public BlackAveragingOvernightIndexedCouponPricer(
        OptionletVolatilityStructure? v = null, bool effectiveVolatilityInput = false)
        : base(0.03, 0.0, false, v, effectiveVolatilityInput)
    {
    }

    /// <summary>Not provided: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        throw NotProvided("swapletPrice", coupon, context);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon is compounded.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) =>
        Initialize(coupon, context).SwapletRate;

    /// <summary>Not provided: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw NotProvided("capletPrice", coupon, context);

    /// <summary>The rate of a caplet on the coupon's period rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The caplet's rate.</returns>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletRate(coupon, effectiveCap, false, context);

    /// <summary>Not provided: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw NotProvided("floorletPrice", coupon, context);

    /// <summary>The rate of a floorlet on the coupon's period rate.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The floorlet's rate.</returns>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletRate(coupon, effectiveFloor, false, context);

    /// <inheritdoc/>
    public override double CapletRate(
        FloatingRateCoupon coupon, double effectiveCap, bool dailyCapFloor, EvaluationContext context) =>
        OptionletRate(coupon, OptionType.Call, effectiveCap, dailyCapFloor, context).Rate;

    /// <inheritdoc/>
    public override double FloorletRate(
        FloatingRateCoupon coupon, double effectiveFloor, bool dailyCapFloor, EvaluationContext context) =>
        OptionletRate(coupon, OptionType.Put, effectiveFloor, dailyCapFloor, context).Rate;

    /// <inheritdoc/>
    public override double? EffectiveCapletVolatility(
        FloatingRateCoupon coupon, double effectiveCap, bool dailyCapFloor, EvaluationContext context) =>
        OptionletRate(coupon, OptionType.Call, effectiveCap, dailyCapFloor, context).Volatility;

    /// <inheritdoc/>
    public override double? EffectiveFloorletVolatility(
        FloatingRateCoupon coupon, double effectiveFloor, bool dailyCapFloor, EvaluationContext context) =>
        OptionletRate(coupon, OptionType.Put, effectiveFloor, dailyCapFloor, context).Volatility;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon is compounded.</exception>
    public override double AverageRate(FloatingRateCoupon coupon, Date date, EvaluationContext context)
    {
        // QuantLib's coupon initializes the pricer, which checks the averaging and computes the swaplet rate, before it
        // asks for the rate.
        _ = Initialize(coupon, context);
        return base.AverageRate(coupon, date, context);
    }

    private static NanoQLException NotProvided(string member, FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        return new NanoQLException($"BlackAveragingOvernightIndexedCouponPricer::{member}() not provided");
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;

    private static Date Max(Date a, Date b) => a < b ? b : a;

    // QuantLib's initialize(coupon): the coupon priced (a capped coupon's underlying), refused unless averaged; the given
    // coupon's gearing; the swaplet rate of the exact average, and the forward rate it implies.
    private Initialized Initialize(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OvernightIndexedCoupon c = CouponOf(coupon);

        if (c.AveragingMethod == RateAveragingType.Compound)
        {
            throw new ArgumentException(
                "Averaging method required to be simple for BlackAveragingOvernightIndexedCouponPricer", nameof(coupon));
        }

        double swapletRate = base.AverageRate(c, c.AccrualEndDate, context);
        double forwardRate = (swapletRate - c.Spread) / c.Gearing;
        return new Initialized(c, coupon.Gearing, swapletRate, forwardRate);
    }

    private (double Rate, double? Volatility) OptionletRate(
        FloatingRateCoupon coupon, OptionType optionType, double effStrike, bool dailyCapFloor, EvaluationContext context)
    {
        Initialized s = Initialize(coupon, context);
        return dailyCapFloor
            ? OptionletRateLocal(s, optionType, effStrike, context)
            : OptionletRateGlobal(s, optionType, effStrike, context);
    }

    private (double Rate, double? Volatility) OptionletRateGlobal(
        Initialized s, OptionType optionType, double effStrike, EvaluationContext context)
    {
        Date lastRelevantFixingDate = s.Coupon.FixingDate();
        if (lastRelevantFixingDate <= context.EvaluationDate)
        {
            // the amount is determined
            double a, b;
            if (optionType == OptionType.Call)
            {
                a = s.ForwardRate;
                b = effStrike;
            }
            else
            {
                a = effStrike;
                b = s.ForwardRate;
            }

            return (s.Gearing * Max(a - b, 0.0), null);
        }
        else
        {
            // not yet determined, use Black model. QuantLib's check for empty fixing dates can't fail here: a coupon
            // has at least one.
            OptionletVolatilityStructure vol = CapletVolatility
                ?? throw new NanoQLException("BlackAveragingOvernightIndexedCouponPricer: missing optionlet volatility");
            ImmutableArray<Date> fixingDates = s.Coupon.FixingDates();
            bool shiftedLn = vol.VolatilityType == VolatilityType.ShiftedLognormal;
            double shift = vol.Displacement;
            double stdDev;
            double effectiveTime = vol.TimeFromReference(fixingDates[^1]);
            if (EffectiveVolatilityInput)
            {
                // vol input is effective, i.e. we use a plain black model
                stdDev = vol.Volatility(fixingDates[^1], effStrike) * Math.Sqrt(effectiveTime);
            }
            else
            {
                // vol input is not effective:
                // for the standard deviation see Lyashenko, Mercurio, Looking forward to backward looking rates,
                // section 6.3. the idea is to dampen the average volatility sigma between the fixing start and fixing end
                // date by a linear function going from (fixing start, 1) to (fixing end, 0)
                double fixingStartTime = vol.TimeFromReference(fixingDates[0]);
                double fixingEndTime = vol.TimeFromReference(fixingDates[^1]);
                double sigma = vol.Volatility(Max(fixingDates[0], vol.ReferenceDate + 1), effStrike);
                double t = Max(fixingStartTime, 0.0);
                if (!ComparisonFunctions.CloseEnough(fixingEndTime, t))
                {
                    t += Math.Pow(fixingEndTime - t, 3.0) / Math.Pow(fixingEndTime - fixingStartTime, 2.0) / 3.0;
                }

                stdDev = sigma * Math.Sqrt(t);
            }

            double effectiveVolatility = stdDev / Math.Sqrt(effectiveTime);
            double fixing = shiftedLn
                ? BlackFormulaFunctions.BlackFormula(optionType, effStrike, s.ForwardRate, stdDev, 1.0, shift)
                : BlackFormulaFunctions.BachelierBlackFormula(optionType, effStrike, s.ForwardRate, stdDev, 1.0);
            return (s.Gearing * fixing, effectiveVolatility);
        }
    }

    private (double Rate, double? Volatility) OptionletRateLocal(
        Initialized s, OptionType optionType, double effStrike, EvaluationContext context)
    {
        if (EffectiveVolatilityInput)
        {
            throw new NanoQLException(
                "BlackAveragingOvernightIndexedCouponPricer::optionletRateLocal() does not support effective volatility input.");
        }

        // We compute a rate and a rawRate such that
        // rate * tau * nominal is the amount of the coupon with daily capped / floored rates
        // rawRate * tau * nominal is the amount of the coupon without capping / flooring the rate
        // We will then return the difference between rate and rawRate (with the correct sign, see below)
        // as the option component of the coupon.

        // See CappedFlooredOvernightIndexedCoupon::effectiveCap(), Floor() for what is passed in as effStrike.
        // From this we back out the absolute strike at which the
        // - daily rate + spread (spread included) or the
        // - daily rate (spread excluded)
        // is capped / floored.
        OvernightIndexedCoupon coupon = s.Coupon;
        double absStrike = coupon.CompoundSpreadDaily ? effStrike + coupon.Spread : effStrike;

        var index = (OvernightIndex)coupon.Index;

        ImmutableArray<Date> fixingDates = coupon.FixingDates();
        ImmutableArray<double> dt = coupon.Dt();

        int n = dt.Length;
        int i = 0;

        // QuantLib's check that the rate cutoff is below the number of fixings can't fail here: an averaged coupon has
        // no lockout.
        int nCutoff = n - coupon.LockoutDays;

        double accumulatedRate = 0.0, accumulatedRateRaw = 0.0;

        // already fixed part
        Date today = context.EvaluationDate;
        while (i < n && fixingDates[Math.Min(i, nCutoff)] < today)
        {
            // rate must have been fixed
            Date fixingDate = fixingDates[Math.Min(i, nCutoff)];
            double pastFixing = index.PastFixing(fixingDate, context)
                ?? throw new NanoQLException($"Missing {index.Name} fixing for {fixingDate}");
            if (coupon.CompoundSpreadDaily)
            {
                pastFixing += coupon.Spread;
            }

            accumulatedRate += BlackCompoundingOvernightIndexedCouponPricer.CappedFlooredRate(pastFixing, optionType, absStrike) * dt[i];
            accumulatedRateRaw += pastFixing * dt[i];
            ++i;
        }

        // today is a border case. QuantLib's try block guards a read of today, a fixing date of the coupon, which is
        // valid, so it can't throw.
        if (i < n && fixingDates[Math.Min(i, nCutoff)] == today && index.PastFixing(today, context) is double todays)
        {
            // might have been fixed
            double pastFixing = todays;
            if (coupon.CompoundSpreadDaily)
            {
                pastFixing += coupon.Spread;
            }

            accumulatedRate += BlackCompoundingOvernightIndexedCouponPricer.CappedFlooredRate(pastFixing, optionType, absStrike) * dt[i];
            accumulatedRateRaw += pastFixing * dt[i];
            ++i;
        }

        // forward part, approximation by pricing a cap / floor in the middle of the future period
        ImmutableArray<Date> dates = coupon.ValueDates();
        double? effectiveVolatility = null;
        if (i < n)
        {
            YieldTermStructure curve = index.ForwardingTermStructure
                ?? throw new NanoQLException($"null term structure set to this instance of {index.Name}");

            double startDiscount = curve.Discount(dates[i]);
            double endDiscount = curve.Discount(dates[Math.Max(nCutoff, i)]);

            // handle the rate cutoff period (if there is any, i.e. if nCutoff < n)
            if (nCutoff < n)
            {
                // forward discount factor for one calendar day on the cutoff date
                double discountCutoffDate = curve.Discount(dates[nCutoff] + 1) / curve.Discount(dates[nCutoff]);

                // keep the above forward discount factor constant during the cutoff period
                endDiscount *= Math.Pow(discountCutoffDate, dates[n] - dates[nCutoff]);
            }

            // estimate the average daily rate over the future period (approximate the continuously compounded rate)
            double tau = coupon.DayCounter.YearFraction(dates[i], dates[^1]);
            double averageRate = -Math.Log(endDiscount / startDiscount) / tau;

            // compute the value of a cap or floor with fixing in the middle of the future period
            // (but accounting for the rate cutoff here). QuantLib dereferences the volatility's handle, which throws
            // when it is empty.
            OptionletVolatilityStructure vol = CapletVolatility
                ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            double midPoint = (vol.TimeFromReference(dates[i]) + vol.TimeFromReference(dates[nCutoff])) / 2.0;
            double stdDev = vol.Volatility(midPoint, effStrike) * Math.Sqrt(midPoint);
            double shift = vol.Displacement;
            bool shiftedLn = vol.VolatilityType == VolatilityType.ShiftedLognormal;
            double cfValue = shiftedLn
                ? BlackFormulaFunctions.BlackFormula(optionType, effStrike, averageRate, stdDev, 1.0, shift)
                : BlackFormulaFunctions.BachelierBlackFormula(optionType, effStrike, averageRate, stdDev, 1.0);

            double effectiveTime = vol.TimeFromReference(fixingDates[^1]);
            effectiveVolatility = stdDev / Math.Sqrt(effectiveTime);

            // add spread to average rate
            if (coupon.CompoundSpreadDaily)
            {
                averageRate += coupon.Spread;
            }

            // incorporate cap/floor into average rate
            double averageRateRaw = averageRate;
            averageRate += optionType == OptionType.Call ? -cfValue : cfValue;

            // now assume the averageRate is the effective rate over the future period and update the average rate
            // this is an approximation, see "Ester / Daily Spread Curve Setup in ORE": set tau to avg value
            double dailyTau = coupon.DayCounter.YearFraction(dates[i], dates[^1]) / (dates[^1] - dates[i]);
            accumulatedRate += dailyTau * averageRate * (dates[^1] - dates[i]);
            accumulatedRateRaw += dailyTau * averageRateRaw * (dates[^1] - dates[i]);
        }

        // D18: the fixing days choose the period here, where the compounding pricer chooses on the lockout.
        double t = coupon.FixingDays == 0
            ? coupon.AccrualPeriod()
            : coupon.DayCounter.YearFraction(dates[0], dates[^1]);
        double rate = accumulatedRate / t;
        double rawRate = accumulatedRateRaw / t;

        rate *= coupon.Gearing;
        rawRate *= coupon.Gearing;

        if (!coupon.CompoundSpreadDaily)
        {
            rate += coupon.Spread;
            rawRate += coupon.Spread;
        }

        // return optionletRate := r - rawRate, i.e. the option component only
        // (see CappedFlooredAverageONIndexedCoupon::rate() for the signs of the capletRate / flooredRate)
        return ((optionType == OptionType.Call ? -1.0 : 1.0) * (rate - rawRate), effectiveVolatility);
    }

    private sealed record Initialized(OvernightIndexedCoupon Coupon, double Gearing, double SwapletRate, double ForwardRate);
}
