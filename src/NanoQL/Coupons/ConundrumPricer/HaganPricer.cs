// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The base class of the pricing of a CMS coupon by static replication, as in Hagan's "Conundrums..." article: a
/// caplet's, a floorlet's and the swaplet's price from vanilla swaptions, through a g-function of the yield-curve
/// model.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: each public member computes, inside the call, what QuantLib's <c>initialize(coupon)</c> stores in the
/// pricer before its queries (the S-cms plan's CMR1 (i)), the g-function with its memo among it, and a capped or
/// collared coupon's rate reads its three rates from one such state. <see cref="WithMeanReversion"/> and
/// <see cref="WithSwaptionVolatility"/> return copies of the pricer's own type, where QuantLib's setters replace its
/// quote and handle in place.
/// </para>
/// <para>
/// The standard model reads the swap index's tenor as its length in years, where QuantLib reads its length in the
/// tenor's own units, so an 18-month index was 18 years; a tenor that isn't a whole number of fixed-leg periods (18
/// months on an annual leg) starts with a stub the model can't price, and is refused (the S-cms plan's CMR5 (i)).
/// </para>
/// <para>
/// On a lognormal volatility a caplet struck at or below 0, on a forward above 0, where QuantLib prices the caplet at a
/// strike of 1e-10 and so drops the strike, is the coupon's expected rate less the strike, the limit of the
/// replication's own formula there; the floorlet there is 0, as QuantLib's (the S-cms plan's CMR5 (xxiii)). A forward
/// at or below 0 has no lognormal rate distribution, and its caplet refuses, as QuantLib's.
/// </para>
/// <para>Derived inside the library only, by <see cref="NumericHaganPricer"/> and <see cref="AnalyticHaganPricer"/>,
/// since its per-coupon state is internal.</para>
/// </remarks>
public abstract class HaganPricer : CmsCouponPricer, IMeanRevertingPricer
{
    private const double CutoffForCaplet = 2;
    private const double CutoffForFloorlet = 0;

    private readonly GFunctionFactoryYieldCurveModel modelOfYieldCurve;

    // Written by the constructor, and by WithMeanReversion on a fresh copy only.
    private double meanReversion;

    private protected HaganPricer(
        SwaptionVolatilityStructure? swaptionVol,
        GFunctionFactoryYieldCurveModel modelOfYieldCurve,
        double meanReversion)
        : base(swaptionVol)
    {
        EnumArguments.Check(modelOfYieldCurve, nameof(modelOfYieldCurve));
        this.modelOfYieldCurve = modelOfYieldCurve;
        this.meanReversion = meanReversion;
    }

    /// <summary>Gets the mean reversion, which the non-parallel shifts' g-function reads.</summary>
    public double MeanReversion => meanReversion;

    /// <summary>A copy of the pricer, of its own type, with another mean reversion.</summary>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public virtual HaganPricer WithMeanReversion(double meanReversion)
    {
        var copy = (HaganPricer)MemberwiseClone();
        copy.meanReversion = meanReversion;
        return copy;
    }

    /// <inheritdoc/>
    IMeanRevertingPricer IMeanRevertingPricer.WithMeanReversion(double meanReversion) => WithMeanReversion(meanReversion);

    /// <summary>A copy of the pricer, of its own type, on another swaption volatility.</summary>
    /// <param name="swaptionVolatility">The swaption volatility, or <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public override HaganPricer WithSwaptionVolatility(SwaptionVolatilityStructure? swaptionVolatility) =>
        (HaganPricer)base.WithSwaptionVolatility(swaptionVolatility);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletPrice(Initialize(coupon, context));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletRate(Initialize(coupon, context));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletPrice(Initialize(coupon, context), effectiveCap);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletRate(Initialize(coupon, context), effectiveCap);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletPrice(Initialize(coupon, context), effectiveFloor);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletRate(Initialize(coupon, context), effectiveFloor);

    // One state for the three rates a capped coupon reads, as QuantLib's one initialize serves them.
    internal override CouponRateView RateView(FloatingRateCoupon coupon, EvaluationContext context) =>
        new Rates(this, Initialize(coupon, context));

    // QuantLib's pure virtual swapletPrice() on the state.
    private protected abstract double SwapletPrice(HaganPricerState s);

    // QuantLib's optionletPrice(optionType, strike) on the state: the replication's price of an option on the fixing.
    private protected abstract double OptionletPrice(HaganPricerState s, OptionType optionType, double strike);

