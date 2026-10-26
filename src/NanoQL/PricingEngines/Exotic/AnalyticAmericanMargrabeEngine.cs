// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticamericanmargrabeengine.hpp ql/pricingengines/exotic/analyticamericanmargrabeengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for an American Margrabe option: the exchange priced as an American call on Q₁S₁ struck at Q₂S₂,
/// on the first asset's yield as dividend, the second's as rate and their spread's volatility, by
/// <see cref="BjerksundStenslandApproximationEngine"/>; E. G. Haug, "The Complete Guide to Option Pricing Formulas",
/// 2nd edition (2007).
/// </summary>
/// <remarks>
/// <para>The value alone, as QuantLib's.</para>
/// <para>
/// The second asset's yield is read on the first process's risk-free curve, its yield plus the first rate less the
/// second, where QuantLib reads no risk-free rate, so two risk-free curves price as QuantLib's engine on the market
/// restated onto the first (<c>Divergences/TwoAssetTwoRiskFreeCurvesTests.cs</c>). The flat market runs on the clock
/// across both processes (<see cref="FdmBlackScholesOp.ClockAcross"/>), each structure read by date, accruing nothing
/// before its date, where QuantLib builds it from the evaluation date with rates over the risk-free curve's time; on
/// structures dated at the evaluation date alike the market is QuantLib's
/// (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticAmericanMargrabeEngine
{
    /// <summary>Prices an American Margrabe option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A quantity isn't positive, or the exercise isn't American.</exception>
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
        return option.FetchResults(new MargrabeOptionResults
        {
            NPV = Value(option, context, process1, process2, correlation),
        });
    }

    // QuantLib's calculate().
    private static double Value(
        MargrabeOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double rho)
    {
        if (option.Exercise.Type != ExerciseType.American)
        {
            throw new NanoQLException("not an American option");
        }

        if (option.Exercise is not AmericanExercise exercise)
        {
            throw new NanoQLException("not an American option");
        }

        if (option.Payoff is not NullPayoff)
        {
            throw new NanoQLException("not a null payoff");
        }

        // The option can be priced as an American single-asset option
        // with an adjusted process and payoff.

        // the clock across both processes, the first's risk-free curve where every structure shares its times
        TermStructure clock = FdmBlackScholesOp.ClockAcross([process1, process2]);
        Date today = clock.ReferenceDate;
        DayCounter rfdc = clock.DayCounter;
        Date lastDate = option.Exercise.LastDate();
        double t = clock.TimeFromReference(lastDate);

        double s1 = process1.X0;
        double s2 = process2.X0;

        double spot = option.Q1 * s1;
        var payoff = new PlainVanillaPayoff(OptionType.Call, option.Q2 * s2);

        double dividendDiscount1 = process1.DividendYield.AccruedDiscount(exercise.LastDate());
        double q1 = -Math.Log(dividendDiscount1) / t;

        // the second asset's yield on the first process's curve: plus the first rate less the second, 0 on one curve
        double dividendDiscount2 = process2.DividendYield.AccruedDiscount(exercise.LastDate());
        double q2 = (-Math.Log(dividendDiscount2) / t)
            + ((Math.Log(process2.RiskFreeRate.AccruedDiscount(lastDate))
                - Math.Log(process1.RiskFreeRate.AccruedDiscount(lastDate))) / t);

        var qTS = new FlatForward(today, q1, rfdc);
        var rTS = new FlatForward(today, q2, rfdc);

        double variance1 = process1.BlackVolatility.AccruedVariance(exercise.LastDate(), s1);
        double variance2 = process2.BlackVolatility.AccruedVariance(exercise.LastDate(), s2);
        double variance = variance1 + variance2
                      - (2 * rho * Math.Sqrt(variance1) * Math.Sqrt(variance2));
        double volatility = Math.Sqrt(variance / t);

        var volTS = new BlackConstantVol(today, new NullCalendar(), volatility, rfdc);

        var stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        var vanilla = new VanillaOption(payoff, exercise);
        return BjerksundStenslandApproximationEngine.Calculate(vanilla, context, stochProcess).NPV!.Value;
    }
}
