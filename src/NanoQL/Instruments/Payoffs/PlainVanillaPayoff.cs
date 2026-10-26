// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The plain-vanilla payoff.</summary>
public sealed class PlainVanillaPayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="PlainVanillaPayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public PlainVanillaPayoff(OptionType type, double strike)
        : base(type, strike)
    {
    }

    /// <inheritdoc/>
    public override string Name => "Vanilla";

    /// <summary>The payoff at the given price: the call's max(price − strike, 0) or the put's max(strike − price, 0).</summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) => Value(OptionType, Strike, price);

    // The payoff of a type and strike at a price, for the path pricers whose strike is each path's, where QuantLib
    // builds a payoff per path; the type checked by the caller.
    internal static double Value(OptionType type, double strike, double price) =>
        type == OptionType.Call ? Max(price - strike, 0.0) : Max(strike - price, 0.0);

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
