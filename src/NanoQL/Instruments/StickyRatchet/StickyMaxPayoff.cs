// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/stickyratchet.hpp ql/instruments/stickyratchet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The sticky-max payoff, a double option: the double payoff with the types +1 and −1.</summary>
public sealed class StickyMaxPayoff : DoubleStickyRatchetPayoff
{
    /// <summary>Initializes a new instance of the <see cref="StickyMaxPayoff"/> class.</summary>
    /// <param name="gearing1">The first strike's gearing.</param>
    /// <param name="gearing2">The second strike's gearing.</param>
    /// <param name="gearing3">The swaplet's gearing.</param>
    /// <param name="spread1">The first strike's spread.</param>
    /// <param name="spread2">The second strike's spread.</param>
    /// <param name="spread3">The swaplet's spread.</param>
    /// <param name="initialValue1">The first strike's initial value.</param>
    /// <param name="initialValue2">The second strike's initial value.</param>
    /// <param name="accrualFactor">The accrual factor.</param>
    public StickyMaxPayoff(
        double gearing1,
        double gearing2,
        double gearing3,
        double spread1,
        double spread2,
        double spread3,
        double initialValue1,
        double initialValue2,
        double accrualFactor)
        : base(+1.0, -1.0, gearing1, gearing2, gearing3, spread1, spread2, spread3, initialValue1, initialValue2, accrualFactor)
    {
    }

    /// <inheritdoc/>
    public override string Name => "StickyMax";
}
