// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/cliquet/mcperformanceengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCPerformanceEngine"/> on low-discrepancy draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCLDPerformanceEngine</c>, QuantLib's <c>MCPerformanceEngine&lt;LowDiscrepancy&gt;</c>, its calculations closed
/// at <see cref="LowDiscrepancy"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCLDPerformanceEngine
{
    /// <summary>Prices a performance option by Monte Carlo.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
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
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, neither a tolerance nor a number of samples is given, or the samples can't reach
    /// the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        CliquetOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCPerformanceEngine.Calculate<LowDiscrepancy, GeneralStatistics>(option, context, process, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
