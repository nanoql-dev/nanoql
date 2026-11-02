// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mchimalayaengine.hpp ql/experimental/exoticoptions/mchimalayaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// <see cref="MCHimalayaEngine"/> on pseudo-random draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCPRHimalayaEngine</c>, QuantLib's <c>MCHimalayaEngine&lt;PseudoRandom&gt;</c>, its calculations closed at
/// <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
[Experimental("NQL0001")]
public static class MCPRHimalayaEngine
{
    /// <summary>Prices a Himalaya option.</summary>
    /// <param name="option">The Himalaya option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// A fixing date precedes the evaluation date ("seasoned options are not handled") or the dates aren't sorted, the
    /// first process isn't a Black-Scholes process, neither samples nor a tolerance is given, or the simulation fails.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        HimalayaOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCHimalayaEngine.Calculate<PseudoRandom, GeneralStatistics>(option, context, processes, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
