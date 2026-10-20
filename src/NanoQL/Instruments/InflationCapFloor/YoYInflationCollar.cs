// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/inflationcapfloor.hpp ql/instruments/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>A year-on-year inflation collar: long a cap and short a floor on a year-on-year leg.</summary>
public sealed class YoYInflationCollar : YoYInflationCapFloor
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationCollar"/> class.</summary>
    /// <param name="yoyLeg">The year-on-year leg.</param>
    /// <param name="capRates">The cap rates, extended to the leg's length with the last one.</param>
    /// <param name="floorRates">The floor rates, extended to the leg's length with the last one.</param>
    /// <exception cref="ArgumentException">
    /// The leg is <c>default</c> or holds a null cash flow, or either rate sequence is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">A rate sequence is <see langword="null"/>.</exception>
    public YoYInflationCollar(
        ImmutableArray<CashFlow> yoyLeg, IEnumerable<double> capRates, IEnumerable<double> floorRates)
        : base(YoYInflationCapFloorType.Collar, yoyLeg, capRates, floorRates, nameof(capRates), nameof(floorRates))
    {
    }
}
