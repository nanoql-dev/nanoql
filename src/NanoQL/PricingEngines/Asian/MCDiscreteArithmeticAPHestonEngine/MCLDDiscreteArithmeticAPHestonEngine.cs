// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_price_heston.hpp ql/pricingengines/asian/mc_discr_arith_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCDiscreteArithmeticAPHestonEngine"/> on low-discrepancy draws with QuantLib's default statistics:
/// QuantLib-Python's <c>MCLDDiscreteArithmeticAPHestonEngine</c>, QuantLib's
/// <c>MCDiscreteArithmeticAPHestonEngine&lt;LowDiscrepancy&gt;</c>, its calculations closed at
/// <see cref="LowDiscrepancy"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCLDDiscreteArithmeticAPHestonEngine
{
    /// <summary>Prices a discrete arithmetic average-price Asian option by Monte Carlo on a Heston process.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="controlVariate">Whether the geometric average is a control variate.</param>
    /// <returns>The option's results, the NPV, the error estimate and the grid; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Both the time steps and the time steps per year are given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples or steps is negative, or the seed is outside
    /// its range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's past fixings fail its checks, the payoff isn't plain vanilla, the exercise isn't European, every
    /// fixing is in the past, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option,
        EvaluationContext context,
        HestonProcess process,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? timeSteps = null,
        int? timeStepsPerYear = null,
        bool controlVariate = false) =>
        MCDiscreteArithmeticAPHestonEngine.Calculate<LowDiscrepancy, GeneralStatistics>(option, context, process, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed, timeSteps, timeStepsPerYear, controlVariate);
}
