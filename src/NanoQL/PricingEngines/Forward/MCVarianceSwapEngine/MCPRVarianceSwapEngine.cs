// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcvarianceswapengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCVarianceSwapEngine"/> on pseudo-random draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCPRVarianceSwapEngine</c>, QuantLib's <c>MCVarianceSwapEngine&lt;PseudoRandom&gt;</c>, its calculations closed
/// at <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/>.
/// </summary>
public static class MCPRVarianceSwapEngine
{
    /// <summary>Prices a variance swap by Monte Carlo.</summary>
    /// <param name="swap">The variance swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The swap's results: the NPV, the variance and, where the numbers give one, the error
    /// estimate.</returns>
    /// <exception cref="ArgumentNullException">The swap, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are both given, neither, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// Neither a tolerance nor a number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static VarianceSwapResults Calculate(
        VarianceSwap swap,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCVarianceSwapEngine.Calculate<PseudoRandom, GeneralStatistics>(swap, context, process, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
