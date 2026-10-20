// SPDX-FileCopyrightText: Copyright (C) 2025 AcadiaSoft Inc.
// SPDX-FileCopyrightText: Copyright (C) 2026 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.hpp ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// How a Black volatility structure extrapolates beyond its last time: QuantLib's
/// <c>BlackVolTimeExtrapolation::Type</c>.
/// </summary>
public enum BlackVolTimeExtrapolationType
{
    /// <summary>The last available volatility, held flat.</summary>
    FlatVolatility = 0,

    /// <summary>The underlying curve's or surface's own extrapolation, whatever method it uses.</summary>
    UseInterpolator = 1,

    /// <summary>The variance extrapolated linearly from the last two available nodes.</summary>
    LinearVariance = 2,
}
