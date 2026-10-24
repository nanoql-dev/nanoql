// SPDX-FileCopyrightText: Copyright (C) 2014, 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/lineartsrpricer.hpp ql/cashflows/lineartsrpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The pricing of a CMS coupon on a linear terminal swap rate model, whose slope is linked to a Gaussian short-rate
/// model (Andersen and Piterbarg, Interest Rate Modeling, 16.3.2).
/// </summary>
/// <remarks>
/// <para>
/// The integration's cut-off is set by <see cref="LinearTsrPricerSettings"/>: explicit lower and upper bounds; the
/// strike where a vanilla swaption has a given ratio (1%) of the at-the-money swaption's vega; the strike where the
/// undeflated payer or receiver price falls below a threshold; or a number of standard deviations of a Black-Scholes
/// process at the at-the-money volatility. The bounds are applied in every case: on a shifted lognormal section to the
/// strike plus the shift, so a zero lower bound is the shifted model's lowest rate; on a normal one the lower bound is
/// min(−upper, lower), unless the bounds are given explicitly.
/// </para>
/// <para>
/// Immutable: each public member computes, inside the call, what QuantLib's <c>initialize(coupon)</c> stores in the
/// pricer (the S-cms plan's CMR1 (i)), and a capped or collared coupon's rate reads its three rates from one such
/// state.
/// <see cref="WithMeanReversion"/> and <see cref="WithSwaptionVolatility"/> return copies, where QuantLib's setters
/// replace its quote and handle in place; the day counter of the model's slope is the volatility's the pricer holds,
/// where QuantLib's keeps its constructor's volatility's after <c>setSwaptionVolatility</c>.
/// </para>
/// <para>
/// QuantLib's fixes here (the S-cms plan's CMR5): the price-threshold strategy searches with its threshold, from the
/// bracket's midpoint, where QuantLib's passes the vega ratio and a guess at the bracket's end, which Brent refuses, so
/// every threshold gave the default bounds' price (ii); the standard-deviations strategy sets each option's own bound,
/// its lognormal width k·σ·√T and the shift subtracted once (iii); below a reversion of 1e-4 the model's shape is
/// −expm1(−a·τ)/a, τ at 0, where QuantLib's reads every such reversion as 0 (iv); on a lognormal section and a forward
/// above −shift a caplet struck at or below −shift is the coupon's expected rate less the strike and a floorlet there
/// 0, where QuantLib's refuses, a forward at or below −shift keeping QuantLib's refusal (xxiii); every section is
/// wrapped at the coupon's forward, where QuantLib's keeps a section's own at-the-money level, which on S7's SABR and
/// ZABR cubes is an interpolated forward (xxvii); and the vega-ratio search reads a normal section's vega from
/// Bachelier's derivative, where QuantLib's section refuses it (xxviii).
/// </para>
/// </remarks>
public sealed class LinearTsrPricer : CmsCouponPricer, IMeanRevertingPricer
{
    private readonly YieldTermStructure? couponDiscountCurve;
    private readonly LinearTsrPricerSettings settings;
    private readonly Integrator integrator;

    // Written by the constructor, and by WithMeanReversion on a fresh copy only.
    private double meanReversion;

    /// <summary>Initializes a new instance of the <see cref="LinearTsrPricer"/> class.</summary>
    /// <param name="swaptionVol">The swaption volatility.</param>
    /// <param name="meanReversion">The mean reversion of the model's slope.</param>
    /// <param name="couponDiscountCurve">The coupon's discounting curve, which only the prices read; the swap index's
    /// discounting curve when <see langword="null"/>, QuantLib's empty handle.</param>
    /// <param name="settings">The integration's bounds; QuantLib's default, the rate bounds 0.0001 and 2, when
    /// <see langword="null"/>.</param>
    /// <param name="integrator">The integrator; QuantLib's <c>GaussKronrodNonAdaptive(1e-10, 5000, 1e-10)</c> when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swaptionVol"/> is <see langword="null"/>, QuantLib's
    /// empty handle, which its constructor dereferences.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The settings' strategy isn't a member of its type.</exception>
    public LinearTsrPricer(
        SwaptionVolatilityStructure swaptionVol,
        double meanReversion,
        YieldTermStructure? couponDiscountCurve = null,
        LinearTsrPricerSettings? settings = null,
        Integrator? integrator = null)
        : base(NotNull(swaptionVol))
    {
        this.meanReversion = meanReversion;
        this.couponDiscountCurve = couponDiscountCurve;
        this.settings = settings ?? new LinearTsrPricerSettings();
        EnumArguments.Check(this.settings.Strategy, nameof(settings));
        this.integrator = integrator ?? new GaussKronrodNonAdaptive(1E-10, 5000, 1E-10);
    }

