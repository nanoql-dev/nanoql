// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticcevengine.hpp ql/pricingengines/vanilla/analyticcevengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The pricing engine for European vanilla options on the constant elasticity of variance (CEV) process
/// df = α·f^β·dW, with an absorbing boundary at f = 0: <see cref="CEVCalculator"/>'s value, discounted.
/// </summary>
/// <remarks>
/// The engine reports the value. The time and the discount are the discount curve's at the exercise date, read by
/// date, nothing accrued before the curve's reference date (the mismatched-dates class), where QuantLib's reads throw:
/// an exercise on or before that date is the payoff at f0, undiscounted (<see cref="CEVCalculator.Value"/> at a time
/// of 0). Near β = 1 and in the formula's small tails the calculator departs from QuantLib (its remarks).
/// </remarks>
public static class AnalyticCEVEngine
{
    /// <summary>Prices a European vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="f0">The forward's value today.</param>
    /// <param name="alpha">The volatility parameter α.</param>
    /// <param name="beta">The elasticity β.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the payoff isn't striked, or the calculator
    /// refuses its arguments.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, double f0, double alpha, double beta,
        YieldTermStructure discountCurve)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        var calculator = new CEVCalculator(f0, alpha, beta);

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        Date exerciseDate = option.Exercise.LastDate();

        // read by date, nothing accrued before the curve's reference date (the mismatched-dates class)
        double value = calculator.Value(payoff.OptionType, payoff.Strike, discountCurve.AccrualTimeTo(exerciseDate))
            * discountCurve.AccruedDiscount(exerciseDate);

        return option.FetchResults(new OneAssetOptionResults { NPV = value });
    }
}
