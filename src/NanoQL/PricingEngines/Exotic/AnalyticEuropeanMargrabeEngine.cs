// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticeuropeanmargrabeengine.hpp ql/pricingengines/exotic/analyticeuropeanmargrabeengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a European Margrabe option: W. Margrabe, "The Value of an Option to Exchange One Asset for
/// Another", Journal of Finance 33 (1978), 177-186; with E. G. Haug, "The Complete Guide to Option Pricing Formulas",
/// 2nd edition (2007), for the Greeks.
/// </summary>
/// <remarks>
/// <para>
/// The value, each asset's delta and gamma, and the theta; the rho is 0 and the other Greeks are left null, as
/// QuantLib's. Each gamma takes its asset's quantity once and the theta's carry terms each once, where QuantLib's
/// gammas drop the quantities and its theta takes them squared (<c>Divergences/MargrabeGreeksQuantityTests.cs</c>).
/// </para>
/// <para>
/// Each forward reads its own process's curves and the payoff is discounted on the first process's risk-free curve,
/// where QuantLib builds the second forward on the first process's discount; the theta reads the second asset's carry
/// on the first process's curve (its yield plus the first rate less the second), as QuantLib's engine on that market
/// restated (<c>Divergences/TwoAssetTwoRiskFreeCurvesTests.cs</c>). The structures are read by date, accruing nothing
/// before their dates, and the theta runs on the clock across both processes
/// (<see cref="FdmBlackScholesOp.ClockAcross"/>), the first process's risk-free curve where every structure shares its
/// times, so a market on one clock keeps QuantLib's arithmetic (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticEuropeanMargrabeEngine
{
    private static readonly CumulativeNormalDistribution Cum = new();
    private static readonly NormalDistribution Norm = new();

    /// <summary>Prices a European Margrabe option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <returns>The option's results: the value, the assets' deltas and gammas, the theta and the rho.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A quantity isn't positive, or the exercise isn't European.</exception>
    public static MargrabeOptionResults Calculate(
        MargrabeOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process1);
        ArgumentNullException.ThrowIfNull(process2);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process1, process2, correlation));
    }

    // QuantLib's calculate().
    private static MargrabeOptionResults Body(
        MargrabeOption option,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double rho)
    {
        if (option.Exercise.Type != ExerciseType.European)
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.Payoff is not NullPayoff)
        {
            throw new NanoQLException("non a Null Payoff type");
        }

        int quantity1 = option.Q1;
        int quantity2 = option.Q2;

        double s1 = process1.X0;
        double s2 = process2.X0;
        Date lastDate = exercise.LastDate();

        double variance1 = process1.BlackVolatility.AccruedVariance(lastDate, s1);
        double variance2 = process2.BlackVolatility.AccruedVariance(lastDate, s2);

        double riskFreeDiscount = process1.RiskFreeRate.AccruedDiscount(lastDate);
        double riskFreeDiscount2 = process2.RiskFreeRate.AccruedDiscount(lastDate);

        double dividendDiscount1 = process1.DividendYield.AccruedDiscount(lastDate);
        double dividendDiscount2 = process2.DividendYield.AccruedDiscount(lastDate);

        // each forward on its own process's curves
        double forward1 = process1.X0 * dividendDiscount1 / riskFreeDiscount;
        double forward2 = process2.X0 * dividendDiscount2 / riskFreeDiscount2;

        double stdDev1 = Math.Sqrt(variance1);
        double stdDev2 = Math.Sqrt(variance2);
        double variance = variance1 + variance2 - (2 * rho * stdDev1 * stdDev2);
        double stdDev = Math.Sqrt(variance);
        double d1 = (Math.Log(quantity1 * forward1 / (quantity2 * forward2))
                   + (0.5 * variance)) / stdDev;
        double d2 = d1 - stdDev;
        double nd1Cum = Cum.Evaluate(d1);
        double nd2Cum = Cum.Evaluate(d2);
        double nd1 = Norm.Evaluate(d1);
        double nd2 = Norm.Evaluate(d2);

        // the time on the clock across both processes, the first's risk-free curve's where they share its times
        double t = FdmBlackScholesOp.ClockAcross([process1, process2]).TimeFromReference(exercise.LastDate());
        double sqt = Math.Sqrt(t);
        double q1 = -Math.Log(dividendDiscount1) / (sqt * sqt);

        // the second asset's carry on the first process's curve: its yield plus the first rate less the second, 0 on
        // one curve
        double q2 = (-Math.Log(dividendDiscount2) / (sqt * sqt))
            + ((Math.Log(riskFreeDiscount2) - Math.Log(riskFreeDiscount)) / (sqt * sqt));

        double value =
            riskFreeDiscount * ((quantity1 * forward1 * nd1Cum) - (quantity2 * forward2 * nd2Cum));

        // Greeks, each quantity once
        double delta1 = riskFreeDiscount * (quantity1 * forward1 * nd1Cum) / s1;
        double delta2 = -riskFreeDiscount * (quantity2 * forward2 * nd2Cum) / s2;
        double gamma1 = (riskFreeDiscount * (quantity1 * forward1 * nd1) / s1) / (s1 * stdDev);
        double gamma2 = (-riskFreeDiscount * (quantity2 * forward2 * nd2) / s2) / (-s2 * stdDev);
        double vega = riskFreeDiscount * (quantity1 * forward1 * nd1) * sqt;
        double theta = -((stdDev * vega / sqt / (2 * t)) - (q1 * s1 * delta1) - (q2 * s2 * delta2));

        return new MargrabeOptionResults
        {
            NPV = value,
            Delta1 = delta1,
            Delta2 = delta2,
            Gamma1 = gamma1,
            Gamma2 = gamma2,
            Theta = theta,
            Rho = 0.0,
        };
    }
}
