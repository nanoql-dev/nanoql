// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The intermediate class for put and call payoffs.</summary>
/// <remarks>
/// An extension point, as in QuantLib. Its option type is checked on construction, with the text of QuantLib's
/// derived payoffs' switches on it, which fail with "unknown/illegal option type" when the payoff is evaluated; so the
/// derived payoffs' switches never meet a value outside <see cref="NanoQL.OptionType"/>.
/// </remarks>
public abstract class TypePayoff : Payoff
{
    /// <summary>Initializes a new instance of the <see cref="TypePayoff"/> class.</summary>
    /// <param name="type">Call or put.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    protected TypePayoff(OptionType type)
    {
        if (type is not (OptionType.Call or OptionType.Put))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "unknown/illegal option type");
        }

        OptionType = type;
    }

    /// <summary>Gets the option type, call or put.</summary>
    public OptionType OptionType { get; }

    /// <summary>Gets the payoff's description: its name and its option type ("Vanilla Call").</summary>
    public override string Description => $"{Name} {OptionType}";
}
