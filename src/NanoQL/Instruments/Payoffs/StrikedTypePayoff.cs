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

/// <summary>The intermediate class for payoffs based on a fixed strike.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
public abstract class StrikedTypePayoff : TypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="StrikedTypePayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    protected StrikedTypePayoff(OptionType type, double strike)
        : base(type)
    {
        Strike = strike;
    }

    /// <summary>Gets the strike.</summary>
    public double Strike { get; }

    /// <summary>
    /// Gets the payoff's description: the type's, then the strike, in a C++ stream's default format ("Vanilla Call,
    /// 100 strike").
    /// </summary>
    public override string Description => $"{base.Description}, {DataFormattersFunctions.General(Strike)} strike";
}
