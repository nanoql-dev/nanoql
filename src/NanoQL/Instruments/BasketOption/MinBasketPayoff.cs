// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/basketoption.hpp ql/instruments/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A basket payoff on the smallest of the assets' prices.</summary>
/// <remarks>
/// An empty basket is refused, where QuantLib dereferences the end of the empty range
/// (<c>Divergences/BasketPayoffEmptyTests.cs</c>).
/// </remarks>
public sealed class MinBasketPayoff : BasketPayoff
{
    /// <summary>Initializes a new instance of the <see cref="MinBasketPayoff"/> class.</summary>
    /// <param name="basePayoff">The one-dimensional payoff applied to the minimum.</param>
    /// <exception cref="ArgumentNullException"><paramref name="basePayoff"/> is <see langword="null"/>.</exception>
    public MinBasketPayoff(Payoff basePayoff)
        : base(basePayoff)
    {
    }

    /// <summary>The smallest of the prices, the first of equal ones, as <c>std::min_element</c> keeps it.</summary>
    /// <param name="a">The assets' prices.</param>
    /// <returns>The minimum.</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/> is empty.</exception>
    public override double Accumulate(ReadOnlySpan<double> a)
    {
        // QuantLib reads *std::min_element of an empty range, its end, unchecked
        if (a.IsEmpty)
        {
            throw new ArgumentException("empty basket given", nameof(a));
        }

        // std::min_element: the first element no later one is less than
        double smallest = a[0];
        for (int i = 1; i < a.Length; ++i)
        {
            if (a[i] < smallest)
            {
                smallest = a[i];
            }
        }

        return smallest;
    }
}
