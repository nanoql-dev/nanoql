// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A payoff based on a floating strike, given with the price.</summary>
public sealed class FloatingTypePayoff : TypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="FloatingTypePayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public FloatingTypePayoff(OptionType type)
        : base(type)
    {
    }

    /// <inheritdoc/>
    public override string Name => "FloatingType";

    /// <summary>The plain-vanilla payoff at the given price and strike.</summary>
    /// <param name="price">The underlying's price.</param>
    /// <param name="strike">The floating strike.</param>
    /// <returns>The call's max(price − strike, 0) or the put's max(strike − price, 0).</returns>
    public double Evaluate(double price, double strike) =>
        OptionType == OptionType.Call ? Max(price - strike, 0.0) : Max(strike - price, 0.0);

    /// <summary>Refuses to evaluate: the payoff needs its strike.</summary>
    /// <param name="price">The underlying's price, unread.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always: "floating payoff not handled".</exception>
    public override double Evaluate(double price) => throw new NanoQLException("floating payoff not handled");

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
