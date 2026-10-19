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

/// <summary>The binary gap payoff.</summary>
/// <remarks>
/// It is equivalent to being long a <see cref="PlainVanillaPayoff"/> at the first strike (of the same type) and short a
/// <see cref="CashOrNothingPayoff"/> at the first strike (of the same type) whose cash payoff is the difference between
/// the second and the first strike. It can be negative depending on the strikes.
/// </remarks>
public sealed class GapPayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="GapPayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <param name="strike">The strike that decides whether the payoff is paid.</param>
    /// <param name="secondStrike">The strike the payoff is measured from, a.k.a. the payoff strike.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public GapPayoff(OptionType type, double strike, double secondStrike)
        : base(type, strike)
    {
        SecondStrike = secondStrike;
    }

    /// <inheritdoc/>
    public override string Name => "Gap";

    /// <summary>Gets the payoff's description: the striked payoff's, then the second strike ("…, 90 strike payoff").</summary>
    public override string Description =>
        $"{base.Description}, {DataFormattersFunctions.General(SecondStrike)} strike payoff";

    /// <summary>Gets the strike the payoff is measured from, a.k.a. the payoff strike.</summary>
    public double SecondStrike { get; }

    /// <summary>
    /// The payoff at the given price: where the option is in the money or at the money, the call's price − second
    /// strike or the put's second strike − price; otherwise 0.
    /// </summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) => OptionType == OptionType.Call
        ? (price - Strike >= 0.0 ? price - SecondStrike : 0.0)
        : (Strike - price >= 0.0 ? SecondStrike - price : 0.0);
}
