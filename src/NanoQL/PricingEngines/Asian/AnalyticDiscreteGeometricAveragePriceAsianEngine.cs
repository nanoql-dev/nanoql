// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/analytic_discr_geom_av_price.hpp ql/pricingengines/asian/analytic_discr_geom_av_price.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for discrete geometric average-price Asian options: the geometric average of log-normal fixings
/// is log-normal, priced by Black's formula on its forward and variance (Clewlow and Strickland, "Implementing
/// Derivatives Models", 1998, pp. 118-123).
/// </summary>
/// <remarks>
/// <para>
/// The value and the Greeks QuantLib sets. It also prices an arithmetic option, as the geometric control of the
/// arithmetic Monte Carlo engine, reading no past fixing, as QuantLib's does.
/// </para>
/// <para>
/// Documented divergences. The average is priced exactly under deterministic term structures: ln G is normal with mean
/// (1/N)(the past fixings' log-sum + Σ[ln F(tᵢ) − V(tᵢ)/2]) and variance (1/N²)ΣΣV(min(tᵢ, tⱼ)) over the future
/// fixings, F the forward and V the Black variance at the strike, each read by date, where QuantLib reads one
/// volatility at the exercise date and the zero rates to it for every fixing, +37.4% and −18.9% off on shaped markets
/// (<c>Divergences/GeometricAsianTermStructureTests.cs</c>). The Greeks are the form's: vega under a parallel shift of
/// the volatilities, rho and dividend rho under parallel shifts of the curves, each on its own structure's clock; on a
/// flat market they are QuantLib's. A running product that has left double's range is read through the past fixings'
/// log-sum, where QuantLib prices ∞ (<c>Divergences/GeometricAsianProductRangeTests.cs</c>).
/// </para>
/// <para>
/// A fixing dated from the evaluation date to the clock's origin, where the structures settle after the evaluation
/// date, is taken at the origin, the spot with no variance and no discount accrued, so as a past fixing at the spot
/// (<c>Divergences/AsianLateSettlementTests.cs</c>), where QuantLib drops it from the average.
/// </para>
/// </remarks>
public static class AnalyticDiscreteGeometricAveragePriceAsianEngine
{
    /// <summary>Prices a discrete geometric average-price Asian option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value, delta, gamma, vega, rho, dividend rho, theta and strike
    /// sensitivity; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the spot isn't positive, or the option's past
    /// fixings fail its checks.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        Date origin = process.EarliestReferenceDate();
        DiscreteAveragingAsianArguments arguments = option.Arguments(context).AtOrigin(
            context.EvaluationDate, d => d < origin, process.StateVariable);
        return option.FetchResults(Body(option, arguments, process));
    }

    // QuantLib's calculate() on the option's arguments, the arithmetic Monte Carlo's control route too
    internal static OneAssetOptionResults Body(
        DiscreteAveragingAsianOption option, DiscreteAveragingAsianArguments arguments, GeneralizedBlackScholesProcess process)
    {
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        double runningLog;
        int pastFixings;
        if (arguments.AverageType == AverageType.Geometric)
        {
            runningLog = arguments.RunningLog;
            pastFixings = arguments.PastFixings;
        }
        else
        {
            // it is being used as control variate
            runningLog = 1.0;
            pastFixings = 0;
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double s = process.StateVariable;
        if (!(s > 0.0))
        {
            throw new NanoQLException("positive underlying value required");
        }

        YieldTermStructure riskFree = process.RiskFreeRate;
        YieldTermStructure dividends = process.DividendYield;
        BlackVolTermStructure volatility = process.BlackVolatility;
        Date origin = process.EarliestReferenceDate();
        double strike = payoff.Strike;

        // the future fixings, on or after the earliest structure's date, and what each reads by date
        int remainingFixings = 0;
        foreach (Date d in arguments.FixingDates)
        {
            if (d >= origin)
            {
                ++remainingFixings;
            }
        }

        int numberOfFixings = pastFixings + remainingFixings;
        double n = numberOfFixings;
        double pastWeight = pastFixings / n;
        double futureWeight = 1.0 - pastWeight;

        // Σ[ln F − V/2], ΣΣV(min), and their sensitivities to a parallel shift of the volatilities and of each curve
        double meanSum = 0.0, varianceSum = 0.0;
        double dMeanSumVol = 0.0, dVarianceSumVol = 0.0;
        double riskFreeTimeSum = 0.0, dividendTimeSum = 0.0;
        int k = 0;
        foreach (Date d in arguments.FixingDates)
        {
            if (!(d >= origin))
            {
                continue;
            }

            double forward = s * dividends.AccruedDiscount(d) / riskFree.AccruedDiscount(d);
            double v = volatility.AccruedVariance(d, strike);
            double tv = volatility.AccrualTimeTo(d);
            double dv = tv > 0.0 ? 2.0 * volatility.BlackVol(d, strike) * tv : 0.0;

            // V(min(tᵢ, tⱼ)) over the sorted fixings: the k-th's own term once and twice for each fixing after it
            double weight = 1.0 + (2.0 * (remainingFixings - 1 - k));
            meanSum += Math.Log(forward) - (0.5 * v);
            varianceSum += weight * v;
            dMeanSumVol -= 0.5 * dv;
            dVarianceSumVol += weight * dv;
            riskFreeTimeSum += riskFree.AccrualTimeTo(d);
            dividendTimeSum += dividends.AccrualTimeTo(d);
            ++k;
        }

        double variance = varianceSum / n / n;
        double muG = ((pastFixings > 0 ? runningLog : 0.0) + meanSum) / n;
        double forwardPrice = Math.Exp(muG + (variance / 2.0));

        Date exDate = option.Exercise.LastDate();
        double riskFreeDiscount = riskFree.AccruedDiscount(exDate);

        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(variance), riskFreeDiscount);

        double value = black.Value;
        double delta = futureWeight * black.Delta(forwardPrice) * forwardPrice / s;
        double gamma = forwardPrice * futureWeight / (s * s)
            * ((black.Gamma(forwardPrice) * futureWeight * forwardPrice) - (pastWeight * black.Delta(forwardPrice)));

        // the price's sensitivity to the mean of ln G and to its variance: F ∂V/∂F, and ∂V/∂σ_G through σ_G = √variance
        double dPrice = black.Delta(forwardPrice) * forwardPrice;
        double dMean = dMeanSumVol / n;
        double dVariance = dVarianceSumVol / n / n;
        double sigG = Math.Sqrt(variance);
        double vega = dPrice * (dMean + (dVariance / 2.0));
        if (sigG > NumericLimits.QL_EPSILON)
        {
            vega += black.Vega(1.0) * dVariance / (2.0 * sigG);
        }

        double rho = (dPrice * riskFreeTimeSum / n) - (riskFree.AccrualTimeTo(exDate) * value);
        double dividendRho = -dPrice * dividendTimeSum / n;

        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            Gamma = gamma,
            Vega = vega,
            Rho = rho,
            DividendRho = dividendRho,
            StrikeSensitivity = black.StrikeSensitivity,
            Theta = GreeksFunctions.BlackScholesTheta(process, value, delta, gamma, exDate),
        };
    }
}
