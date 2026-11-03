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
/// The pricing of a CMS coupon by static replication, as in Hagan's "Conundrums..." article, by numerical integration
/// over the prices of vanilla swaptions.
/// </summary>
/// <remarks>
/// QuantLib's optionlet price sets the pricer's integration limits, which <c>upperLimit()</c>, <c>lowerLimit()</c> and
/// <c>stdDeviations()</c> read afterwards; here <see cref="IntegrationLimits"/> computes them from the arguments of the
/// optionlet price that sets them.
/// </remarks>
public sealed class NumericHaganPricer : HaganPricer
{
    private const double RequiredStdDeviations = 8;

    private readonly double lowerLimit;
    private readonly double upperLimit;
    private readonly double precision;
    private readonly double hardUpperLimit;

    /// <summary>Initializes a new instance of the <see cref="NumericHaganPricer"/> class.</summary>
    /// <param name="swaptionVol">The swaption volatility, or <see langword="null"/>, QuantLib's empty handle, refused
    /// when a coupon with a future fixing is priced.</param>
    /// <param name="modelOfYieldCurve">The yield-curve model of the g-function.</param>
    /// <param name="meanReversion">The mean reversion, which the non-parallel shifts' model reads.</param>
    /// <param name="lowerLimit">The integration's lower limit on a lognormal volatility.</param>
    /// <param name="upperLimit">The upper limit <see cref="IntegrationLimits"/> gives before a call integrates.</param>
    /// <param name="precision">The integration's absolute accuracy.</param>
    /// <param name="hardUpperLimit">The integration's largest upper limit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modelOfYieldCurve"/> isn't a member of its
    /// type.</exception>
    public NumericHaganPricer(
        SwaptionVolatilityStructure? swaptionVol,
        GFunctionFactoryYieldCurveModel modelOfYieldCurve,
        double meanReversion,
        double lowerLimit = 0.0,
        double upperLimit = 1.0,
        double precision = 1.0e-6,
        double hardUpperLimit = double.MaxValue)
        : base(swaptionVol, modelOfYieldCurve, meanReversion)
    {
        this.lowerLimit = lowerLimit;
        this.upperLimit = upperLimit;
        this.precision = precision;
        this.hardUpperLimit = hardUpperLimit;
    }

    /// <inheritdoc/>
    public override NumericHaganPricer WithMeanReversion(double meanReversion) =>
        (NumericHaganPricer)base.WithMeanReversion(meanReversion);

    /// <inheritdoc/>
    public override NumericHaganPricer WithSwaptionVolatility(SwaptionVolatilityStructure? swaptionVolatility) =>
        (NumericHaganPricer)base.WithSwaptionVolatility(swaptionVolatility);

    /// <summary>
    /// The integration limits QuantLib's members hold after the coupon's optionlet of the given type is priced on a
    /// fresh pricer: <c>lowerLimit()</c>, <c>upperLimit()</c> and <c>stdDeviations()</c>.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="optionType">The optionlet's type: a call sets the upper limit, a put the lower one.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>
    /// The limits: a call's upper limit the forward at 8 standard deviations, a put's lower one the constructor's on a
    /// lognormal volatility and the forward less 8 normal standard deviations otherwise, the other limit the
    /// constructor's; a coupon whose fixing is known integrates nothing, so the constructor's limits and no number of
    /// standard deviations, where QuantLib's member is still unset.
    /// </returns>
    /// <exception cref="ArgumentException">The coupon isn't a CMS coupon.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">The coupon has no accrual period, or a curve or the volatility is
    /// missing.</exception>
    public NumericHaganPricerIntegrationLimits IntegrationLimits(
        FloatingRateCoupon coupon, OptionType optionType, EvaluationContext context)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        HaganPricerState s = Initialize(coupon, context);
        if (s.IsFixed)
        {
            return new NumericHaganPricerIntegrationLimits(lowerLimit, upperLimit, null);
        }

