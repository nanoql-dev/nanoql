// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/vanillaswingoption.hpp ql/instruments/vanillaswingoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A forward payoff: the price less the strike for a call, the strike less the price for a put, unfloored.</summary>
/// <remarks>Immutable.</remarks>
public sealed class VanillaForwardPayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="VanillaForwardPayoff"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public VanillaForwardPayoff(OptionType type, double strike)
        : base(type, strike)
    {
    }

    /// <inheritdoc/>
    public override string Name => "ForwardTypePayoff";

    /// <inheritdoc/>
    public override double Evaluate(double price) =>
        OptionType == OptionType.Call ? price - Strike : Strike - price;
}
