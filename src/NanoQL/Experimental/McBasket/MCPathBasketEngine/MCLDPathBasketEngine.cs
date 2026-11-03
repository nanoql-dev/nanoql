// SPDX-FileCopyrightText: Copyright (C) 2008 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/mcpathbasketengine.hpp ql/experimental/mcbasket/mcpathbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// <see cref="MCPathBasketEngine"/> on low-discrepancy draws with QuantLib's default statistics:
/// <c>MCLDPathBasketEngine</c>, named as QuantLib-Python names its siblings, QuantLib's
/// <c>MCPathBasketEngine&lt;LowDiscrepancy&gt;</c>, its calculations closed at <see cref="LowDiscrepancy"/> and
/// <see cref="GeneralStatistics"/>.
/// </summary>
[Experimental("NQL0001")]
public static class MCLDPathBasketEngine
{
    /// <summary>Prices a path option, its early exercise ignored.</summary>
    /// <param name="option">The path option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="controlVariate">Whether to use a control variate, which this engine doesn't provide.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// The option has no payoff or no fixing date, the fixing dates aren't strictly increasing, the first process isn't
    /// a Black-Scholes process, neither samples nor a tolerance is given, or the simulation fails.
    /// </exception>
    public static InstrumentResults Calculate(
        PathMultiAssetOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCPathBasketEngine.Calculate<LowDiscrepancy, GeneralStatistics>(option, context, processes, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, controlVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
