// SPDX-FileCopyrightText: Copyright (C) 2008 Frank Hövermann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/extendedblackvariancesurface.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>How an <see cref="ExtendedBlackVarianceSurface"/> extrapolates beyond its strikes.</summary>
[Experimental("NQL0001")]
public enum ExtendedBlackVarianceSurfaceExtrapolation
{
    /// <summary>The variance at the nearest strike, held flat.</summary>
    ConstantExtrapolation = 0,

    /// <summary>The interpolator's own extrapolation.</summary>
    InterpolatorDefaultExtrapolation = 1,
}
