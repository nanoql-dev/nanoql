// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/binomialdoublebarrierengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// The binomial double-barrier engine on the Joshi tree and Derman and Kani's discretized double-barrier option:
/// QuantLib-Python's <c>BinomialJ4DoubleBarrierEngine</c>, QuantLib's <c>BinomialDoubleBarrierEngine&lt;Joshi4,
/// DiscretizedDermanKaniDoubleBarrierOption&gt;</c>, the wheel's closing.
/// </summary>
[Experimental("NQL0001")]
public static class BinomialJ4DoubleBarrierEngine
{
    /// <summary>Prices a double barrier option on a binomial tree.</summary>
    /// <param name="option">The option, with a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <returns>The value, delta, gamma and theta; the delta and the gamma <see langword="null"/> where the tree's
    /// first nodes don't separate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the payoff isn't striked, the tree can't be built, or the asset refuses the
    /// barrier type.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DoubleBarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int timeSteps) =>
        BinomialDoubleBarrierEngine.Calculate<Joshi4, DiscretizedDermanKaniDoubleBarrierOption>(option, context, process, timeSteps);
}