    // The price of the fixing itself on the replication, the swaplet's at a gearing of 1 and no spread: a caplet struck
    // at or below 0 on a lognormal volatility is this less the strike's price (CMR5 (xxiii)).
    private protected abstract double ExpectedRatePrice(HaganPricerState s);

    // QuantLib's HaganPricer::initialize(coupon), for one call.
    private protected HaganPricerState Initialize(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        CmsCoupon cms = coupon as CmsCoupon ?? throw new ArgumentException("CMS coupon needed", nameof(coupon));
        double gearing = cms.Gearing;
        double spread = cms.Spread;
        double accrualPeriod = cms.AccrualPeriod();
        if (!(accrualPeriod != 0.0))
        {
            throw new NanoQLException("null accrual period");
        }

        Date fixingDate = cms.FixingDate();
        Date paymentDate = cms.Date;
        SwapIndex swapIndex = cms.SwapIndex;
        YieldTermStructure rateCurve = (swapIndex.DiscountingTermStructure ?? swapIndex.ForwardingTermStructure)
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        Date today = context.EvaluationDate;

        double discount = paymentDate > today ? rateCurve.Discount(paymentDate) : 1.0;

        double spreadLegValue = spread * accrualPeriod * discount;

        if (!(fixingDate > today))
        {
            return new HaganPricerState
            {
                Coupon = cms,
                Context = context,
                Gearing = gearing,
                Spread = spread,
                AccrualPeriod = accrualPeriod,
                FixingDate = fixingDate,
                PaymentDate = paymentDate,
                Today = today,
                Discount = discount,
                SpreadLegValue = spreadLegValue,
            };
        }

        Period swapTenor = swapIndex.Tenor;
        VanillaSwap swap = swapIndex.UnderlyingSwap(fixingDate, context);
        FixedVsFloatingSwapResults results = swapIndex.UnderlyingSwapResults(swap, context);

        double swapRateValue = results.FairRate ?? throw new NanoQLException("result not available");

        const double bp = 1.0e-4;
        double annuity = Math.Abs((results.FixedLegBPS ?? throw new NanoQLException("result not available")) / bp);

        int q = (int)swapIndex.FixedLegTenor.Frequency();
        Schedule schedule = swap.FixedSchedule;
        DayCounter dc = swapIndex.DayCounter;
        double startTime = dc.YearFraction(rateCurve.ReferenceDate, swap.StartDate());
        double swapFirstPaymentTime = dc.YearFraction(rateCurve.ReferenceDate, schedule.Date(1));
        double paymentTime = dc.YearFraction(rateCurve.ReferenceDate, paymentDate);
        double delta = (paymentTime - startTime) / (swapFirstPaymentTime - startTime);

        GFunction gFunction = modelOfYieldCurve switch
        {
            // the swap's length in years, where QuantLib reads the tenor's length in its own units (CMR5 (i))
            GFunctionFactoryYieldCurveModel.Standard =>
                GFunctionFactory.NewGFunctionStandard(q, delta, StandardSwapLength(swapTenor, swapIndex.FixedLegTenor, q)),
            GFunctionFactoryYieldCurveModel.ExactYield => GFunctionFactory.NewGFunctionExactYield(cms, context),
            GFunctionFactoryYieldCurveModel.ParallelShifts => GFunctionFactory.NewGFunctionWithShifts(cms, 0.0, context),
            _ => GFunctionFactory.NewGFunctionWithShifts(cms, meanReversion, context),
        };

        SwaptionVolatilityStructure volatility = Volatility();
        var vanillaOptionPricer = new MarketQuotedOptionPricer(swapRateValue, fixingDate, swapTenor, volatility);

        return new HaganPricerState
        {
            Coupon = cms,
            Context = context,
            Gearing = gearing,
            Spread = spread,
            AccrualPeriod = accrualPeriod,
            FixingDate = fixingDate,
            PaymentDate = paymentDate,
            Today = today,
            Discount = discount,
            SpreadLegValue = spreadLegValue,
            Volatility = volatility,
            SwapTenor = swapTenor,
            SwapRateValue = swapRateValue,
            Annuity = annuity,
            GFunction = gFunction,
            VanillaOptionPricer = vanillaOptionPricer,
        };
    }

    private static double Max0(double x) => x < 0.0 ? 0.0 : x;

