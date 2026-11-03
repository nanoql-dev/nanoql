// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticeuropeanvasicekengine.hpp ql/pricingengines/vanilla/analyticeuropeanvasicekengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The pricing of vanilla European options under a stochastic Vasicek interest rate: Black's formula on the Vasicek
/// bond with the total variance of the equity, the rate and their covariance (the Black-Scholes-Vasicek model,
/// http://hsrm-mathematik.de/WS201516/master/option-pricing/Black-Scholes-Vasicek-Model.pdf).
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value.
/// </para>
/// <para>
/// QuantLib reads the equity volatility at time 0 and leaves the dividend curve out
/// (<c>analyticeuropeanvasicekengine.cpp:74-91</c>), so a rising volatility curve prices on its first value and a
/// dividend yield changes nothing. Here the equity volatility is its term volatility to the exercise, its variance over
/// the rate's time, and the underlying is discounted by the dividend curve: QuantLib's value on a flat volatility with
/// no dividend, and the analytic European engine's on the Vasicek bond's rate as the rate's volatility vanishes
/// (<c>Divergences/BlackVasicekVolatilityTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticBlackVasicekEngine
{
    /// <summary>Prices a European vanilla option with a Vasicek short rate.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="blackProcess">The Black-Scholes process.</param>
    /// <param name="vasicekProcess">The Vasicek model.</param>
    /// <param name="correlation">The correlation of the equity with the short rate.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or the payoff isn't striked.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess blackProcess,
        Vasicek vasicekProcess,
        double correlation)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(blackProcess);
        ArgumentNullException.ThrowIfNull(vasicekProcess);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        var f = new CumulativeNormalDistribution();

        double t = 0;
        Date lastDate = option.Exercise.LastDate();
        double bigT = blackProcess.RiskFreeRate.DayCounter.YearFraction(blackProcess.RiskFreeRate.ReferenceDate, lastDate);
        double kappa = vasicekProcess.A();
        double sT = blackProcess.X0;
        double k = payoff.Strike;

        // the term volatility over the rate's time, where QuantLib reads the volatility at 0; the quote itself where
        // the two structures share their times
        double sigmaS = blackProcess.BlackVolatility.SharesTimes(blackProcess.RiskFreeRate)
            ? blackProcess.BlackVolatility.BlackVol(lastDate, k)
            : Math.Sqrt(blackProcess.BlackVolatility.AccruedVariance(lastDate, k) / bigT);
        double sigmaR = vasicekProcess.Sigma;
        double rT = vasicekProcess.R0;

        // the underlying net of its dividends to the exercise, which QuantLib leaves out
        double forwardSpot = sT * blackProcess.DividendYield.AccruedDiscount(lastDate);

        double zcb = vasicekProcess.DiscountBond(t, bigT, rT);
        double epsilon = payoff.OptionType == OptionType.Call ? 1 : -1;
        double upsilon = new SimpsonIntegral(1e-5, 1000).Integrate(
            u =>
            {
                double g = GK(bigT - u, kappa);
                return (sigmaS * sigmaS) + (2 * correlation * sigmaS * sigmaR * g) + (sigmaR * sigmaR * g * g);
            },
            t,
            bigT);
        double dPositive = (Math.Log(forwardSpot / k / zcb) + (upsilon / 2)) / Math.Sqrt(upsilon);
        double dNegative = (Math.Log(forwardSpot / k / zcb) - (upsilon / 2)) / Math.Sqrt(upsilon);
        double nD1 = f.Evaluate(epsilon * dPositive);
        double nD2 = f.Evaluate(epsilon * dNegative);

        double value = epsilon * ((forwardSpot * nD1) - (zcb * k * nD2));
        return option.FetchResults(new OneAssetOptionResults { NPV = value });
    }

    private static double GK(double t, double kappa) => (1 - Math.Exp(-kappa * t)) / kappa;
}
