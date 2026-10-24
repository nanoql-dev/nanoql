// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticdigitalamericanengine.hpp ql/pricingengines/vanilla/analyticdigitalamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic pricing engine for American knock-out options with a digital payoff at expiry: a cash-or-nothing or
/// asset-or-nothing payoff paid at expiry if the underlying never reached the strike (an up-and-out call or a
/// down-and-out put).
/// </summary>
/// <remarks>
/// QuantLib's <c>AnalyticDigitalAmericanKOEngine</c>, whose <c>knock_in()</c> returns false: the engine of
/// <see cref="AnalyticDigitalAmericanEngine"/> with the knock-out's formula. A payoff at the hit is refused, where
/// QuantLib's knock-out engine prices the knock-in, its at-hit formula having no knock-out: a documented divergence
/// (<c>Divergences/DigitalAmericanKnockOutAtHitTests.cs</c>).
/// </remarks>
public static class AnalyticDigitalAmericanKOEngine
{
    /// <summary>Prices an American digital option, knock-out, paid at expiry.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results, the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The payoff is neither cash-or-nothing nor asset-or-nothing.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't American, its window opens after the volatility's reference date, it pays at the hit (a
    /// divergence), or the underlying isn't positive.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process) =>
        AnalyticDigitalAmericanEngine.Calculate(option, context, process, false);
}
