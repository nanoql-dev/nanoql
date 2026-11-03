// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Affine Group Limited
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/binomialengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The binomial engine on the Trigeorgis tree: QuantLib-Python's <c>BinomialTrigeorgisVanillaEngine</c>, QuantLib's
/// <c>BinomialVanillaEngine&lt;Trigeorgis&gt;</c>.
/// </summary>
public static class BinomialTrigeorgisVanillaEngine
{
    /// <summary>Prices a vanilla option on the Trigeorgis tree.</summary>
    /// <param name="option">The option, with a plain-vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, at least 2.</param>
    /// <returns>The results, as <see cref="BinomialVanillaEngine.Calculate{TTree}"/> gives them.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is below 2.</exception>
    /// <exception cref="NanoQLException">The payoff isn't plain vanilla, or the option can't be priced.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int timeSteps) =>
        BinomialVanillaEngine.Calculate<Trigeorgis>(option, context, process, timeSteps);
}
