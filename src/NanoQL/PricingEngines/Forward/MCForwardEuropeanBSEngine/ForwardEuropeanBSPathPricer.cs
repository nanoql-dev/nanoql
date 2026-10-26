// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp ql/pricingengines/forward/mcforwardeuropeanbsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The forward-start European path pricer: a plain vanilla payoff on the path's last value, struck at the moneyness
/// times its value at the reset, discounted.
/// </summary>
public sealed class ForwardEuropeanBSPathPricer : PathPricer<MonteCarloPath>
{
    private readonly OptionType type;
    private readonly double moneyness;
    private readonly int resetIndex;
    private readonly double discount;

    /// <summary>Initializes a new instance of the <see cref="ForwardEuropeanBSPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="moneyness">The strike's multiple of the reset value, non-negative.</param>
    /// <param name="resetIndex">The reset's index on the path.</param>
    /// <param name="discount">The discount factor to the payment.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException"><paramref name="moneyness"/> is negative.</exception>
    public ForwardEuropeanBSPathPricer(OptionType type, double moneyness, int resetIndex, double discount)
    {
        EnumArguments.Check(type, nameof(type));
        this.type = type;
        this.moneyness = moneyness;
        this.resetIndex = resetIndex;
        this.discount = discount;
        if (!(moneyness >= 0.0))
        {
            throw new ArgumentException("moneyness less than zero not allowed", nameof(moneyness));
        }
    }

    /// <summary>The payoff on the path's last value, struck at the reset, discounted.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path has no step.</exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int n = path.Length - 1;
        if (!(n > 0))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        double resetLevel = path[resetIndex];
        double strike = resetLevel * moneyness;
        return PlainVanillaPayoff.Value(type, strike, path.Back()) * discount;
    }
}
