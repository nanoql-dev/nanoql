// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/analytic_cont_geom_av_price.hpp ql/pricingengines/asian/analytic_cont_geom_av_price.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for continuous geometric average-price Asian options, averaging from the evaluation date: the
/// continuous geometric average of a log-normal process is log-normal (Haug, "Option Pricing Formulas", 1998,
/// pp. 96-97).
/// </summary>
/// <remarks>
/// <para>
/// The value and the Greeks QuantLib sets; a seasoned option, whose averaging began before, is refused, as QuantLib
/// refuses it.
/// </para>
/// <para>
/// Documented divergences (the S-bar plan's BR4 (a), BR5 (xxvi)). The average is priced exactly under deterministic
/// term structures: ln G is normal with mean (1/T)∫[ln F(t) − V(t)/2]dt and variance (2/T²)∫V(t)(T − t)dt over the
/// volatility's clock, F the forward and V the Black variance at the strike, where QuantLib reads one volatility and the
/// zero rates at the exercise date, +39.4% and −19.2% off on shaped markets
/// (<c>Divergences/GeometricAsianTermStructureTests.cs</c>). The integrals run on NanoQL's adaptive Kronrod rule, each
/// at R7 on its own scale (SD24): the mean on ∫(|ln S| + |ln F/S| + V/2)dt, the variance and the Greeks' integrals on a
/// first 15-point estimate. The Greeks are the form's: vega under a parallel shift of the volatilities, rho and dividend
/// rho under parallel shifts of the curves, each on its own structure's clock; on a flat market they are QuantLib's.
/// </para>
/// </remarks>
public static class AnalyticContinuousGeometricAveragePriceAsianEngine
{
    // SD24's relative tolerance and the rule's safety cap
    private const double Tolerance = 1e-12;
    private const int Cap = 100000;

    /// <summary>Prices a continuous geometric average-price Asian option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value, delta, gamma, vega, rho, dividend rho and theta (null where the
    /// calculator refuses it); the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't geometric, the exercise isn't European, the option is seasoned, the payoff isn't plain
    /// vanilla, or the spot isn't positive.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousAveragingAsianOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process));
    }

    private static OneAssetOptionResults Body(ContinuousAveragingAsianOption option, GeneralizedBlackScholesProcess process)
    {
        if (!(option.AverageType == AverageType.Geometric))
        {
            throw new NanoQLException("not a geometric average option");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        // Check if this is a seasoned option
        if (option.StartDate is not null)
        {
            throw new NanoQLException(
                "seasoned continuous geometric Asian options not yet supported - "
                + "requires adjustment of forward price and variance based on "
                + "accumulated geometric average. This feature needs mathematical "
                + "review and implementation.");
        }

        Date exercise = option.Exercise.LastDate();
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double spot = process.StateVariable;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying");
        }

        YieldTermStructure riskFree = process.RiskFreeRate;
        YieldTermStructure dividends = process.DividendYield;
        BlackVolTermStructure volatility = process.BlackVolatility;
        double strike = payoff.Strike;
        double t = volatility.AccrualTimeTo(exercise);
        double riskFreeDiscount = riskFree.AccruedDiscount(exercise);
        double lnSpot = Math.Log(spot);

        // ln F/S at the volatility's time u, each curve read at the instant u denotes on the volatility's clock
        double LogCarry(double u) => Math.Log(dividends.DiscountAt(volatility, u, true) / riskFree.DiscountAt(volatility, u, true));
        double Variance(double u) => u > 0.0 ? volatility.BlackVariance(u, strike, true) : 0.0;
        double DVariance(double u) => u > 0.0 ? 2.0 * volatility.BlackVol(u, strike, true) * u : 0.0;
        double CurveTime(TermStructure curve, double u) => curve.SharesTimes(volatility) ? u : curve.AccrualTime(volatility, u);

        double mean = lnSpot + (IntegrateOnScale(
            u => LogCarry(u) - (0.5 * Variance(u)),
            u => Math.Abs(lnSpot) + Math.Abs(LogCarry(u)) + (0.5 * Variance(u)),
            t) / t);
        double variance = 2.0 / (t * t) * GaussKronrodAdaptive.IntegrateOnFirstEstimate(u => Variance(u) * (t - u), 0.0, t, Tolerance, Cap);
        double forward = Math.Exp(mean + (variance / 2.0));

        var black = new BlackCalculator(payoff, forward, Math.Sqrt(variance), riskFreeDiscount);

        double value = black.Value;

        // the price's sensitivity to the mean of ln G and to its variance, through σ_G = √variance
        double dPrice = black.Delta(forward) * forward;
        double dMean = -0.5 * GaussKronrodAdaptive.IntegrateOnFirstEstimate(DVariance, 0.0, t, Tolerance, Cap) / t;
        double dVariance = 2.0 / (t * t) * GaussKronrodAdaptive.IntegrateOnFirstEstimate(u => DVariance(u) * (t - u), 0.0, t, Tolerance, Cap);
        double sigG = Math.Sqrt(variance);
        double vega = dPrice * (dMean + (dVariance / 2.0));
        if (sigG > NumericLimits.QL_EPSILON)
        {
            vega += black.Vega(1.0) * dVariance / (2.0 * sigG);
        }

        double riskFreeTime = GaussKronrodAdaptive.IntegrateOnFirstEstimate(u => CurveTime(riskFree, u), 0.0, t, Tolerance, Cap) / t;
        double dividendTime = GaussKronrodAdaptive.IntegrateOnFirstEstimate(u => CurveTime(dividends, u), 0.0, t, Tolerance, Cap) / t;

        double? theta;
        try
        {
            theta = black.Theta(spot, t);
        }
        catch (NanoQLException)
        {
            theta = null;
        }

        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = black.Delta(spot),
            Gamma = black.Gamma(spot),
            DividendRho = -dPrice * dividendTime,
            Rho = (dPrice * riskFreeTime) - (riskFree.AccrualTimeTo(exercise) * value),
            Vega = vega,
            Theta = theta,
        };
    }

    // ∫₀ᵗ f on a scale ∫₀ᵗ g ≥ 0 (SD24): f/scale at the relative tolerance, times the scale; 0 where the scale is 0
    private static double IntegrateOnScale(Func<double, double> f, Func<double, double> scaleIntegrand, double t)
    {
        double scale = new GaussKronrodAdaptive(double.MaxValue, 15).Integrate(scaleIntegrand, 0.0, t);
        if (scale == 0.0)
        {
            return 0.0;
        }

        return new GaussKronrodAdaptive(Tolerance, Cap).Integrate(u => f(u) / scale, 0.0, t) * scale;
    }
}
