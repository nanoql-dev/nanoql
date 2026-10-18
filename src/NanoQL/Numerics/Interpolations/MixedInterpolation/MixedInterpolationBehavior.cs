// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/mixedinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>How a mixed interpolation defines its two interpolations: QuantLib's <c>MixedInterpolation::Behavior</c>.</summary>
public enum MixedInterpolationBehavior
{
    /// <summary>Define both interpolations over the whole range of the points. This is the default behavior.</summary>
    ShareRanges,

    /// <summary>
    /// Define the first interpolation over the first part of the range, and the second interpolation over the second
    /// part.
    /// </summary>
    SplitRanges,
}
