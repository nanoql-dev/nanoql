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

/// <summary>The pricer for arithmetically averaged overnight-indexed coupons.</summary>
/// <remarks>
/// <para>
/// Reference: Katsumi Takada 2011, Valuation of Arithmetically Average of Fed Funds Rates and Construction of the US
/// Dollar Swap Yield Curve. The forward part is forecast fixing by fixing, each with a Hull-White convexity adjustment
/// for its payment delay, or, with the approximation, through one discount ratio less two convexity adjustments.
/// </para>
/// <para>
/// Unsealed, as in QuantLib, where the Black averaging pricer derives from it. A mean reversion of zero gives NaN
/// wherever a fixing is forecast, as QuantLib's formulas do, the volatility being divided by its cube.
/// </para>
/// </remarks>
public class ArithmeticAveragedOvernightIndexedCouponPricer : OvernightIndexedCouponPricer
{
    private readonly bool byApprox;
    private readonly double mrs;
    private readonly double vol;

    /// <summary>Initializes a new instance of the <see cref="ArithmeticAveragedOvernightIndexedCouponPricer"/> class.</summary>
    /// <param name="meanReversion">The Hull-White mean reversion of the convexity adjustment.</param>
    /// <param name="volatility">The Hull-White volatility of the convexity adjustment; none by default.</param>
    /// <param name="byApprox">Whether to use Katsumi Takada's approximation.</param>
    /// <param name="v">The optionlet volatility for caplets and floorlets, or <see langword="null"/> for none.</param>
    /// <param name="effectiveVolatilityInput">Whether the volatility is the effective volatility of the period's rate.</param>
    public ArithmeticAveragedOvernightIndexedCouponPricer(
        double meanReversion = 0.03,
        double volatility = 0.00,
        bool byApprox = false,
        OptionletVolatilityStructure? v = null,
        bool effectiveVolatilityInput = false)
        : base(v, effectiveVolatilityInput)
    {
        this.byApprox = byApprox;
        mrs = meanReversion;
        vol = volatility;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArithmeticAveragedOvernightIndexedCouponPricer"/> class with no
    /// convexity adjustment.
    /// </summary>
    /// <param name="byApprox">Whether to use Katsumi Takada's approximation.</param>
    public ArithmeticAveragedOvernightIndexedCouponPricer(bool byApprox)
        : this(0.03, 0.0, byApprox)
    {
    }

    /// <inheritdoc/>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AverageRate(coupon, CouponOf(coupon).AccrualEndDate, context);
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

    /// <summary>Not implemented: QuantLib's pricer throws; the Black averaging pricer prices caplets.</summary>
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
        throw new NanoQLException(
            "ArithmeticAveragedOvernightIndexedCouponPricer::capletRate(Rate, bool) not implemented");
    }

