// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mceuropeanhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>The European path pricer of a Heston-like multi-path: a plain vanilla payoff on the first asset's last value,
/// discounted.</summary>
/// <remarks>Immutable; its evaluation allocates nothing (the S-hes plan's HR2 (iii)).</remarks>
public sealed class EuropeanHestonPathPricer : PathPricer<MultiPath>
{
    private readonly PlainVanillaPayoff payoff;
    private readonly double discount;

    /// <summary>Initializes a new instance of the <see cref="EuropeanHestonPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discount">The discount factor to the payment.</param>
    /// <exception cref="ArgumentException"><paramref name="strike"/> is negative.</exception>
    public EuropeanHestonPathPricer(OptionType type, double strike, double discount)
    {
        payoff = new PlainVanillaPayoff(type, strike);
        this.discount = discount;
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("strike less than zero not allowed", nameof(strike));
        }
    }

    /// <summary>The payoff on the first asset's last value, discounted.</summary>
    /// <param name="path">The multi-path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path is empty.</exception>
    public override double Evaluate(MultiPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        MonteCarloPath first = path[0];
        int n = path.PathSize();
        if (!(n > 0))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        return payoff.Evaluate(first.Back()) * discount;
    }
}
