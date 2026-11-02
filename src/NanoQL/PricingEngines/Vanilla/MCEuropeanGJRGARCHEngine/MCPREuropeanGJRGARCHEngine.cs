// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCEuropeanGJRGARCHEngine"/> on pseudo-random draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCPREuropeanGJRGARCHEngine</c>, QuantLib's <c>MCEuropeanGJRGARCHEngine&lt;PseudoRandom&gt;</c>, its calculations
/// closed at <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCPREuropeanGJRGARCHEngine
{
    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The GJR-GARCH process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> where they're given per
    /// year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/> where a tolerance is
    /// given.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The option's results: its value and error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The payoff isn't plain vanilla, or QuantLib's Monte Carlo checks
    /// fail.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GJRGARCHProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCEuropeanGJRGARCHEngine.Calculate<PseudoRandom, GeneralStatistics>(option, context, process, timeSteps, timeStepsPerYear, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
