// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Log-mixed-linear-cubic interpolation whose cubic part is the natural cubic spline of the logarithms.
/// </summary>
/// <remarks>
/// QuantLib's convenience class: a <see cref="LogMixedLinearCubicInterpolation"/> with
/// <see cref="CubicInterpolationDerivativeApprox.Spline"/>, monotonic <see langword="false"/>, and second-derivative
/// boundary conditions of 0, which only the spline schemes read.
/// </remarks>
public sealed class LogMixedLinearCubicNaturalSpline : LogMixedLinearCubicInterpolation
{
    /// <summary>Initializes a new instance of the <see cref="LogMixedLinearCubicNaturalSpline"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 3.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <param name="n">The index of the switch point, one of the points.</param>
    /// <param name="behavior">Whether the two interpolations share the points' range or split it.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is negative, or <paramref name="behavior"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The sizes differ, there are fewer than 3 points, <paramref name="n"/> isn't the index of a point, a part has too
    /// few points, or a y value isn't positive.
    /// </exception>
    public LogMixedLinearCubicNaturalSpline(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int n,
        MixedInterpolationBehavior behavior = MixedInterpolationBehavior.ShareRanges)
        : base(
            x,
            y,
            n,
            behavior,
            CubicInterpolationDerivativeApprox.Spline,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0)
    {
    }
}
