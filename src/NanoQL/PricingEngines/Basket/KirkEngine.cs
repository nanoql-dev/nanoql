// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/kirkengine.hpp ql/pricingengines/basket/kirkengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// Kirk's engine for a European spread option on two futures: E. Kirk, "Correlation in the Energy Markets",
/// Managing Energy Price Risk, Risk Publications and Enron, pp. 71-78.
/// </summary>
/// <remarks>
/// Immutable. A second forward and strike summing to zero or less is refused by Black's formula, as in QuantLib.
/// </remarks>
public sealed class KirkEngine : SpreadBlackScholesVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="KirkEngine"/> class.</summary>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    public KirkEngine(
        GeneralizedBlackScholesProcess process1, GeneralizedBlackScholesProcess process2, double correlation)
        : base(process1, process2, correlation)
    {
    }

    /// <summary>Prices a European spread option by Kirk's formula.</summary>
    /// <param name="option">The option, on a <see cref="SpreadBasketPayoff"/> over a plain vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't a spread payoff over a plain vanilla payoff, or the second forward
    /// and the strike sum to zero or less.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation) =>
        new KirkEngine(process1, process2, correlation).Calculate(option, context);

    /// <inheritdoc/>
    protected override double Calculate(
        double f1, double f2, double strike, OptionType optionType, double variance1, double variance2, double df)
    {
        double f = f1 / (f2 + strike);
        double r = f2 / (f2 + strike);
        double v
            = Math.Sqrt(variance1
                        + (variance2 * (r * r))
                        - (2 * Rho * Math.Sqrt(variance1 * variance2)
                            * (f2 / (f2 + strike))));

        var black = new BlackCalculator(optionType, 1.0, f, v, df);

        return (f2 + strike) * black.Value;
    }
}
