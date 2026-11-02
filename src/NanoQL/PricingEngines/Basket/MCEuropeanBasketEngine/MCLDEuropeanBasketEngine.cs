// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mceuropeanbasketengine.hpp ql/pricingengines/basket/mceuropeanbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCEuropeanBasketEngine"/> on low-discrepancy draws with QuantLib's default statistics: QuantLib-Python's
/// <c>MCLDEuropeanBasketEngine</c>, QuantLib's <c>MCEuropeanBasketEngine&lt;LowDiscrepancy&gt;</c>, its calculations
/// closed at <see cref="LowDiscrepancy"/> and <see cref="GeneralStatistics"/> (D12).
/// </summary>
public static class MCLDEuropeanBasketEngine
{
    /// <summary>Prices a European basket option.</summary>
    /// <param name="option">The basket option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// The first process isn't a Black-Scholes process, neither samples nor a tolerance is given, or the simulation
    /// fails.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCEuropeanBasketEngine.Calculate<LowDiscrepancy, GeneralStatistics>(option, context, processes, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
