// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/payoff.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The abstract base class of option payoffs.</summary>
/// <remarks>
/// <para>
/// QuantLib's function-call operator is <see cref="Evaluate"/>. Its <c>accept(AcyclicVisitor&amp;)</c> isn't ported: a
/// caller that needs the payoff's type matches it with a <c>switch</c>.
/// </para>
/// <para>An extension point, as in QuantLib: every payoff derives from it.</para>
/// </remarks>
public abstract class Payoff
{
    /// <summary>Initializes a new instance of the <see cref="Payoff"/> class.</summary>
    protected Payoff()
    {
    }

    /// <summary>Gets the payoff's name.</summary>
    /// <remarks>Used for output and comparison between payoffs. It is not meant to be used for writing switch-on-type code.</remarks>
    public abstract string Name { get; }

    /// <summary>Gets the payoff's description: its name and terms, its numbers in a C++ stream's default format.</summary>
    public abstract string Description { get; }

    /// <summary>The payoff at the given price of the underlying.</summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public abstract double Evaluate(double price);
}
