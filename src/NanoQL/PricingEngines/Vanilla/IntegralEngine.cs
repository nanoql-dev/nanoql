// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/integralengine.hpp ql/pricingengines/vanilla/integralengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The pricing engine for European vanilla options by integrating the payoff against the lognormal density of the
/// underlying at expiry, by the segment rule on 5,000 intervals over ten standard deviations each side.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value. Each structure is read at the exercise date by date, nothing accrued before its own
/// reference date (the mismatched-dates class), where QuantLib's reads throw.
/// </para>
/// <para>
/// At zero variance QuantLib's bounds collapse and its density divides by zero (<c>integralengine.cpp:78-83</c>), NaN;
/// here the value is the payoff at the forward, discounted, the density's limit. A documented divergence (the S-van
/// plan's VR4 (x), <c>Divergences/IntegralEngineZeroVarianceTests.cs</c>).
/// </para>
/// </remarks>
public static class IntegralEngine
{
    /// <summary>Prices a European vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or the payoff isn't striked.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        // each structure read by date, nothing accrued before its reference date (the mismatched-dates class)
        Date lastDate = option.Exercise.LastDate();
        double variance = process.BlackVolatility.AccruedVariance(lastDate, payoff.Strike);
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
        double s0 = process.StateVariable;
        double drift = Math.Log(dividendDiscount / riskFreeDiscount) - (0.5 * variance);

        double value;
        if (!(variance > 0.0))
        {
            // the density's limit, where QuantLib divides by the variance (the S-van plan's VR4 (x))
            value = riskFreeDiscount * payoff.Evaluate(s0 * dividendDiscount / riskFreeDiscount);
        }
        else
        {
            double F(double x)
            {
                double temp = s0 * Math.Exp(x);
                double result = payoff.Evaluate(temp);
                return result * Math.Exp(-(x - drift) * (x - drift) / (2.0 * variance));
            }

            var integrator = new SegmentIntegral(5000);

            double infinity = 10.0 * Math.Sqrt(variance);
            value = riskFreeDiscount / Math.Sqrt(2.0 * Math.PI * variance)
                * integrator.Integrate(F, drift - infinity, drift + infinity);
        }

        return option.FetchResults(new OneAssetOptionResults { NPV = value });
    }
}
