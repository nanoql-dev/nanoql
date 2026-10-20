// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/inflationcapfloor.hpp ql/instruments/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The free functions of QuantLib's <c>inflationcapfloor.hpp</c>.</summary>
public static class InflationCapFloorFunctions
{
    /// <summary>A year-on-year cap-like instrument's type, as QuantLib's stream operator writes it.</summary>
    /// <param name="t">The type.</param>
    /// <returns>"YoYInflationCap", "YoYInflationFloor" or "YoYInflationCollar".</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="t"/> isn't a member of its type, with QuantLib's
    /// "unknown YoYInflationCapFloor::Type (n)".</exception>
    public static string ToString(YoYInflationCapFloorType t) => t switch
    {
        YoYInflationCapFloorType.Cap => "YoYInflationCap",
        YoYInflationCapFloorType.Floor => "YoYInflationFloor",
        YoYInflationCapFloorType.Collar => "YoYInflationCollar",
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, $"unknown YoYInflationCapFloor::Type ({(int)t})"),
    };
}
