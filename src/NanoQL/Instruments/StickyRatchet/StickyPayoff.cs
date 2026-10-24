// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/stickyratchet.hpp ql/instruments/stickyratchet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The sticky payoff, a single option: the double payoff with the first type +1 and no second
/// option.</summary>
public sealed class StickyPayoff : DoubleStickyRatchetPayoff
{
    /// <summary>Initializes a new instance of the <see cref="StickyPayoff"/> class.</summary>
    /// <param name="gearing1">The strike's gearing.</param>
    /// <param name="gearing2">The swaplet's gearing.</param>
    /// <param name="spread1">The strike's spread.</param>
    /// <param name="spread2">The swaplet's spread.</param>
    /// <param name="initialValue">The strike's initial value.</param>
    /// <param name="accrualFactor">The accrual factor.</param>
    public StickyPayoff(double gearing1, double gearing2, double spread1, double spread2, double initialValue, double accrualFactor)
        : base(+1.0, 0.0, gearing1, 0.0, gearing2, spread1, 0.0, spread2, initialValue, 0.0, accrualFactor)
    {
    }

    /// <inheritdoc/>
    public override string Name => "Sticky";
}
