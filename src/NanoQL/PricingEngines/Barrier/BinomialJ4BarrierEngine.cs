// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/binomialbarrierengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The binomial barrier engine on the Joshi tree and Derman and Kani's discretized barrier option: QuantLib-Python's
/// <c>BinomialJ4BarrierEngine</c>, QuantLib's <c>BinomialBarrierEngine&lt;Joshi4,
/// DiscretizedDermanKaniBarrierOption&gt;</c>, the wheel's closing (D12).
/// </summary>
public static class BinomialJ4BarrierEngine
{
    /// <summary>Prices a barrier option on a binomial tree.</summary>
    /// <param name="option">The option, with a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="maxTimeSteps">The most time steps Boyle and Lau's optimum may raise them to: 0, QuantLib's default,
    /// for the heuristic max(1000, 5 × <paramref name="timeSteps"/>); equal to <paramref name="timeSteps"/> to disable
    /// it. Ignored but for Cox-Ross-Rubinstein trees.</param>
    /// <returns>The value, delta, gamma and theta; the delta and the gamma <see langword="null"/> where the tree's
    /// first nodes don't separate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="timeSteps"/> isn't positive, or <paramref name="maxTimeSteps"/> is neither 0 nor at least
    /// <paramref name="timeSteps"/>.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked or its strike isn't positive, the underlying isn't positive or lies beyond the barrier
    /// ("barrier touched"), or the tree can't be built.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int timeSteps,
        int maxTimeSteps = 0) =>
        BinomialBarrierEngine.Calculate<Joshi4, DiscretizedDermanKaniBarrierOption>(option, context, process, timeSteps, maxTimeSteps);
}
