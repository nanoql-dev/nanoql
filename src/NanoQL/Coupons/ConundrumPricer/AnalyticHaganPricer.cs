// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Coupons;

/// <summary>
/// The pricing of a CMS coupon by static replication with Hagan's analytic forms (his 3.4c, 3.5b and 3.5c): a vanilla
/// swaption's price plus a convexity term in the g-function's slope at the forward.
/// </summary>
/// <remarks>
/// <para>
/// Hagan's forms presume G = P(Tp)/A0, the payment's discount over the annuity, which the numeric pricer doesn't need,
/// since it divides by G. QuantLib's shift models' G is P/A0, so their analytic convexity term is high by P/P(Tp)
/// against the numeric twin; here it is QuantLib's term times P(Tp)/P, both on the forwarding curve. On an index with
/// an exogenous discounting curve, where the annuity and discount come from that curve and the g-function from the
/// forwarding one, every model's convexity term is normalised as the numeric pricer normalises its integrand,
/// discount·G′/G for annuity·G′. The standard and exact-yield models on one curve keep QuantLib's bits.
/// </para>
/// <para>
/// So an index given a discounting curve equal to its forwarding one prices its analytic term up to the model's own
/// approximation, 3e-4 of it, away from the same index without one.
/// </para>
/// </remarks>
public sealed class AnalyticHaganPricer : HaganPricer
{
    /// <summary>Initializes a new instance of the <see cref="AnalyticHaganPricer"/> class.</summary>
    /// <param name="swaptionVol">The swaption volatility, or <see langword="null"/>, QuantLib's empty handle, refused
    /// when a coupon with a future fixing is priced.</param>
    /// <param name="modelOfYieldCurve">The yield-curve model of the g-function.</param>
    /// <param name="meanReversion">The mean reversion, which the non-parallel shifts' model reads.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modelOfYieldCurve"/> isn't a member of its
    /// type.</exception>
    public AnalyticHaganPricer(
        SwaptionVolatilityStructure? swaptionVol,
        GFunctionFactoryYieldCurveModel modelOfYieldCurve,
        double meanReversion)
        : base(swaptionVol, modelOfYieldCurve, meanReversion)
    {
    }

    /// <inheritdoc/>
    public override AnalyticHaganPricer WithMeanReversion(double meanReversion) =>
        (AnalyticHaganPricer)base.WithMeanReversion(meanReversion);

    /// <inheritdoc/>
    public override AnalyticHaganPricer WithSwaptionVolatility(SwaptionVolatilityStructure? swaptionVolatility) =>
        (AnalyticHaganPricer)base.WithSwaptionVolatility(swaptionVolatility);

    // Hagan, 3.5b, 3.5c
    private protected override double OptionletPrice(HaganPricerState s, OptionType optionType, double strike)
    {
        double variance = s.Volatility!.BlackVariance(s.FixingDate, s.SwapTenor, s.SwapRateValue);
        double firstDerivativeOfGAtForwardValue = s.GFunction!.FirstDerivative(s.SwapRateValue);
        double price = 0.0;

        double CK = s.VanillaOptionPricer!.Evaluate(strike, optionType, s.Annuity);
        price += s.Discount / s.Annuity * CK;

        double term;
        if (s.Volatility.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            double sqrtSigma2T = Math.Sqrt(variance);
            double lnRoverK = Math.Log(s.SwapRateValue / strike);
            double d32 = (lnRoverK + (1.5 * variance)) / sqrtSigma2T;
            double d12 = (lnRoverK + (.5 * variance)) / sqrtSigma2T;
            double dminus12 = (lnRoverK - (.5 * variance)) / sqrtSigma2T;

            var cumulativeOfNormal = new CumulativeNormalDistribution();
            int sign = (int)optionType;
            double N32 = cumulativeOfNormal.Evaluate(sign * d32);
            double N12 = cumulativeOfNormal.Evaluate(sign * d12);
            double Nminus12 = cumulativeOfNormal.Evaluate(sign * dminus12);

            term = Normalised(s) is double w
                ? sign * w * s.SwapRateValue
                    * ((s.SwapRateValue * Math.Exp(variance) * N32) - ((s.SwapRateValue + strike) * N12) + (strike * Nminus12))
                : sign * firstDerivativeOfGAtForwardValue * s.Annuity * s.SwapRateValue
                    * ((s.SwapRateValue * Math.Exp(variance) * N32) - ((s.SwapRateValue + strike) * N12) + (strike * Nminus12));
        }
        else
        {
            double sqrtSigma2T = Math.Sqrt(variance);
            double d = (s.SwapRateValue - strike) / sqrtSigma2T;

            var cumulativeOfNormal = new CumulativeNormalDistribution();
            int sign = (int)optionType;

            double N = cumulativeOfNormal.Evaluate(sign * d);
            term = Normalised(s) is double w
                ? sign * w * variance * N
                : sign * firstDerivativeOfGAtForwardValue * s.Annuity * variance * N;
        }

        price += ShiftFactor(s) is double factor ? term * factor : term;

        price *= s.AccrualPeriod;
        return price;
    }

