// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcforwardeuropeanhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCForwardEuropeanHestonEngine"/> on pseudo-random draws with QuantLib's default statistics:
/// QuantLib-Python's <c>MCPRForwardEuropeanHestonEngine</c>, QuantLib's
/// <c>MCForwardEuropeanHestonEngine&lt;PseudoRandom&gt;</c>, its calculations closed at <see cref="PseudoRandom"/> and
/// <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCPRForwardEuropeanHestonEngine
{
    /// <summary>Prices a forward-start European option by Monte Carlo under a Heston process.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="controlVariate">Whether the analytic vanilla is used as a control variate.</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't
    /// positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla or its exercise European, neither a tolerance nor
    /// a number of samples is given, the samples can't reach the tolerance, or a step refuses.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ForwardVanillaOption option,
        EvaluationContext context,
        HestonProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        bool controlVariate = false) =>
        MCForwardEuropeanHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(option, context, process, timeSteps, timeStepsPerYear, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed, controlVariate);
}
