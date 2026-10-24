// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/chebyshevinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The Chebyshev nodes' kind: QuantLib's <c>ChebyshevInterpolation::PointsType</c>.</summary>
public enum ChebyshevInterpolationPointsType
{
    /// <summary>The roots of the Chebyshev polynomial of the first kind, −cos((i + ½)π/n).</summary>
    FirstKind,

    /// <summary>The extrema of the Chebyshev polynomial of the first kind, −cos(iπ/(n − 1)), the ends included.</summary>
    SecondKind,
}
