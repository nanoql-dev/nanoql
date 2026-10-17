// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Log-cubic interpolation with Kruger's first-derivative approximation of the logarithms.
/// </summary>
/// <remarks>
/// QuantLib's convenience class: a <see cref="LogCubicInterpolation"/> with
/// <see cref="CubicInterpolationDerivativeApprox.Kruger"/>, monotonic <see langword="false"/>, and second-derivative
/// boundary conditions of 0, which only the spline schemes read.
/// </remarks>
public sealed class KrugerLogCubic : LogCubicInterpolation
{
    /// <summary>Initializes a new instance of the <see cref="KrugerLogCubic"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <exception cref="ArgumentException">The sizes differ, there are too few points, or a y value isn't positive.</exception>
    public KrugerLogCubic(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(
            x,
            y,
            CubicInterpolationDerivativeApprox.Kruger,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0)
    {
    }
}
