// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/inflationcapfloor.hpp ql/instruments/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The type of a year-on-year inflation cap-like instrument: QuantLib's
/// <c>YoYInflationCapFloor::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes "YoYInflationCap", "YoYInflationFloor" and "YoYInflationCollar", not the members'
/// names, so it is <see cref="InflationCapFloorFunctions.ToString(YoYInflationCapFloorType)"/>.
/// </remarks>
public enum YoYInflationCapFloorType
{
    /// <summary>A cap: a strip of caplets, calls on the year-on-year rate.</summary>
    Cap = 0,

    /// <summary>A floor: a strip of floorlets, puts on the year-on-year rate.</summary>
    Floor = 1,

    /// <summary>A collar: long a cap and short a floor.</summary>
    Collar = 2,
}