        return optionType == OptionType.Call
            ? new NumericHaganPricerIntegrationLimits(lowerLimit, ResetUpperLimit(s, RequiredStdDeviations), RequiredStdDeviations)
            : new NumericHaganPricerIntegrationLimits(ResetLowerLimit(s, RequiredStdDeviations), upperLimit, RequiredStdDeviations);
    }

    private protected override double OptionletPrice(HaganPricerState s, OptionType optionType, double strike)
    {
        var integrand = new ConundrumIntegrand(
            s.VanillaOptionPricer!, s.GFunction!, s.FixingDate, s.PaymentDate, s.Annuity, s.SwapRateValue, strike, optionType);
        double stdDeviationsForUpperLimit = RequiredStdDeviations;
        double stdDeviationsForLowerLimit = RequiredStdDeviations;
        double a, b, integralValue;
        if (optionType == OptionType.Call)
        {
            double upper = ResetUpperLimit(s, stdDeviationsForUpperLimit);
            integralValue = Integrate(s, strike, upper, integrand);
        }
        else
        {
            double lower = ResetLowerLimit(s, stdDeviationsForLowerLimit);
            a = Math.Min(strike, lower);
            b = strike;
            integralValue = Integrate(s, a, b, integrand);
        }

        double dFdK = integrand.FirstDerivativeOfF(strike);
        double swaptionPrice = s.VanillaOptionPricer!.Evaluate(strike, optionType, s.Annuity);

        // v. HAGAN, Conundrums..., formule 2.17a, 2.18a
        return s.AccrualPeriod * (s.Discount / s.Annuity)
            * (((1 + dFdK) * swaptionPrice) + ((int)optionType * integralValue));
    }

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
            return (s.Gearing * ExpectedRatePrice(s)) + s.SpreadLegValue;
        }
    }

    private protected override double ExpectedRatePrice(HaganPricerState s)
    {
        double atmCapletPrice = OptionletPrice(s, OptionType.Call, s.SwapRateValue);
        double atmFloorletPrice = OptionletPrice(s, OptionType.Put, s.SwapRateValue);
        return (s.AccrualPeriod * s.Discount * s.SwapRateValue) + atmCapletPrice - atmFloorletPrice;
    }

    // a + (b − a)·x^k, the variable change of the non-adaptive rule on a wide interval, with its Jacobian.
    private static double VariableChange(ConundrumIntegrand f, double a, double width, int k, double x)
    {
        double newVar;
        double temp = width;
        for (int i = 1; i < k; ++i)
        {
            temp *= x;
        }

        newVar = a + (x * temp);
        return f.Evaluate(newVar) * k * temp;
    }

    private double Integrate(HaganPricerState s, double a, double b, ConundrumIntegrand integrand)
    {
        double result = .0;

        // we use the non adaptive algorithm only for semi infinite interval
        if (a > 0)
        {
            // we estimate the actual boundary by testing integrand values
            double upperBoundary = 2 * a;
            while (integrand.Evaluate(upperBoundary) > precision)
            {
                upperBoundary *= 2.0;
            }

            // sometimes b < a because of a wrong estimation of b based on stdev
            if (b > a)
            {
                upperBoundary = Math.Min(upperBoundary, b);
            }

            var gaussKronrodNonAdaptive = new GaussKronrodNonAdaptive(precision, 1000000, 1.0);

            // if the integration intervall is wide enough we use the
            // following change variable x -> a + (b-a)*(t/(a-b))^3
            upperBoundary = Math.Max(a, Math.Min(upperBoundary, hardUpperLimit));
            IntegrationResult r;
            if (upperBoundary > 2 * a)
            {
                const int k = 3;
                double width = upperBoundary - a;
                r = gaussKronrodNonAdaptive.IntegrateWithStatistics(x => VariableChange(integrand, a, width, k, x), .0, 1.0);
            }
            else
            {
                r = gaussKronrodNonAdaptive.IntegrateWithStatistics(integrand.Evaluate, a, upperBoundary);
            }

            result = r.Value;

            // if the expected precision has not been reached we use the old algorithm
            if (!gaussKronrodNonAdaptive.IntegrationSuccess(r))
            {
                var integrator = new GaussKronrodAdaptive(precision, 100000);
                b = Math.Max(a, Math.Min(b, hardUpperLimit));
                result = integrator.Integrate(integrand.Evaluate, a, b);
            }
        }
        else
        {
            // if a < b we use the old algorithm
            b = Math.Max(a, Math.Min(b, hardUpperLimit));
            if (s.Volatility!.VolatilityType == VolatilityType.ShiftedLognormal)
            {
                var integrator = new GaussKronrodAdaptive(precision, 100000);
                result = integrator.Integrate(integrand.Evaluate, a, b);
            }
            else
            {
                // Normal and floorlet
                var integrator = new GaussKronrodNonAdaptive(precision, 100000, 1.0);

                // Doing an integral from -inf to strike, where strike itself is negative,
                // the GaussKronrodAdaptive quadrature rule throws an exceptions in
                // GaussKronrodAdaptive::integrateRecursively due to maximum number of
                // function evaluations exceed.
                result = integrator.Integrate(integrand.Evaluate, a, b);
            }
        }

        return result;
    }

    private static double ResetUpperLimit(HaganPricerState s, double stdDeviationsForUpperLimit)
    {
        double variance = s.Volatility!.BlackVariance(s.FixingDate, s.SwapTenor, s.SwapRateValue);

        if (s.Volatility.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            return s.SwapRateValue * Math.Exp(stdDeviationsForUpperLimit * Math.Sqrt(variance));
        }
        else
        {
            return s.SwapRateValue + (stdDeviationsForUpperLimit * Math.Sqrt(variance));
        }
    }

    private double ResetLowerLimit(HaganPricerState s, double stdDeviationsForUpperLimit)
    {
        double variance = s.Volatility!.BlackVariance(s.FixingDate, s.SwapTenor, s.SwapRateValue);

        if (s.Volatility.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            // QuantLib returns its lowerLimit_ member, which on a lognormal volatility only the constructor sets
            return lowerLimit;
        }
        else
        {
            return s.SwapRateValue - (stdDeviationsForUpperLimit * Math.Sqrt(variance));
        }
    }
}