    /// <summary>Not implemented: QuantLib's pricer throws; the Black averaging pricer prices floorlets.</summary>
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
        throw new NanoQLException(
            "ArithmeticAveragedOvernightIndexedCouponPricer::floorletRate(Rate, bool) not implemented");
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">A past fixing is missing, or a fixing is forecast and the index has no
    /// curve.</exception>
    public override double AverageRate(FloatingRateCoupon coupon, Date date, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OvernightIndexedCoupon c = CouponOf(coupon);
        NanoQL.Time.Date.ThrowIfDefault(date, nameof(date));

        // The coupon's index is always an overnight index: its constructor takes one.
        var index = (OvernightIndex)c.Index;

        ImmutableArray<Date> fixingDates = c.FixingDates();
        ImmutableArray<Date> interestDates = c.InterestDates();
        ImmutableArray<double> dt = c.Dt();

        int i = 0;
        int n = DetermineNumberOfFixings(interestDates, date);

        double accumulatedRate = 0.0;

        TimeSeries<double> pastFixings = index.TimeSeries(context);

        // already fixed part
        Date today = context.EvaluationDate;
        while (i < n && fixingDates[i] < today)
        {
            // rate must have been fixed
            if (!pastFixings.TryGetValue(fixingDates[i], out double pastFixing))
            {
                throw new NanoQLException($"Missing {index.Name} fixing for {fixingDates[i]}");
            }

            double span = date >= interestDates[i + 1]
                ? dt[i]
                : index.DayCounter.YearFraction(interestDates[i], date);
            accumulatedRate += index.SimpleAccrual(pastFixing, span);
            ++i;
        }

        // today is a border case. QuantLib's try block guards a read of the stored series, which can't throw.
        if (i < n && fixingDates[i] == today && pastFixings.TryGetValue(fixingDates[i], out double todaysFixing))
        {
            // might have been fixed
            double span = date >= interestDates[i + 1]
                ? dt[i]
                : index.DayCounter.YearFraction(interestDates[i], date);
            accumulatedRate += index.SimpleAccrual(todaysFixing, span);
            ++i;
        }

        /* forward part using telescopic property in order
        to avoid the evaluation of multiple forward fixings
        (approximation proposed by Katsumi Takada)*/
        if (byApprox && i < n)
        {
            YieldTermStructure curve = index.ForwardingTermStructure
                ?? throw new NanoQLException($"null term structure set to this instance of {index.Name}");

            ImmutableArray<Date> dates = c.ValueDates();
            double startDiscount = curve.Discount(dates[i]);
            double endDiscount = curve.Discount(dates[n]);

            accumulatedRate +=
                Math.Log(startDiscount / endDiscount)
                - ConvAdj1(curve.TimeFromReference(dates[i]), curve.TimeFromReference(dates[n]))
                - ConvAdj2(curve.TimeFromReference(dates[i]), curve.TimeFromReference(dates[n]));
        }

        // otherwise
        else if (i < n)
        {
            YieldTermStructure curve = index.ForwardingTermStructure
                ?? throw new NanoQLException($"null term structure set to this instance of {index.Name}");

            ImmutableArray<Date> dates = c.ValueDates();
            double te = curve.TimeFromReference(dates[n]);
            while (i < n)
            {
                // forcast fixing
                double forecastFixing = index.Fixing(fixingDates[i], context);
                double ti1 = curve.TimeFromReference(dates[i]);
                double ti2 = curve.TimeFromReference(dates[i + 1]);
                /*convexity adjustment due to payment dalay of each
                overnight fixing, supposing an Hull-White short rate model*/
                double convAdj = Math.Exp(
                    0.5 * Math.Pow(vol, 2.0) / Math.Pow(mrs, 3.0) * (Math.Exp(2 * mrs * ti1) - 1)
                    * (Math.Exp(-mrs * ti2) - Math.Exp(-mrs * te)) * (Math.Exp(-mrs * ti2) - Math.Exp(-mrs * ti1)));
                accumulatedRate += (convAdj * index.GrowthFactor(forecastFixing, dt[i])) - 1;
                ++i;
            }
        }

        double rate = accumulatedRate / c.AccruedPeriod(date);
        return (c.Gearing * rate) + c.Spread;
    }

    /// <summary>The approximation's first convexity adjustment.</summary>
    /// <param name="ts">The time of the forward part's start.</param>
    /// <param name="te">The time of its end.</param>
    /// <returns>The adjustment.</returns>
    protected double ConvAdj1(double ts, double te) =>
        vol * vol / (4.0 * Math.Pow(mrs, 3.0)) * (1.0 - Math.Exp(-2.0 * mrs * ts))
        * Math.Pow(1.0 - Math.Exp(-mrs * (te - ts)), 2.0);

    /// <summary>The approximation's second convexity adjustment.</summary>
    /// <param name="ts">The time of the forward part's start.</param>
    /// <param name="te">The time of its end.</param>
    /// <returns>The adjustment.</returns>
    protected double ConvAdj2(double ts, double te) =>
        vol * vol / (2.0 * Math.Pow(mrs, 2.0))
        * ((te - ts) - (Math.Pow(1.0 - Math.Exp(-mrs * (te - ts)), 2.0) / mrs)
           - ((1.0 - Math.Exp(-2.0 * mrs * (te - ts))) / (2.0 * mrs)));

    private static NanoQLException NotAvailable(string member, FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        return new NanoQLException($"{member} not available");
    }
}
