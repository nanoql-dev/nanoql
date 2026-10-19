// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>The binary cash-or-nothing payoff.</summary>
public sealed class CashOrNothingPayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="CashOrNothingPayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="cashPayoff">The cash paid where the option is in the money.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public CashOrNothingPayoff(OptionType type, double strike, double cashPayoff)
        : base(type, strike)
    {
        CashPayoff = cashPayoff;
    }

    /// <inheritdoc/>
    public override string Name => "CashOrNothing";

    /// <summary>Gets the payoff's description: the striked payoff's, then the cash payoff ("…, 10 cash payoff").</summary>
    public override string Description =>
        $"{base.Description}, {DataFormattersFunctions.General(CashPayoff)} cash payoff";

    /// <summary>Gets the cash paid where the option is in the money.</summary>
    public double CashPayoff { get; }

    /// <summary>
    /// The payoff at the given price: the cash payoff where the option is strictly in the money, otherwise 0.
    /// </summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) => OptionType == OptionType.Call
        ? (price - Strike > 0.0 ? CashPayoff : 0.0)
        : (Strike - price > 0.0 ? CashPayoff : 0.0);
}
