// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcdigitalengine.hpp ql/pricingengines/vanilla/mcdigitalengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCDigitalEngine"/> on pseudo-random draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCPRDigitalEngine</c>, QuantLib's <c>MCDigitalEngine&lt;PseudoRandom&gt;</c>, its calculations closed at
/// <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCPRDigitalEngine
{
    /// <summary>Prices an American cash-or-nothing option by Monte Carlo.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of
    /// steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't
    /// positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't cash-or-nothing, the exercise isn't American, neither a tolerance nor a number of samples is
    /// given, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
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
        MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(option, context, process, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
