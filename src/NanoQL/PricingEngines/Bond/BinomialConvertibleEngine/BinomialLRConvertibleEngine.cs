// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bond/binomialconvertibleengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The binomial convertible engine on the Leisen-Reimer tree: QuantLib-Python's <c>BinomialLRConvertibleEngine</c>, QuantLib's
/// <c>BinomialConvertibleEngine&lt;LeisenReimer&gt;</c> (D12).
/// </summary>
public static class BinomialLRConvertibleEngine
{
    /// <summary>Prices a convertible bond on the Leisen-Reimer tree.</summary>
    /// <param name="bond">The convertible bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process of the underlying.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="creditSpread">The credit spread.</param>
    /// <param name="dividends">The underlying's dividends; none by default.</param>
    /// <returns>The results, as <see cref="BinomialConvertibleEngine.Calculate{TTree}"/> gives them.</returns>
    /// <exception cref="ArgumentNullException">The bond, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeSteps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is 0, or a dividend is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The bond can't be priced.</exception>
    public static BondResults Calculate(
        ConvertibleBond bond,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int timeSteps,
        double creditSpread,
        ImmutableArray<Dividend> dividends = default) =>
        BinomialConvertibleEngine.Calculate<LeisenReimer>(bond, context, process, timeSteps, creditSpread, dividends);
}
