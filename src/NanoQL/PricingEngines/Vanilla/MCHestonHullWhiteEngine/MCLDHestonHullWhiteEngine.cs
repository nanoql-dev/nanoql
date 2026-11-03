// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mchestonhullwhiteengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCHestonHullWhiteEngine"/> on low-discrepancy draws with QuantLib's default statistics:
/// <c>MCLDHestonHullWhiteEngine</c>, named as QuantLib-Python names its siblings, QuantLib's
/// <c>MCHestonHullWhiteEngine&lt;LowDiscrepancy&gt;</c>, its calculations closed at <see cref="LowDiscrepancy"/> and
/// <see cref="GeneralStatistics"/>.
/// </summary>
public static class MCLDHestonHullWhiteEngine
{
    /// <summary>Prices a European option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The hybrid process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> where they're given per
    /// year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="controlVariate">Whether the control variate is used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/> where a tolerance is
    /// given.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The option's results: its value and error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the process's step fails, or QuantLib's Monte
    /// Carlo checks fail.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HybridHestonHullWhiteProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCHestonHullWhiteEngine.Calculate<LowDiscrepancy, GeneralStatistics>(option, context, process, timeSteps, timeStepsPerYear, antitheticVariate, controlVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
