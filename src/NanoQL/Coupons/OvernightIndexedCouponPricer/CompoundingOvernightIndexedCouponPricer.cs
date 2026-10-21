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
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The pricer for compounded overnight-indexed coupons.</summary>
/// <remarks>
/// Unsealed, as in QuantLib, where the Black compounding pricer derives from it. Past fixings come from the context's
/// store; the forward part is forecast on the index's curve, telescoped through one discount ratio wherever the
/// coupon's <see cref="OvernightIndexedCoupon.CanApplyTelescopicFormula"/> holds. A nonzero spread compounded daily
/// is compounded day by day over that range, on the forecast fixings, as QuantLib compounds it outside the range and
/// once the fixings are published; QuantLib multiplies both compound factors by the discount ratio, which drops the
/// spread (a documented divergence; the S-rx plan's C3 review, B1-1, ruled under R8).
/// </remarks>
public class CompoundingOvernightIndexedCouponPricer : OvernightIndexedCouponPricer
{
    /// <summary>Initializes a new instance of the <see cref="CompoundingOvernightIndexedCouponPricer"/> class.</summary>
    /// <param name="v">The optionlet volatility for caplets and floorlets, or <see langword="null"/> for none.</param>
    /// <param name="effectiveVolatilityInput">Whether the volatility is the effective volatility of the period's rate.</param>
    public CompoundingOvernightIndexedCouponPricer(
        OptionletVolatilityStructure? v = null, bool effectiveVolatilityInput = false)
        : base(v, effectiveVolatilityInput)
    {
    }

