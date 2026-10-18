// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/volatilitytype.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>The type of a volatility: shifted lognormal (Black) or normal (Bachelier).</summary>
/// <remarks>
/// Its <see cref="object.ToString"/> gives QuantLib's stream text, "ShiftedLognormal" or "Normal"; QuantLib's
/// "Unknown volatility type (n)" is reachable only for a value outside the enum, which members refuse on entry.
/// </remarks>
public enum VolatilityType
{
    /// <summary>A shifted lognormal volatility.</summary>
    ShiftedLognormal,

    /// <summary>A normal volatility.</summary>
    Normal,
}
