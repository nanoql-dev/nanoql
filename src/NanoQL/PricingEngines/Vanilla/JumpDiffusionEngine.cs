// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/jumpdiffusionengine.hpp ql/pricingengines/vanilla/jumpdiffusionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.PricingEngines;

/// <summary>
/// The jump-diffusion engine for vanilla options (Merton 1976): the Poisson-weighted sum of Black-Scholes prices
/// conditional on the number of jumps, each on a constant rate and volatility.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma, theta, vega, rho and dividend rho. The correctness of the returned
/// value is tested by reproducing results available in literature, and of the Greeks by reproducing numerical
/// derivatives.
/// </para>
/// <para>
/// QuantLib builds each term's flat curve and constant volatility on the risk-free curve's reference date with the
/// volatility's time from its own (<c>jumpdiffusionengine.cpp:62-67</c>, <c>:113-114</c>); here both are built on the
/// volatility's reference date, so the time and the structures agree: QuantLib's where the two dates coincide
/// (<c>Divergences/JumpDiffusionReferenceDateTests.cs</c>). The terms' curve runs over the volatility's time, so their
/// rho is per unit of its rate; the engine's rho is per unit of the risk-free curve's own rate over its own time, and
/// theta's risk-free term runs over that time, as the analytic European engine's: QuantLib's where the two times agree.
/// With no diffusive volatility the no-jump term's vega weight √(variance/t)/v and its theta's jump term are 0/0
/// (<c>:110</c>, <c>:123-127</c>); here they are their limits, 1 and 0, the values they take at any positive volatility
/// (<c>Divergences/JumpDiffusionZeroVolatilityTests.cs</c>).
/// </para>
/// </remarks>
public static class JumpDiffusionEngine
{
    /// <summary>Prices a vanilla option on a Merton-76 process.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Merton-76 process.</param>
    /// <param name="relativeAccuracy">The relative accuracy of the sum's last term.</param>
    /// <param name="maxIterations">The most terms.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, the base engine refuses, or the sum doesn't reach the accuracy within the terms.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        Merton76Process process,
        double relativeAccuracy = 1e-4,
        int maxIterations = 100)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        double jumpSquareVol = process.LogJumpVolatility * process.LogJumpVolatility;
        double muPlusHalfSquareVol = process.LogMeanJump + (0.5 * jumpSquareVol);

        // mean jump size
        double k = Math.Exp(muPlusHalfSquareVol) - 1.0;
        double lambda = (k + 1.0) * process.JumpIntensity;

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        Date lastDate = option.Exercise.LastDate();
        double variance = process.BlackVolatility.BlackVariance(lastDate, payoff.Strike);

        DayCounter voldc = process.BlackVolatility.DayCounter;
        NanoQL.Time.Calendar? volcal = process.BlackVolatility.Calendar;
        Date volRefDate = process.BlackVolatility.ReferenceDate;
        double t = voldc.YearFraction(volRefDate, lastDate);
        double riskFreeRate = -Math.Log(process.RiskFreeRate.Discount(lastDate)) / t;

        var p = new PoissonDistribution(lambda * t);

        double value = 0.0, delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0, rho = 0.0, dividendRho = 0.0;
        bool thetaKnown = true;

        double r, v, weight, lastContribution = 1.0;
        int i;
        double thetaCorrection;

        // Haug arbitrary criterium is:
        // for (i=0; i<11; i++) {
        for (i = 0; (lastContribution > relativeAccuracy && i < maxIterations) || i < (int)(lambda * t); i++)
        {
            // constant vol/rate assumption. It should be relaxed
            v = Math.Sqrt((variance + (i * jumpSquareVol)) / t);
            r = riskFreeRate - (process.JumpIntensity * k) + (i * muPlusHalfSquareVol / t);

            // on the volatility's reference date, where QuantLib takes the risk-free curve's
            var bsProcess = new GeneralizedBlackScholesProcess(
                process.StateVariable,
                process.DividendYield,
                new FlatForward(volRefDate, r, voldc),
                new BlackConstantVol(volRefDate, volcal, v, voldc));
            OneAssetOptionResults baseResults = AnalyticEuropeanEngine.Body(option, bsProcess, bsProcess.RiskFreeRate);

            double baseValue = baseResults.NPV!.Value;
            double baseDelta = baseResults.Delta!.Value;
            double baseGamma = baseResults.Gamma!.Value;
            double baseVega = baseResults.Vega!.Value;
            double baseRho = baseResults.Rho!.Value;
            double baseDividendRho = baseResults.DividendRho!.Value;
            double? baseTheta = baseResults.Theta;

            weight = p.Evaluate(i);
            value += weight * baseValue;
            delta += weight * baseDelta;
            gamma += weight * baseGamma;

            // the no-jump term's weight is 1 at every positive volatility, and its limit at none
            vega += weight * (i == 0 ? 1.0 : Math.Sqrt(variance / t) / v) * baseVega;

            // theta modified
            thetaCorrection = (i == 0 ? 0.0 : baseVega * (i * jumpSquareVol / (2.0 * v * t * t)))
                + (baseRho * i * muPlusHalfSquareVol / (t * t));
            if (baseTheta is double bt)
            {
                theta += weight * (bt + thetaCorrection + (lambda * baseValue));
                if (i != 0)
                {
                    theta -= p.Evaluate(i - 1) * lambda * baseValue;
                }
            }
            else
            {
                thetaKnown = false;
            }

            // end theta calculation
            rho += weight * baseRho;
            dividendRho += weight * baseDividendRho;

            lastContribution = Math.Abs(baseValue / (Math.Abs(value) > NumericLimits.QL_EPSILON ? value : 1.0));

            lastContribution = Max(
                lastContribution, Math.Abs(baseDelta / (Math.Abs(delta) > NumericLimits.QL_EPSILON ? delta : 1.0)));

            lastContribution = Max(
                lastContribution, Math.Abs(baseGamma / (Math.Abs(gamma) > NumericLimits.QL_EPSILON ? gamma : 1.0)));

            if (baseTheta is double tb)
            {
                lastContribution = Max(
                    lastContribution, Math.Abs(tb / (Math.Abs(theta) > NumericLimits.QL_EPSILON ? theta : 1.0)));
            }

            lastContribution = Max(
                lastContribution, Math.Abs(baseVega / (Math.Abs(vega) > NumericLimits.QL_EPSILON ? vega : 1.0)));

            lastContribution = Max(
                lastContribution, Math.Abs(baseRho / (Math.Abs(rho) > NumericLimits.QL_EPSILON ? rho : 1.0)));

            lastContribution = Max(
                lastContribution,
                Math.Abs(baseDividendRho / (Math.Abs(dividendRho) > NumericLimits.QL_EPSILON ? dividendRho : 1.0)));

            lastContribution *= weight;
        }

        if (!(i < maxIterations))
        {
            throw new NanoQLException(
                $"{i} iterations have been not enough to reach the required {Show(relativeAccuracy)} accuracy. The "
                + $"{DataFormattersFunctions.Ordinal(i)} addendum was {Show(lastContribution)} while the running sum was "
                + $"{Show(value)}");
        }

        // the risk-free curve's own rate moves the terms' rate by its time over the volatility's; its term in theta
        // runs over its own time (the mismatched-dates class), none where it accrues nothing
        double riskFreeTime = process.RiskFreeRate.AccrualTimeTo(lastDate);
        double riskFreeTerm = riskFreeTime > 0.0 ? riskFreeRate * ((1.0 / riskFreeTime) - (1.0 / t)) * rho : 0.0;

        var results = new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            Gamma = gamma,
            Theta = thetaKnown ? theta - riskFreeTerm : null,
            Vega = vega,
            Rho = rho * (riskFreeTime / t),
            DividendRho = dividendRho,
        };
        return option.FetchResults(results);
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
