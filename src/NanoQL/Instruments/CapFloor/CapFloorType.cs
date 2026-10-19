// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/capfloor.hpp ql/instruments/capfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The type of a cap-like instrument: QuantLib's <c>CapFloor::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes each member's name, so <see cref="object.ToString"/> gives its text and it isn't
/// ported; for a value outside the enum it fails "unknown CapFloor::Type (n)", the text the constructors' check of a
/// value outside the enum keeps.
/// </remarks>
public enum CapFloorType
{
    /// <summary>A cap: a strip of caplets, calls on the floating rate.</summary>
    Cap = 0,

    /// <summary>A floor: a strip of floorlets, puts on the floating rate.</summary>
    Floor = 1,

    /// <summary>A collar: long a cap and short a floor.</summary>
    Collar = 2,
}
