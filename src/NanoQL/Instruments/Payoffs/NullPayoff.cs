// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A dummy payoff: it has a name and a description, and refuses to be evaluated.</summary>
public sealed class NullPayoff : Payoff
{
    /// <inheritdoc/>
    public override string Name => "Null";

    /// <inheritdoc/>
    public override string Description => Name;

    /// <summary>Refuses to evaluate: the payoff is a dummy.</summary>
    /// <param name="price">The underlying's price, unread.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always: "dummy payoff given".</exception>
    public override double Evaluate(double price) => throw new NanoQLException("dummy payoff given");
}
