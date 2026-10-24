// SPDX-FileCopyrightText: Copyright (C) 2006 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/forward.hpp ql/instruments/forward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>A forward's payoff: the price less the strike for a long position, the strike less the price for a short
/// one.</summary>
public sealed class ForwardTypePayoff : Payoff
{
    /// <summary>Initializes a new instance of the <see cref="ForwardTypePayoff"/> class.</summary>
    /// <param name="type">The position.</param>
    /// <param name="strike">The strike, not negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="strike"/> is negative, QuantLib's "negative strike
    /// given".</exception>
    public ForwardTypePayoff(PositionType type, double strike)
    {
        EnumArguments.Check(type, nameof(type));
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("negative strike given", nameof(strike));
        }

        ForwardType = type;
        Strike = strike;
    }

    /// <summary>Gets the position.</summary>
    public PositionType ForwardType { get; }

    /// <summary>Gets the strike.</summary>
    public double Strike { get; }

    /// <inheritdoc/>
    public override string Name => "Forward";

    /// <inheritdoc/>
    public override string Description => $"{Name}, {DataFormattersFunctions.General(Strike)} strike";

    /// <inheritdoc/>
    public override double Evaluate(double price) => ForwardType == PositionType.Long ? price - Strike : Strike - price;
}
