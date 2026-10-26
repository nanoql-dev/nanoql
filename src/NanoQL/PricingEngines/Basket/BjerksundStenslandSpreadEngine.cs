// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/bjerksundstenslandspreadengine.hpp ql/pricingengines/basket/bjerksundstenslandspreadengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// Bjerksund and Stensland's engine for a European spread option on two futures: P. Bjerksund and G. Stensland,
/// "Closed form spread option valuation", Quantitative Finance 14 (2014), pp. 1785–1794.
/// </summary>
/// <remarks>
/// Immutable. A second forward and strike summing to zero or less is refused, where QuantLib takes the log of a
/// negative ratio and returns NaN (<c>Divergences/SpreadApproximationStrikeTests.cs</c>).
/// </remarks>
public sealed class BjerksundStenslandSpreadEngine : SpreadBlackScholesVanillaEngine
{
    private static readonly CumulativeNormalDistribution Phi = new();

    /// <summary>Initializes a new instance of the <see cref="BjerksundStenslandSpreadEngine"/> class.</summary>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    public BjerksundStenslandSpreadEngine(
        GeneralizedBlackScholesProcess process1, GeneralizedBlackScholesProcess process2, double correlation)
        : base(process1, process2, correlation)
    {
    }

    /// <summary>Prices a European spread option by Bjerksund and Stensland's formula.</summary>
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
        new BjerksundStenslandSpreadEngine(process1, process2, correlation).Calculate(option, context);

    /// <inheritdoc/>
    protected override double Calculate(
        double f1, double f2, double k, OptionType optionType, double variance1, double variance2, double df)
    {
        double cp = (optionType == OptionType.Call) ? 1 : -1;

        double a = f2 + k;

        // the log of f1/a below, where QuantLib's is NaN
        if (a <= 0.0)
        {
            throw new NanoQLException("f2 + strike must be positive for this approximation");
        }

        double b = f2 / a;

        double sigma1 = Math.Sqrt(variance1);
        double sigma2 = Math.Sqrt(variance2);

        double stdev = Math.Sqrt(
            variance1 + (b * b * variance2) - (2 * Rho * b * sigma1 * sigma2));

        double lfa = Math.Log(f1 / a);

        double d1 =
            (lfa + ((0.5 * variance1) + (0.5 * b * b * variance2) - (b * Rho * sigma1 * sigma2))) / stdev;
        double d2 =
            (lfa + ((-0.5 * variance1) + (variance2 * b * ((0.5 * b) - 1)) + (Rho * sigma1 * sigma2))) / stdev;
        double d3 = (lfa + ((-0.5 * variance1) + (0.5 * b * b * variance2))) / stdev;

        return df * cp * ((f1 * Phi.Evaluate(cp * d1)) - (f2 * Phi.Evaluate(cp * d2)) - (k * Phi.Evaluate(cp * d3)));
    }
}
