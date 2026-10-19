// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The binary asset-or-nothing payoff.</summary>
/// <remarks>
/// The binary path-independent payoffs are defined in M. Rubinstein, E. Reiner, "Unscrambling The Binary Code", Risk,
/// Vol. 4 no. 9, 1991.
/// </remarks>
public sealed class AssetOrNothingPayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="AssetOrNothingPayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public AssetOrNothingPayoff(OptionType type, double strike)
        : base(type, strike)
    {
    }

    /// <inheritdoc/>
    public override string Name => "AssetOrNothing";

    /// <summary>The payoff at the given price: the price where the option is strictly in the money, otherwise 0.</summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) => OptionType == OptionType.Call
        ? (price - Strike > 0.0 ? price : 0.0)
        : (Strike - price > 0.0 ? price : 0.0);
}
