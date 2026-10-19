// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/capfloor.hpp ql/instruments/capfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>A collar: long a cap and short a floor on the same floating leg.</summary>
/// <remarks>As in QuantLib, cap rates below the floor rates aren't refused.</remarks>
public sealed class Collar : CapFloor
{
    /// <summary>Initializes a new instance of the <see cref="Collar"/> class.</summary>
    /// <param name="floatingLeg">The floating leg.</param>
    /// <param name="capRates">The cap rates, extended to the leg's length with the last one.</param>
    /// <param name="floorRates">The floor rates, extended to the leg's length with the last one.</param>
    /// <exception cref="ArgumentException">
    /// The leg is <c>default</c> or holds a null cash flow, or there are no cap rates or no floor rates.
    /// </exception>
    /// <exception cref="ArgumentNullException">A rate sequence is <see langword="null"/>.</exception>
    public Collar(ImmutableArray<CashFlow> floatingLeg, IEnumerable<double> capRates, IEnumerable<double> floorRates)
        : base(CapFloorType.Collar, floatingLeg, capRates, floorRates)
    {
    }
}