    // The standard model's swap length in years, which it reads as a whole number of equal fixed-leg periods; a tenor
    // that isn't one starts with a stub, which the model's delay, counted in the first period's length, can't price,
    // so it is refused (the S-cms plan's CMR5 (i), as the user ruled during its run).
    private static double StandardSwapLength(Period swapTenor, Period fixedLegTenor, int q)
    {
        bool whole = swapTenor.Units switch
        {
            TimeUnit.Years => true,
            TimeUnit.Months => swapTenor.Length * q % 12 == 0,
            _ => false,
        };
        if (!whole)
        {
            throw new NanoQLException(
                $"the Standard g-function needs a swap of whole fixed-leg periods, not {swapTenor} on a {fixedLegTenor} "
                + "fixed leg; the ExactYield model reads the swap's schedule");
        }

        return PeriodFunctions.Years(swapTenor);
    }

    private double SwapletRate(HaganPricerState s) => SwapletPrice(s) / (s.AccrualPeriod * s.Discount);

    private double CapletPrice(HaganPricerState s, double effectiveCap)
    {
        // caplet is equivalent to call option on fixing
        if (s.IsFixed)
        {
            // the fixing is determined
            double Rs = Max0(s.SwapIndex.Fixing(s.FixingDate, s.Context) - effectiveCap);
            double price = s.Gearing * Rs * (s.AccrualPeriod * s.Discount);
            return price;
        }
        else
        {
            double capletPrice = 0.0;

            if (s.Volatility!.VolatilityType == VolatilityType.ShiftedLognormal)
            {
                const double cutoffNearZero = 1e-10;

                if (effectiveCap <= 0.0 && s.SwapRateValue > 0.0)
                {
                    // QuantLib's caplet at 1e-10 drops the strike; the caplet is the expected rate less the strike, on
                    // a forward above 0, the lognormal model's lowest rate (its option pricer refuses a shift), where a
                    // forward at or below it keeps QuantLib's refusal
                    capletPrice = ExpectedRatePrice(s) - (effectiveCap * (s.AccrualPeriod * s.Discount));
                }
                else if (effectiveCap < CutoffForCaplet)
                {
                    double effectiveStrikeForMax = Math.Max(effectiveCap, cutoffNearZero);
                    capletPrice = OptionletPrice(s, OptionType.Call, effectiveStrikeForMax);
                }
            }
            else
            {
                capletPrice = OptionletPrice(s, OptionType.Call, effectiveCap);
            }

            return s.Gearing * capletPrice;
        }
    }

    private double CapletRate(HaganPricerState s, double effectiveCap) =>
        CapletPrice(s, effectiveCap) / (s.AccrualPeriod * s.Discount);

    private double FloorletPrice(HaganPricerState s, double effectiveFloor)
    {
        // floorlet is equivalent to put option on fixing
        if (s.IsFixed)
        {
            // the fixing is determined
            double Rs = Max0(effectiveFloor - s.SwapIndex.Fixing(s.FixingDate, s.Context));
            double price = s.Gearing * Rs * (s.AccrualPeriod * s.Discount);
            return price;
        }
        else
        {
            double floorletPrice = 0.0;
            if (s.Volatility!.VolatilityType == VolatilityType.ShiftedLognormal)
            {
                const double cutoffNearZero = 1e-10;

                if (effectiveFloor > CutoffForFloorlet)
                {
                    double effectiveStrikeForMin = Math.Max(effectiveFloor, cutoffNearZero);
                    floorletPrice = OptionletPrice(s, OptionType.Put, effectiveStrikeForMin);
                }
            }
            else
            {
                floorletPrice = OptionletPrice(s, OptionType.Put, effectiveFloor);
            }

            return s.Gearing * floorletPrice;
        }
    }

    private double FloorletRate(HaganPricerState s, double effectiveFloor) =>
        FloorletPrice(s, effectiveFloor) / (s.AccrualPeriod * s.Discount);

    // The three rates on one state.
    private sealed class Rates(HaganPricer pricer, HaganPricerState s) : CouponRateView
    {
        public override double SwapletRate() => pricer.SwapletRate(s);

        public override double CapletRate(double effectiveCap) => pricer.CapletRate(s, effectiveCap);

        public override double FloorletRate(double effectiveFloor) => pricer.FloorletRate(s, effectiveFloor);
    }
}