    /// <inheritdoc/>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OvernightIndexedCoupon c = CouponOf(coupon);
        return Compute(c, c.AccrualEndDate, context).SwapletRate;
    }

    /// <summary>Not available: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        throw NotAvailable("swapletPrice", coupon, context);

    /// <summary>Not available: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw NotAvailable("capletPrice", coupon, context);

    /// <summary>Not available: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw NotAvailable("capletRate", coupon, context);

    /// <summary>Not available: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw NotAvailable("floorletPrice", coupon, context);

    /// <summary>Not available: QuantLib's pricer throws.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw NotAvailable("floorletRate", coupon, context);

    /// <summary>Not implemented: QuantLib's pricer throws; the Black compounding pricer prices caplets.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveCap">The effective cap.</param>
    /// <param name="dailyCapFloor">Whether the cap applies to the daily rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double CapletRate(
        FloatingRateCoupon coupon, double effectiveCap, bool dailyCapFloor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        throw new NanoQLException("CompoundingOvernightIndexedCouponPricer::capletRate(Rate, bool) not implemented");
    }

    /// <summary>Not implemented: QuantLib's pricer throws; the Black compounding pricer prices floorlets.</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="effectiveFloor">The effective floor.</param>
    /// <param name="dailyCapFloor">Whether the floor applies to the daily rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override double FloorletRate(
        FloatingRateCoupon coupon, double effectiveFloor, bool dailyCapFloor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        throw new NanoQLException("CompoundingOvernightIndexedCouponPricer::floorletRate(Rate, bool) not implemented");
    }

    /// <inheritdoc/>
    public override double AverageRate(FloatingRateCoupon coupon, Date date, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OvernightIndexedCoupon c = CouponOf(coupon);
        NanoQL.Time.Date.ThrowIfDefault(date, nameof(date));
        return Compute(c, date, context).SwapletRate;
    }

    /// <summary>The coupon's effective spread (see <see cref="OvernightIndexedCoupon.EffectiveSpread"/>).</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The effective spread.</returns>
    public double EffectiveSpread(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OvernightIndexedCoupon c = CouponOf(coupon);
        return Compute(c, c.AccrualEndDate, context).EffectiveSpread;
    }

    /// <summary>The coupon's effective index fixing (see <see cref="OvernightIndexedCoupon.EffectiveSpread"/>).</summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The effective index fixing.</returns>
    public double EffectiveIndexFixing(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OvernightIndexedCoupon c = CouponOf(coupon);
        return Compute(c, c.AccrualEndDate, context).EffectiveIndexFixing;
    }

    /// <summary>
    /// The coupon's swaplet rate accrued up to a date, with its effective spread and index fixing: past fixings from the
    /// context's store, today's if stored, and the forward part forecast on the index's curve.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="date">The date the rate accrues to.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The swaplet rate, the effective spread and the effective index fixing.</returns>
    /// <exception cref="NanoQLException">A past fixing is missing, the index has no curve, or the curve doesn't span the
    /// forward part.</exception>
    protected (double SwapletRate, double EffectiveSpread, double EffectiveIndexFixing) Compute(
        OvernightIndexedCoupon coupon, Date date, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        Date today = context.EvaluationDate;

        // The coupon's index is always an overnight index: its constructor takes one.
        var index = (OvernightIndex)coupon.Index;
        TimeSeries<double> pastFixings = index.TimeSeries(context);

        ImmutableArray<Date> fixingDates = coupon.FixingDates();
        ImmutableArray<Date> valueDates = coupon.ValueDates();
        ImmutableArray<Date> interestDates = coupon.InterestDates();
        ImmutableArray<double> dt = coupon.Dt();
        bool applyObservationShift = coupon.ApplyObservationShift && coupon.FixingDays > 0;
        DayCounter dc = index.DayCounter;
        double couponSpread = coupon.Spread;
        bool compoundSpreadDaily = coupon.CompoundSpreadDaily;

        int i = 0;
        int n = DetermineNumberOfFixings(interestDates, date);

        (double Gf, double GfSpread) GrowthFactor(double fixing, int idx)
        {
            double span = applyObservationShift || date >= interestDates[idx + 1]
                ? dt[idx]
                : dc.YearFraction(interestDates[idx], date);
            double gf = 1.0 + (fixing * span);
            double gfSpread = compoundSpreadDaily ? gf + (couponSpread * span) : gf;
            return (gf, gfSpread);
        }

        double compoundFactor = 1.0, compoundFactorWithoutSpread = 1.0;

        // already fixed part
        while (i < n && fixingDates[i] < today)
        {
            // rate must have been fixed
            if (!pastFixings.TryGetValue(fixingDates[i], out double fixing))
            {
                throw new NanoQLException($"Missing {index.Name} fixing for {fixingDates[i]}");
            }

            (double gf, double gfSpread) = GrowthFactor(fixing, i);
            compoundFactorWithoutSpread *= gf;
            compoundFactor *= gfSpread;
            ++i;
        }

        // today is a border case. QuantLib's try block guards a read of the stored series, which can't throw.
        if (i < n && fixingDates[i] == today && pastFixings.TryGetValue(fixingDates[i], out double todaysFixing))
        {
            // might have been fixed
            (double gf, double gfSpread) = GrowthFactor(todaysFixing, i);
            compoundFactorWithoutSpread *= gf;
            compoundFactor *= gfSpread;
            ++i;
        }

        // forward part using telescopic property in order
        // to avoid the evaluation of multiple forward fixings
        // where possible.
        if (i < n)
        {
            YieldTermStructure curve = index.ForwardingTermStructure
                ?? throw new NanoQLException($"null term structure set to this instance of {index.Name}");
            if (!(curve.ReferenceDate <= valueDates[i] && (curve.AllowsExtrapolation || valueDates[n] <= curve.MaxDate)))
            {
                throw new NanoQLException(
                    $"coupon requires a range [{valueDates[i]}, {valueDates[n]}] wider than is supported by the term "
                    + $"structure for instance of{index.Name} [{curve.ReferenceDate}, "
                    + $"{(curve.AllowsExtrapolation ? NanoQL.Time.Date.MaxDate : curve.MaxDate)}]");
            }

            (double Gf, double GfSpread) ProjGrowthFactor(int k) => GrowthFactor(index.Fixing(fixingDates[k], context), k);

            if (coupon.CanApplyTelescopicFormula)
            {
                // handle partial accrual of first fixing if the first interestDate lands on a fixing holiday
                int telescopicStartIdx =
                    i == 0 && !applyObservationShift && valueDates[0] < interestDates[0] ? 1 : i;

                // telescopic formula up to potential lockout dates and partial accrual up to date.
                int endDateIdx = Math.Min(n, valueDates.Length - 1 - coupon.LockoutDays);
                int telescopicEndIdx = endDateIdx - (applyObservationShift || valueDates[endDateIdx] <= date ? 0 : 1);
                if (telescopicStartIdx < telescopicEndIdx)
                {
                    while (i < telescopicStartIdx)
                    {
                        // compound up any periods ahead of the telescopic range
                        (double gf, double gfSpread) = ProjGrowthFactor(i);
                        compoundFactorWithoutSpread *= gf;
                        compoundFactor *= gfSpread;
                        ++i;
                    }

                    double startDiscount = curve.Discount(valueDates[telescopicStartIdx]);
                    double endDiscount = curve.Discount(valueDates[telescopicEndIdx]);
                    compoundFactorWithoutSpread *= startDiscount / endDiscount;
                    if (compoundSpreadDaily && couponSpread != 0.0)
                    {
                        // the spread compounds each day on the forecast fixings, where QuantLib's discount ratio
                        // drops it
                        while (i < telescopicEndIdx)
                        {
                            compoundFactor *= ProjGrowthFactor(i).GfSpread;
                            ++i;
                        }
                    }
                    else
                    {
                        compoundFactor *= startDiscount / endDiscount;
                        i = telescopicEndIdx;
                    }
                }
            }

            // compound up any remaining periods
            while (i < n)
            {
                (double gf, double gfSpread) = ProjGrowthFactor(i);
                compoundFactorWithoutSpread *= gf;
                compoundFactor *= gfSpread;
                ++i;
            }
        }

        (Date rateAccrualStartDate, Date rateAccrualEndDate) = applyObservationShift
            ? (valueDates[0], valueDates[n])
            : (interestDates[0], date < interestDates[n] ? date : interestDates[n]);
        double tau = dc.YearFraction(rateAccrualStartDate, rateAccrualEndDate);
        double rate = (compoundFactor - 1.0) / tau;
        double swapletRate = coupon.Gearing * rate;
        double effectiveSpread;
        double effectiveIndexFixing;

        if (!compoundSpreadDaily)
        {
            swapletRate += couponSpread;
            effectiveSpread = couponSpread;
            effectiveIndexFixing = rate;
        }
        else
        {
            effectiveSpread = rate - ((compoundFactorWithoutSpread - 1.0) / tau);
            effectiveIndexFixing = rate - effectiveSpread;
        }

        return (swapletRate, effectiveSpread, effectiveIndexFixing);
    }

    private static NanoQLException NotAvailable(string member, FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        return new NanoQLException($"{member} not available");
    }
}