    // Hagan 3.4c
    private protected override double SwapletPrice(HaganPricerState s)
    {
        if (s.IsFixed)
        {
            // the fixing is determined
            double Rs = s.SwapIndex.Fixing(s.FixingDate, s.Context);
            double price = ((s.Gearing * Rs) + s.Spread) * (s.AccrualPeriod * s.Discount);
            return price;
        }
        else
        {
            double price = ForwardPrice(s);
            return ((s.Gearing * price) + (s.Spread * s.Discount)) * s.AccrualPeriod;
        }
    }

    private protected override double ExpectedRatePrice(HaganPricerState s) => ForwardPrice(s) * s.AccrualPeriod;

    // The discount on the coupon's own curve to the payment date over the g-function's at the swap's start, the shift
    // models' factor on one curve; null for the other models, and on an exogenous discounting curve.
    private static double? ShiftFactor(HaganPricerState s)
    {
        if (s.SwapIndex.DiscountingTermStructure is not null || s.GFunction is not GFunctionWithShifts withShifts)
        {
            return null;
        }

        YieldTermStructure forwarding = s.SwapIndex.ForwardingTermStructure!;
        return forwarding.Discount(s.PaymentDate) / withShifts.DiscountAtStart;
    }

    // discount·G′/G, the convexity term's weight in place of annuity·G′ on an exogenous discounting curve; null on one
    // curve.
    private static double? Normalised(HaganPricerState s)
    {
        if (s.SwapIndex.DiscountingTermStructure is null)
        {
            return null;
        }

        GFunction g = s.GFunction!;
        return s.Discount * g.FirstDerivative(s.SwapRateValue) / g.Evaluate(s.SwapRateValue);
    }

    // The swaplet's price per unit accrual at a gearing of 1 and no spread: the discounted forward and its convexity
    // term.
    private static double ForwardPrice(HaganPricerState s)
    {
        double variance = s.Volatility!.BlackVariance(s.FixingDate, s.SwapTenor, s.SwapRateValue);
        double firstDerivativeOfGAtForwardValue = s.GFunction!.FirstDerivative(s.SwapRateValue);
        double price = 0.0;
        price += s.Discount * s.SwapRateValue;
        double term;
        if (s.Volatility.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            term = Normalised(s) is double w
                ? w * s.SwapRateValue * s.SwapRateValue * (Math.Exp(variance) - 1.0)
                : firstDerivativeOfGAtForwardValue * s.Annuity * s.SwapRateValue * s.SwapRateValue * (Math.Exp(variance) - 1.0);
        }
        else
        {
            term = Normalised(s) is double w
                ? w * variance
                : firstDerivativeOfGAtForwardValue * s.Annuity * variance;
        }

        price += ShiftFactor(s) is double factor ? term * factor : term;
        return price;
    }
}
