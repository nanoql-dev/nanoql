// SPDX-FileCopyrightText: Copyright (C) 2014, 2015, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/lognormalcmsspreadpricer.hpp ql/experimental/coupons/lognormalcmsspreadpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: LognormalCmsSpreadPricer::integrand_normal, never ported: lognormalcmsspreadpricer.cpp defines it and nothing calls it

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// The CMS spread coupon pricer of Brigo and Mercurio, 13.6.2, with the extensions for shifted lognormal and normal
/// dynamics of http://ssrn.com/abstract=2686998: QuantLib's <c>LognormalCmsSpreadPricer</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two swap rates' convexity adjustments are the CMS pricer's, on its swaption volatility, whose type and shifts
/// the spread model inherits by default; or the type is given, and on a lognormal model the shifts, 0 when not given.
/// An at-the-money surface takes only the inherited type; a cube is read on the model's type and shifts through its
/// smile sections. The correlation is clamped to ±0.9999, so that the integrand never divides by 0; at the default 16
/// Gauss-Hermite points its value at 0.9999 is 1.5e-3 off the 256-point value, the points the caller's choice. References:
/// Brigo and Mercurio, Interest Rate Models - Theory and Practice, 2nd edition, Springer, 2006, chapter 13.6.2.
/// </para>
/// <para>
/// With unequal shifts, the integrand branches on the displaced strike k + g₁s₁ + g₂s₂, and its negative branch shifts
/// the second rate by its own shift, as QuantLib's master does after 1.44 (#2874); QuantLib 1.44 branches on the strike
/// and shifts the second rate by the first's, which prices NaN caplets and floorlets at strikes 0.008 and 0.002 on
/// #2874's set-up and breaks put-call parity by 2.03e-2 at others (<c>Divergences/CmsSpreadUnequalShiftsTests.cs</c>).
/// Equal shifts take the same branch either way.
/// </para>
/// <para>
/// Without a coupon discount curve, each coupon is discounted on its own first swap index's curve, its discounting
/// curve when exogenous and its forwarding curve otherwise; QuantLib's pricer keeps the first coupon's curve for every
/// later coupon, so one pricer serving coupons on different indexes discounts them all on the first's
/// (<c>Divergences/CmsSpreadCouponDiscountCurveTests.cs</c>). A pricer is stateless here, as every nanoql pricer: what
/// QuantLib's <c>initialize</c> stores is computed per call, one state serving a capped coupon's three rates.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class LognormalCmsSpreadPricer : CmsSpreadCouponPricer
{
    private readonly CmsCouponPricer cmsPricer;
    private readonly YieldTermStructure? couponDiscountCurve;
    private readonly GaussHermiteIntegration integrator;
    private readonly CumulativeNormalDistribution cnd;
    private readonly bool inheritedVolatilityType;
    private readonly VolatilityType volType;
    private readonly double shift1;
    private readonly double shift2;

    /// <summary>Initializes a new instance of the <see cref="LognormalCmsSpreadPricer"/> class.</summary>
    /// <param name="cmsPricer">The pricer of the two CMS coupons, whose swaption volatility the model reads.</param>
    /// <param name="correlation">The correlation of the two swap rates, or <see langword="null"/>, QuantLib's empty
    /// handle, refused when a future fixing is priced.</param>
    /// <param name="couponDiscountCurve">The curve the coupons are discounted on, or <see langword="null"/> for each
    /// coupon's first swap index's.</param>
    /// <param name="integrationPoints">The number of Gauss-Hermite points, at least 4.</param>
    /// <param name="volatilityType">The spread model's volatility type, or <see langword="null"/> to inherit the swaption
    /// volatility's.</param>
    /// <param name="shift1">The first rate's shift on a given lognormal type, 0 when <see langword="null"/>.</param>
    /// <param name="shift2">The second rate's shift on a given lognormal type, 0 when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cmsPricer"/> is null.</exception>
    /// <exception cref="NanoQLException">
    /// There are fewer than 4 points, shifts are given with an inherited type, or the type is inherited from a CMS pricer
    /// with no swaption volatility, with QuantLib's texts.
    /// </exception>
    public LognormalCmsSpreadPricer(
        CmsCouponPricer cmsPricer,
        double? correlation,
        YieldTermStructure? couponDiscountCurve = null,
        int integrationPoints = 16,
        VolatilityType? volatilityType = null,
        double? shift1 = null,
        double? shift2 = null)
        : base(correlation)
    {
        this.cmsPricer = cmsPricer ?? throw new ArgumentNullException(nameof(cmsPricer));
        this.couponDiscountCurve = couponDiscountCurve;
        if (!(integrationPoints >= 4))
        {
            throw new NanoQLException($"at least 4 integration points should be used ({integrationPoints})");
        }

        integrator = new GaussHermiteIntegration(integrationPoints);
        cnd = new CumulativeNormalDistribution(0.0, 1.0);
        if (volatilityType is null)
        {
            if (!(shift1 is null && shift2 is null))
            {
                throw new NanoQLException("if volatility type is inherited, no shifts should be specified");
            }

            inheritedVolatilityType = true;
            volType = (cmsPricer.SwaptionVolatility ?? throw new NanoQLException("empty Handle cannot be dereferenced"))
                .VolatilityType;
        }
        else
        {
            this.shift1 = shift1 ?? 0.0;
            this.shift2 = shift2 ?? 0.0;
            inheritedVolatilityType = false;
            volType = volatilityType.Value;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS spread coupon.</exception>
    /// <exception cref="NanoQLException">
    /// The gearings aren't of opposite signs, a curve, the volatility or the correlation is missing, or the volatility
    /// type isn't inherited on an at-the-money surface.
    /// </exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletPrice(Initialize(coupon, context));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS spread coupon.</exception>
    /// <exception cref="NanoQLException">As <see cref="SwapletPrice(FloatingRateCoupon, EvaluationContext)"/>.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletRate(Initialize(coupon, context));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS spread coupon.</exception>
    /// <exception cref="NanoQLException">As <see cref="SwapletPrice(FloatingRateCoupon, EvaluationContext)"/>.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletPrice(Initialize(coupon, context), effectiveCap);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS spread coupon.</exception>
    /// <exception cref="NanoQLException">As <see cref="SwapletPrice(FloatingRateCoupon, EvaluationContext)"/>.</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletRate(Initialize(coupon, context), effectiveCap);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS spread coupon.</exception>
    /// <exception cref="NanoQLException">As <see cref="SwapletPrice(FloatingRateCoupon, EvaluationContext)"/>.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletPrice(Initialize(coupon, context), effectiveFloor);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS spread coupon.</exception>
    /// <exception cref="NanoQLException">As <see cref="SwapletPrice(FloatingRateCoupon, EvaluationContext)"/>.</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletRate(Initialize(coupon, context), effectiveFloor);

    // One state for the three rates a capped coupon reads, as QuantLib's one initialize serves them.
    internal override CouponRateView RateView(FloatingRateCoupon coupon, EvaluationContext context) =>
        new Rates(this, Initialize(coupon, context));

    // std::max(a, b) and std::min(a, b), which keep a where b is NaN.
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static double SwapletPrice(State s) =>
        (s.Gearing * s.AccrualPeriod * s.Discount * ((s.Gearing1 * s.AdjustedRate1) + (s.Gearing2 * s.AdjustedRate2)))
        + s.SpreadLegValue;

    private static double SwapletRate(State s) => SwapletPrice(s) / (s.AccrualPeriod * s.Discount);

    private double CapletRate(State s, double effectiveCap) => CapletPrice(s, effectiveCap) / (s.AccrualPeriod * s.Discount);

    private double FloorletRate(State s, double effectiveFloor) =>
        FloorletPrice(s, effectiveFloor) / (s.AccrualPeriod * s.Discount);

    // caplet is equivalent to call option on fixing
    private double CapletPrice(State s, double effectiveCap)
    {
        if (s.FixingDate <= s.Today)
        {
            // the fixing is determined
            double rs = Max(s.Coupon.Index.Fixing(s.FixingDate, s.Context) - effectiveCap, 0.0);
            return s.Gearing * rs * s.AccrualPeriod * s.Discount;
        }
        else
        {
            double capletPrice = OptionletPrice(s, OptionType.Call, effectiveCap);
            return s.Gearing * capletPrice;
        }
    }

    // floorlet is equivalent to put option on fixing
    private double FloorletPrice(State s, double effectiveFloor)
    {
        if (s.FixingDate <= s.Today)
        {
            // the fixing is determined
            double rs = Max(effectiveFloor - s.Coupon.Index.Fixing(s.FixingDate, s.Context), 0.0);
            return s.Gearing * rs * s.AccrualPeriod * s.Discount;
        }
        else
        {
            double floorletPrice = OptionletPrice(s, OptionType.Put, effectiveFloor);
            return s.Gearing * floorletPrice;
        }
    }

    // this method is only called for future fixings
    private double OptionletPrice(State s, OptionType optionType, double strike)
    {
        double phi = optionType == OptionType.Call ? 1.0 : -1.0;
        double res = 0.0;
        if (volType == VolatilityType.ShiftedLognormal)
        {
            // (shifted) lognormal volatility
            double a, b, s1, s2, m1, m2, v1, v2, k;
            double displacedStrike = strike + (s.Gearing1 * s.Shift1) + (s.Gearing2 * s.Shift2);
            if (displacedStrike >= 0.0)
            {
                a = s.Gearing1;
                b = s.Gearing2;
                s1 = s.SwapRate1 + s.Shift1;
                s2 = s.SwapRate2 + s.Shift2;
                m1 = s.Mu1;
                m2 = s.Mu2;
                v1 = s.Vol1;
                v2 = s.Vol2;
                k = displacedStrike;
            }
            else
            {
                a = -s.Gearing2;
                b = -s.Gearing1;
                s1 = s.SwapRate2 + s.Shift2;
                s2 = s.SwapRate1 + s.Shift1;
                m1 = s.Mu2;
                m2 = s.Mu1;
                v1 = s.Vol2;
                v2 = s.Vol1;
                k = -displacedStrike;
                res += phi * ((s.Gearing1 * s.AdjustedRate1) + (s.Gearing2 * s.AdjustedRate2) - strike);
            }

            double fixingTime = s.FixingTime;
            double rho = s.Rho;
            CumulativeNormalDistribution n = cnd;

            // this is Brigo, 13.16.2 with x = v / sqrt(2)
            double Integrand(double x)
            {
                double v = MathConstants.M_SQRT2 * x;
                double h = k - (b * s2 * Math.Exp(((m2 - (0.5 * v2 * v2)) * fixingTime) + (v2 * Math.Sqrt(fixingTime) * v)));
                double phi1 = n.Evaluate(
                    phi * (Math.Log(a * s1 / h)
                        + ((m1 + ((0.5 - (rho * rho)) * v1 * v1)) * fixingTime)
                        + (rho * v1 * Math.Sqrt(fixingTime) * v))
                    / (v1 * Math.Sqrt(fixingTime * (1.0 - (rho * rho)))));
                double phi2 = n.Evaluate(
                    phi * (Math.Log(a * s1 / h)
                        + ((m1 - (0.5 * v1 * v1)) * fixingTime)
                        + (rho * v1 * Math.Sqrt(fixingTime) * v))
                    / (v1 * Math.Sqrt(fixingTime * (1.0 - (rho * rho)))));
                double f = (a * phi * s1
                        * Math.Exp((m1 * fixingTime) - (0.5 * rho * rho * v1 * v1 * fixingTime) + (rho * v1 * Math.Sqrt(fixingTime) * v))
                        * phi1)
                    - (phi * h * phi2);
                return Math.Exp(-x * x) * f;
            }

            res += 1.0 / MathConstants.M_SQRTPI * integrator.Evaluate(Integrand);
        }
        else
        {
            // normal volatility
            double forward = (s.Gearing1 * s.AdjustedRate1) + (s.Gearing2 * s.AdjustedRate2);
            double stddev = Math.Sqrt(
                s.FixingTime
                * ((s.Gearing1 * s.Gearing1 * s.Vol1 * s.Vol1)
                    + (s.Gearing2 * s.Gearing2 * s.Vol2 * s.Vol2)
                    + (2.0 * s.Gearing1 * s.Gearing2 * s.Rho * s.Vol1 * s.Vol2)));
            res = BlackFormulaFunctions.BachelierBlackFormula(optionType, strike, forward, stddev, 1.0);
        }

        return res * s.Discount * s.AccrualPeriod;
    }

    // QuantLib's LognormalCmsSpreadPricer::initialize(coupon), for one call.
    private State Initialize(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        CmsSpreadCoupon c = coupon as CmsSpreadCoupon ?? throw new ArgumentException("CMS spread coupon needed", nameof(coupon));
        SwapSpreadIndex index = c.SwapSpreadIndex;
        var s = new State(c, context)
        {
            Gearing = c.Gearing,
            Spread = c.Spread,
            FixingDate = c.FixingDate(),
            PaymentDate = c.Date,
            AccrualPeriod = c.AccrualPeriod(),
        };

        // if no coupon discount curve is given just use the discounting curve from the coupon's first swap index
        // (QuantLib's keeps the first coupon's). for rate calculation this curve cancels out in the computation, so
        // e.g. the discounting swap engine will produce correct results, even if the couponDiscountCurve is not set
        // here. only the price member function in this class will be dependent on the coupon discount curve.
        s.Today = context.EvaluationDate;
        YieldTermStructure curve = couponDiscountCurve
            ?? (index.SwapIndex1.ExogenousDiscount ? index.SwapIndex1.DiscountingTermStructure : index.SwapIndex1.ForwardingTermStructure)
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        s.Discount = s.PaymentDate > curve.ReferenceDate ? curve.Discount(s.PaymentDate) : 1.0;
        s.SpreadLegValue = s.Spread * s.AccrualPeriod * s.Discount;
        s.Gearing1 = index.Gearing1;
        s.Gearing2 = index.Gearing2;
        if (!(s.Gearing1 > 0.0 && s.Gearing2 < 0.0))
        {
            throw new NanoQLException(
                $"gearing1 ({DataFormattersFunctions.General(s.Gearing1)}) should be positive while gearing2 "
                + $"({DataFormattersFunctions.General(s.Gearing2)}) should be negative");
        }

        CmsCoupon c1 = Leg(c, index.SwapIndex1, context);
        CmsCoupon c2 = Leg(c, index.SwapIndex2, context);
        if (s.FixingDate > s.Today)
        {
            SwaptionVolatilityStructure swvol =
                cmsPricer.SwaptionVolatility ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            s.FixingTime = swvol.TimeFromReference(s.FixingDate);
            s.SwapRate1 = c1.IndexFixing(context);
            s.SwapRate2 = c2.IndexFixing(context);
            s.AdjustedRate1 = c1.AdjustedFixing(context);
            s.AdjustedRate2 = c2.AdjustedFixing(context);
            s.Shift1 = shift1;
            s.Shift2 = shift2;
            if (inheritedVolatilityType && volType == VolatilityType.ShiftedLognormal)
            {
                s.Shift1 = swvol.Shift(s.FixingDate, index.SwapIndex1.Tenor);
                s.Shift2 = swvol.Shift(s.FixingDate, index.SwapIndex2.Tenor);
            }

            if (swvol is not SwaptionVolatilityCube swcub)
            {
                // not a cube, just an atm surface given, so we can not easily convert volatilities and just forbid it
                if (!inheritedVolatilityType)
                {
                    throw new NanoQLException("if only an atm surface is given, the volatility type must be inherited");
                }

                s.Vol1 = swvol.Volatility(s.FixingDate, index.SwapIndex1.Tenor, s.SwapRate1);
                s.Vol2 = swvol.Volatility(s.FixingDate, index.SwapIndex2.Tenor, s.SwapRate2);
            }
            else
            {
                s.Vol1 = swcub.SmileSection(s.FixingDate, index.SwapIndex1.Tenor).Volatility(s.SwapRate1, volType, s.Shift1);
                s.Vol2 = swcub.SmileSection(s.FixingDate, index.SwapIndex2.Tenor).Volatility(s.SwapRate2, volType, s.Shift2);
            }

            if (volType == VolatilityType.ShiftedLognormal)
            {
                s.Mu1 = 1.0 / s.FixingTime * Math.Log((s.AdjustedRate1 + s.Shift1) / (s.SwapRate1 + s.Shift1));
                s.Mu2 = 1.0 / s.FixingTime * Math.Log((s.AdjustedRate2 + s.Shift2) / (s.SwapRate2 + s.Shift2));
            }

            // for the normal volatility case we do not need the drifts but rather use adjusted rates directly in the
            // integrand
            double correlation = Correlation ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            s.Rho = Max(Min(correlation, 0.9999), -0.9999); // avoid division by zero in integrand
        }
        else
        {
            // fixing is in the past or today
            s.AdjustedRate1 = c1.IndexFixing(context);
            s.AdjustedRate2 = c2.IndexFixing(context);
        }

        return s;
    }

    // One of the two CMS coupons on the spread coupon's terms, at gearing 1 and spread 0, on the CMS pricer.
    private CmsCoupon Leg(CmsSpreadCoupon c, SwapIndex index, EvaluationContext context) =>
        new CmsCoupon(
            c.Date,
            c.Nominal(context),
            c.AccrualStartDate,
            c.AccrualEndDate,
            c.FixingDays,
            index,
            1.0,
            0.0,
            c.ReferencePeriodStart,
            c.ReferencePeriodEnd,
            c.DayCounter,
            c.IsInArrears).SetPricer(cmsPricer);

    // What QuantLib's initialize stores, for one call.
    private sealed class State(CmsSpreadCoupon coupon, EvaluationContext context)
    {
        public CmsSpreadCoupon Coupon { get; } = coupon;

        public EvaluationContext Context { get; } = context;

        public Date Today { get; set; }

        public Date FixingDate { get; init; }

        public Date PaymentDate { get; init; }

        public double FixingTime { get; set; }

        public double Gearing { get; init; }

        public double Spread { get; init; }

        public double AccrualPeriod { get; init; }

        public double SpreadLegValue { get; set; }

        public double Discount { get; set; }

        public double SwapRate1 { get; set; }

        public double SwapRate2 { get; set; }

        public double Gearing1 { get; set; }

        public double Gearing2 { get; set; }

        public double AdjustedRate1 { get; set; }

        public double AdjustedRate2 { get; set; }

        public double Vol1 { get; set; }

        public double Vol2 { get; set; }

        public double Mu1 { get; set; }

        public double Mu2 { get; set; }

        public double Rho { get; set; }

        public double Shift1 { get; set; }

        public double Shift2 { get; set; }
    }

    // The three rates on one state.
    private sealed class Rates(LognormalCmsSpreadPricer pricer, State s) : CouponRateView
    {
        public override double SwapletRate() => LognormalCmsSpreadPricer.SwapletRate(s);

        public override double CapletRate(double effectiveCap) => pricer.CapletRate(s, effectiveCap);

        public override double FloorletRate(double effectiveFloor) => pricer.FloorletRate(s, effectiveFloor);
    }
}
