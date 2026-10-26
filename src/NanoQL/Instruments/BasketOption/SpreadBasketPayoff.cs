// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/basketoption.hpp ql/instruments/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A basket payoff on the spread of two assets' prices, the first less the second.</summary>
public sealed class SpreadBasketPayoff : BasketPayoff
{
    /// <summary>Initializes a new instance of the <see cref="SpreadBasketPayoff"/> class.</summary>
    /// <param name="basePayoff">The one-dimensional payoff applied to the spread.</param>
    /// <exception cref="ArgumentNullException"><paramref name="basePayoff"/> is <see langword="null"/>.</exception>
    public SpreadBasketPayoff(Payoff basePayoff)
        : base(basePayoff)
    {
    }

    /// <summary>The first price less the second.</summary>
    /// <param name="a">The two assets' prices.</param>
    /// <returns>The spread.</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/> doesn't hold two prices.</exception>
    public override double Accumulate(ReadOnlySpan<double> a)
    {
        if (!(a.Length == 2))
        {
            throw new ArgumentException("payoff is only defined for two underlyings", nameof(a));
        }

        return a[0] - a[1];
    }
}
