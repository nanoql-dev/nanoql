// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/inflationcapfloor.hpp ql/instruments/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>A year-on-year inflation floor: a strip of floorlets on a year-on-year leg.</summary>
public sealed class YoYInflationFloor : YoYInflationCapFloor
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationFloor"/> class.</summary>
    /// <param name="yoyLeg">The year-on-year leg.</param>
    /// <param name="exerciseRates">The floor rates, extended to the leg's length with the last one.</param>
    /// <exception cref="ArgumentException">
    /// The leg is <c>default</c> or holds a null cash flow, or there are no rates ("no floor rates given").
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="exerciseRates"/> is <see langword="null"/>.</exception>
    public YoYInflationFloor(ImmutableArray<CashFlow> yoyLeg, IEnumerable<double> exerciseRates)
        : base(YoYInflationCapFloorType.Floor, yoyLeg, [], exerciseRates, "capRates", nameof(exerciseRates))
    {
    }
}
