// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvariancesurface.hpp ql/termstructures/volatility/equityfx/blackvariancesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// How a <see cref="BlackVarianceSurface"/> extrapolates beyond its strikes: QuantLib's
/// <c>BlackVarianceSurface::Extrapolation</c>.
/// </summary>
public enum BlackVarianceSurfaceExtrapolation
{
    /// <summary>The variance at the nearest strike, held flat.</summary>
    ConstantExtrapolation = 0,

    /// <summary>The interpolator's own extrapolation.</summary>
    InterpolatorDefaultExtrapolation = 1,
}