    /// <summary>Gets the mean reversion.</summary>
    public double MeanReversion => meanReversion;

    /// <summary>Gets the coupon's discounting curve, or <see langword="null"/> for the swap index's.</summary>
    public YieldTermStructure? CouponDiscountCurve => couponDiscountCurve;

    /// <summary>Gets the integration's bounds.</summary>
    public LinearTsrPricerSettings Settings => settings;

    /// <summary>Gets the integrator.</summary>
    public Integrator Integrator => integrator;

    /// <summary>A copy of the pricer with another mean reversion.</summary>
    /// <param name="meanReversion">The mean reversion.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public LinearTsrPricer WithMeanReversion(double meanReversion)
    {
        var copy = (LinearTsrPricer)MemberwiseClone();
        copy.meanReversion = meanReversion;
        return copy;
    }

    /// <inheritdoc/>
    IMeanRevertingPricer IMeanRevertingPricer.WithMeanReversion(double meanReversion) => WithMeanReversion(meanReversion);

    /// <summary>A copy of the pricer on another swaption volatility.</summary>
    /// <param name="swaptionVolatility">The swaption volatility, or <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public override LinearTsrPricer WithSwaptionVolatility(SwaptionVolatilityStructure? swaptionVolatility) =>
        (LinearTsrPricer)base.WithSwaptionVolatility(swaptionVolatility);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">A curve or the volatility is missing.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletPrice(Initialize(coupon, context));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">A curve or the volatility is missing.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) =>
        SwapletRate(Initialize(coupon, context));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">A curve or the volatility is missing.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletPrice(Initialize(coupon, context), effectiveCap);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">A curve or the volatility is missing.</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        CapletRate(Initialize(coupon, context), effectiveCap);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">A curve or the volatility is missing.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletPrice(Initialize(coupon, context), effectiveFloor);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="NanoQLException">A curve or the volatility is missing.</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        FloorletRate(Initialize(coupon, context), effectiveFloor);

    // One state for the three rates a capped coupon reads, as QuantLib's one initialize serves them.
    internal override CouponRateView RateView(FloatingRateCoupon coupon, EvaluationContext context) =>
        new Rates(this, Initialize(coupon, context));

    private static SwaptionVolatilityStructure NotNull(SwaptionVolatilityStructure swaptionVol)
    {
        ArgumentNullException.ThrowIfNull(swaptionVol);
        return swaptionVol;
    }

    private static double Max0(double x) => x < 0.0 ? 0.0 : x;

    // QuantLib's GsrG(d) on the volatility's day counter: (1 − e^(−a·τ))/a, τ from the fixing to d; below a reversion
    // of 1e-4 −expm1(−a·τ)/a, and τ at 0, where QuantLib's returns τ for every |a| 1e-4 (CMR5 (iv)).
    private double GsrG(DayCounter volDayCounter, Date fixingDate, Date d)
    {
        double yf = volDayCounter.YearFraction(fixingDate, d);
        double a = meanReversion;
        if (Math.Abs(a) < 1.0E-4)
        {
            return a == 0.0 ? yf : -Expm1Functions.StdExpm1(-a * yf) / a;
        }
        else
        {
            return (1.0 - Math.Exp(-a * yf)) / a;
        }
    }

    // QuantLib's LinearTsrPricer::initialize(coupon), for one call.
    private LinearTsrPricerState Initialize(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        CmsCoupon cms = coupon as CmsCoupon ?? throw new ArgumentException("CMS coupon needed", nameof(coupon));
        double gearing = cms.Gearing;
        double spread = cms.Spread;
        double accrualPeriod = cms.AccrualPeriod();

        Date fixingDate = cms.FixingDate();
        Date paymentDate = cms.Date;
        SwapIndex swapIndex = cms.SwapIndex;

        YieldTermStructure? forwardCurve = swapIndex.ForwardingTermStructure;
        YieldTermStructure discountCurve = (swapIndex.ExogenousDiscount ? swapIndex.DiscountingTermStructure : forwardCurve)
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        // if no coupon discount curve is given just use the discounting curve
        // from the swap index. for rate calculation this curve cancels out in
        // the computation, so e.g. the discounting swap engine will produce
        // correct results, even if the couponDiscountCurve is not set here.
        // only the price member function in this class will be dependent on the
        // coupon discount curve.

        Date today = context.EvaluationDate;

        double couponCurvePaymentDiscount;
        if (couponDiscountCurve is not null && paymentDate > couponDiscountCurve.ReferenceDate)
        {
            couponCurvePaymentDiscount = couponDiscountCurve.Discount(paymentDate);
        }
        else
        {
            couponCurvePaymentDiscount = 1.0;
        }

        double discountCurvePaymentDiscount;
        if (paymentDate > discountCurve.ReferenceDate)
        {
            discountCurvePaymentDiscount = discountCurve.Discount(paymentDate);
        }
        else
        {
            discountCurvePaymentDiscount = 1.0;
        }

        double couponDiscountRatio = couponCurvePaymentDiscount / discountCurvePaymentDiscount;

        double spreadLegValue = spread * accrualPeriod * discountCurvePaymentDiscount * couponDiscountRatio;

        if (!(fixingDate > today))
        {
            return new LinearTsrPricerState
            {
                Coupon = cms,
                Context = context,
                Gearing = gearing,
                Spread = spread,
                AccrualPeriod = accrualPeriod,
                FixingDate = fixingDate,
                PaymentDate = paymentDate,
                Today = today,
                DiscountCurvePaymentDiscount = discountCurvePaymentDiscount,
                CouponDiscountRatio = couponDiscountRatio,
                SpreadLegValue = spreadLegValue,
            };
        }

        Period swapTenor = swapIndex.Tenor;

        FixedVsFloatingSwap swap = swapIndex is OvernightIndexedSwapIndex on
            ? on.UnderlyingSwap(fixingDate, context)
            : swapIndex.UnderlyingSwap(fixingDate, context);
        FixedVsFloatingSwapResults results = swapIndex.UnderlyingSwapResults(swap, context);
        double swapRateValue = results.FairRate ?? throw new NanoQLException("result not available");
        double annuity = 1.0E4 * Math.Abs(results.FixedLegBPS ?? throw new NanoQLException("result not available"));
        var swapFixedLeg = swap.FixedLeg;

        SwaptionVolatilityStructure volatility = Volatility();
        SmileSection sectionTmp = volatility.SmileSection(fixingDate, swapTenor);

        double adjustedLowerBound = settings.LowerRateBound;
        double adjustedUpperBound = settings.UpperRateBound;

        if (sectionTmp.VolatilityType == VolatilityType.Normal)
        {
            // adjust lower bound if it was not set explicitly
            if (settings.DefaultBounds)
            {
                adjustedLowerBound = Math.Min(adjustedLowerBound, -adjustedUpperBound);
            }
        }
        else
        {
            // adjust bounds by section's shift
            adjustedLowerBound -= sectionTmp.Shift;
            adjustedUpperBound -= sectionTmp.Shift;
        }

        // every section is wrapped at the coupon's forward, where QuantLib keeps one with an at-the-money level of its
        // own, as S7's SABR and ZABR cubes' interpolated forward (CMR5 (xxvii))
        SmileSection smileSection = new AtmSmileSection(sectionTmp, swapRateValue);

        // compute linear model's parameters

        DayCounter volDayCounter = volatility.DayCounter;
        double gx = 0.0, gy = 0.0;
        foreach (CashFlow i in swapFixedLeg)
        {
            var c = (Coupon)i;
            double yf = c.AccrualPeriod();
            Date d = c.Date;
            double pv = yf * discountCurve.Discount(d);
            gx += pv * GsrG(volDayCounter, fixingDate, d);
            gy += pv;
        }

        double gamma = gx / gy;
        Date lastd = swapFixedLeg[^1].Date;

        double a = discountCurve.Discount(paymentDate) * (gamma - GsrG(volDayCounter, fixingDate, paymentDate))
            / ((discountCurve.Discount(lastd) * GsrG(volDayCounter, fixingDate, lastd)) + (swapRateValue * gy * gamma));

        double b = (discountCurve.Discount(paymentDate) / gy) - (a * swapRateValue);

        return new LinearTsrPricerState
        {
            Coupon = cms,
            Context = context,
            Gearing = gearing,
            Spread = spread,
            AccrualPeriod = accrualPeriod,
            FixingDate = fixingDate,
            PaymentDate = paymentDate,
            Today = today,
            DiscountCurvePaymentDiscount = discountCurvePaymentDiscount,
            CouponDiscountRatio = couponDiscountRatio,
            SpreadLegValue = spreadLegValue,
            SwapTenor = swapTenor,
            SwapRateValue = swapRateValue,
            Annuity = annuity,
            SmileSection = smileSection,
            AdjustedLowerBound = adjustedLowerBound,
            AdjustedUpperBound = adjustedUpperBound,
            A = a,
            B = b,
        };
    }

    private static double SingularTerms(LinearTsrPricerState s, OptionType type, double strike)
    {
        double omega = type == OptionType.Call ? 1.0 : -1.0;
        double s1 = Max0(omega * (s.SwapRateValue - strike)) * ((s.A * s.SwapRateValue) + s.B);
        double s2 = ((s.A * strike) + s.B)
            * s.SmileSection!.OptionPrice(strike, strike < s.SwapRateValue ? OptionType.Put : OptionType.Call);
        return s1 + s2;
    }

    private static double Integrand(LinearTsrPricerState s, double strike) =>
        2.0 * s.A * s.SmileSection!.OptionPrice(strike, strike < s.SwapRateValue ? OptionType.Put : OptionType.Call);

    // A section's vega at a strike: QuantLib's on a lognormal section; on a normal one, which QuantLib's section
    // refuses, Bachelier's derivative in the standard deviation times √T (CMR5 (xxviii)).
    private static double Vega(SmileSection section, double strike)
    {
        if (section.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            return section.Vega(strike);
        }

        double atm = section.AtmLevel()!.Value;
        return BlackFormulaFunctions.BachelierBlackFormulaStdDevDerivative(strike, atm, Math.Sqrt(section.Variance(strike)))
            * Math.Sqrt(section.ExerciseTime);
    }

    private static double StrikeFromVegaRatio(LinearTsrPricerState s, double ratio, OptionType optionType, double referenceStrike)
    {
        double a, b, min, max, k;
        SmileSection section = s.SmileSection!;
        if (optionType == OptionType.Call)
        {
            a = s.SwapRateValue;
            min = referenceStrike;
            b = max = k = Math.Min(section.MaxStrike(), s.AdjustedUpperBound);
        }
        else
        {
            a = min = k = Math.Max(section.MinStrike(), s.AdjustedLowerBound);
            b = s.SwapRateValue;
            max = referenceStrike;
        }

        double targetVega = Vega(section, s.SwapRateValue) * ratio;

        try
        {
            k = new Brent().Solve(strike => Vega(section, strike) - targetVega, 1.0E-5, (a + b) / 2.0, a, b);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // use default value set above
        }

        return Math.Min(Math.Max(k, min), max);
    }

    // QuantLib's search starts at the swap rate, the bracket's end, which Brent refuses, so every search fell back to
    // the default bound; it starts at the bracket's midpoint here, as the vega-ratio search does (CMR5 (ii)).
    private static double StrikeFromPrice(LinearTsrPricerState s, double price, OptionType optionType, double referenceStrike)
    {
        double a, b, min, max, k;
        SmileSection section = s.SmileSection!;
        if (optionType == OptionType.Call)
        {
            a = s.SwapRateValue;
            min = referenceStrike;
            b = max = k = Math.Min(section.MaxStrike(), s.AdjustedUpperBound);
        }
        else
        {
            a = min = k = Math.Max(section.MinStrike(), s.AdjustedLowerBound);
            b = s.SwapRateValue;
            max = referenceStrike;
        }

        try
        {
            k = new Brent().Solve(strike => section.OptionPrice(strike, optionType) - price, 1.0E-5, (a + b) / 2.0, a, b);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // use default value set above
        }

        return Math.Min(Math.Max(k, min), max);
    }

    private double OptionletPrice(LinearTsrPricerState s, OptionType optionType, double strike)
    {
        if (optionType == OptionType.Call && strike >= s.AdjustedUpperBound)
        {
            return 0.0;
        }

        if (optionType == OptionType.Put && strike <= s.AdjustedLowerBound)
        {
            return 0.0;
        }

        SmileSection section = s.SmileSection!;
        if (section.VolatilityType == VolatilityType.ShiftedLognormal && strike <= -section.Shift
            && s.SwapRateValue > -section.Shift)
        {
            // where QuantLib's section refuses the strike, the caplet is the expected rate less the strike and the
            // floorlet 0, the limits of the replication's own formula (CMR5 (xxiii)); on a forward at or below
            // −shift the model has no rate distribution, and QuantLib's refusal stands
            return optionType == OptionType.Call
                ? ExpectedRatePrice(s) - (strike * (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio))
                : 0.0;
        }

        // determine lower or upper integration bound (depending on option type)

        double lower = strike, upper = strike;

        switch (settings.Strategy)
        {
            case LinearTsrPricerSettingsStrategy.RateBound:
            {
                if (optionType == OptionType.Call)
                {
                    upper = s.AdjustedUpperBound;
                }
                else
                {
                    lower = s.AdjustedLowerBound;
                }

                break;
            }

            case LinearTsrPricerSettingsStrategy.VegaRatio:
            {
                // strikeFromVegaRatio ensures that returned strike is on the
                // expected side of strike
                double bound = StrikeFromVegaRatio(s, settings.VegaRatio, optionType, strike);
                if (optionType == OptionType.Call)
                {
                    upper = Math.Min(bound, s.AdjustedUpperBound);
                }
                else
                {
                    lower = Math.Max(bound, s.AdjustedLowerBound);
                }

                break;
            }

            case LinearTsrPricerSettingsStrategy.PriceThreshold:
            {
                // strikeFromPrice ensures that returned strike is on the expected
                // side of strike; QuantLib passes the vega ratio here (CMR5 (ii))
                double bound = StrikeFromPrice(s, settings.PriceThreshold, optionType, strike);
                if (optionType == OptionType.Call)
                {
                    upper = Math.Min(bound, s.AdjustedUpperBound);
                }
                else
                {
                    lower = Math.Max(bound, s.AdjustedLowerBound);
                }

                break;
            }

            default:
            {
                // BSStdDevs: each option's own bound, the lognormal width k·σ·√T, the shift subtracted once, where
                // QuantLib's sets both bounds, takes k·σ and subtracts the shift twice (CMR5 (iii))
                double atm = section.AtmLevel()!.Value;
                double atmVol = section.Volatility(atm);
                double shift = section.Shift;
                double lowerTmp, upperTmp;
                if (section.VolatilityType == VolatilityType.ShiftedLognormal)
                {
                    double width = settings.StdDevs * atmVol * Math.Sqrt(section.ExerciseTime);
                    upperTmp = ((atm + shift) * Math.Exp(width - (0.5 * atmVol * atmVol * section.ExerciseTime))) - shift;
                    lowerTmp = ((atm + shift) * Math.Exp(-width - (0.5 * atmVol * atmVol * section.ExerciseTime))) - shift;
                }
                else
                {
                    double tmp = settings.StdDevs * atmVol * Math.Sqrt(section.ExerciseTime);
                    upperTmp = atm + tmp;
                    lowerTmp = atm - tmp;
                }

                if (optionType == OptionType.Call)
                {
                    upper = Math.Min(upperTmp, s.AdjustedUpperBound);
                }
                else
                {
                    lower = Math.Max(lowerTmp, s.AdjustedLowerBound);
                }

                break;
            }
        }

        // compute the relevant integral

        double result = 0.0;
        double tmpBound;
        if (upper > lower)
        {
            tmpBound = Math.Min(upper, s.SwapRateValue);
            if (tmpBound > lower)
            {
                result += integrator.Integrate(x => Integrand(s, x), lower, tmpBound);
            }

            tmpBound = Math.Max(lower, s.SwapRateValue);
            if (upper > tmpBound)
            {
                result += integrator.Integrate(x => Integrand(s, x), tmpBound, upper);
            }

            result *= optionType == OptionType.Call ? 1.0 : -1.0;
        }

        result += SingularTerms(s, optionType, strike);

        return s.Annuity * result * s.CouponDiscountRatio * s.AccrualPeriod;
    }

    // The swaplet's price at a gearing of 1 and no spread, the fixing's own price on the model: a caplet struck at or
    // below −shift, on a forward above it, is this less the strike's price (CMR5 (xxiii)).
    private double ExpectedRatePrice(LinearTsrPricerState s)
    {
        double atmCapletPrice = OptionletPrice(s, OptionType.Call, s.SwapRateValue);
        double atmFloorletPrice = OptionletPrice(s, OptionType.Put, s.SwapRateValue);
        return (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.SwapRateValue * s.CouponDiscountRatio)
            + atmCapletPrice - atmFloorletPrice;
    }

    private double SwapletRate(LinearTsrPricerState s) =>
        SwapletPrice(s) / (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio);

    private double CapletPrice(LinearTsrPricerState s, double effectiveCap)
    {
        // caplet is equivalent to call option on fixing
        if (s.IsFixed)
        {
            // the fixing is determined
            double Rs = Max0(s.SwapIndex.Fixing(s.FixingDate, s.Context) - effectiveCap);
            double price = s.Gearing * Rs * (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio);
            return price;
        }
        else
        {
            double capletPrice = OptionletPrice(s, OptionType.Call, effectiveCap);
            return s.Gearing * capletPrice;
        }
    }

    private double CapletRate(LinearTsrPricerState s, double effectiveCap) =>
        CapletPrice(s, effectiveCap) / (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio);

    private double FloorletPrice(LinearTsrPricerState s, double effectiveFloor)
    {
        // floorlet is equivalent to put option on fixing
        if (s.IsFixed)
        {
            // the fixing is determined
            double Rs = Max0(effectiveFloor - s.SwapIndex.Fixing(s.FixingDate, s.Context));
            double price = s.Gearing * Rs * (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio);
            return price;
        }
        else
        {
            double floorletPrice = OptionletPrice(s, OptionType.Put, effectiveFloor);
            return s.Gearing * floorletPrice;
        }
    }

    private double FloorletRate(LinearTsrPricerState s, double effectiveFloor) =>
        FloorletPrice(s, effectiveFloor) / (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio);

    private double SwapletPrice(LinearTsrPricerState s)
    {
        if (s.IsFixed)
        {
            // the fixing is determined
            double Rs = s.SwapIndex.Fixing(s.FixingDate, s.Context);
            double price = ((s.Gearing * Rs) + s.Spread)
                * (s.AccrualPeriod * s.DiscountCurvePaymentDiscount * s.CouponDiscountRatio);
            return price;
        }
        else
        {
            return (s.Gearing * ExpectedRatePrice(s)) + s.SpreadLegValue;
        }
    }

    // The three rates on one state.
    private sealed class Rates(LinearTsrPricer pricer, LinearTsrPricerState s) : CouponRateView
    {
        public override double SwapletRate() => pricer.SwapletRate(s);

        public override double CapletRate(double effectiveCap) => pricer.CapletRate(s, effectiveCap);

        public override double FloorletRate(double effectiveFloor) => pricer.FloorletRate(s, effectiveFloor);
    }
}
