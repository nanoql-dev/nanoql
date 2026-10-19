// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A payoff with its strike expressed as a percentage of the price, its moneyness.</summary>
/// <remarks>The moneyness is held as the strike: <see cref="StrikedTypePayoff.Strike"/> returns it.</remarks>
public sealed class PercentageStrikePayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="PercentageStrikePayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <param name="moneyness">The strike as a fraction of the price.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public PercentageStrikePayoff(OptionType type, double moneyness)
        : base(type, moneyness)
    {
    }

    /// <inheritdoc/>
    public override string Name => "PercentageStrike";

    /// <summary>
    /// The payoff at the given price: the call's price·max(1 − moneyness, 0) or the put's price·max(moneyness − 1, 0).
    /// </summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) =>
        OptionType == OptionType.Call ? price * Max(1.0 - Strike, 0.0) : price * Max(Strike - 1.0, 0.0);

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
