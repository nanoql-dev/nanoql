// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/fixedlocalvolsurface.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// How a <see cref="FixedLocalVolSurface"/> extrapolates beyond a time's strikes: QuantLib's
/// <c>FixedLocalVolSurface::Extrapolation</c>.
/// </summary>
public enum FixedLocalVolSurfaceExtrapolation
{
    /// <summary>The local volatility at the nearest strike, held flat.</summary>
    ConstantExtrapolation = 0,

    /// <summary>The interpolator's own extrapolation.</summary>
    InterpolatorDefaultExtrapolation = 1,
}
