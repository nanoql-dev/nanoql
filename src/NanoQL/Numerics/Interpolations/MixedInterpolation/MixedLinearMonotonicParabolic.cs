// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/mixedinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Mixed linear/cubic interpolation whose cubic part takes the parabolic derivative approximation with Hyman's
/// monotonicity filter.
/// </summary>
/// <remarks>
/// QuantLib's convenience class: a <see cref="MixedLinearCubicInterpolation"/> with
/// <see cref="CubicInterpolationDerivativeApprox.Parabolic"/>, monotonic <see langword="true"/>, and second-derivative boundary
/// conditions of 0, which only the spline schemes read.
/// </remarks>
public sealed class MixedLinearMonotonicParabolic : MixedLinearCubicInterpolation
{
    /// <summary>Initializes a new instance of the <see cref="MixedLinearMonotonicParabolic"/> class.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <param name="n">The index of the switch point, one of the points.</param>
    /// <param name="behavior">Whether the two interpolations share the points' range or split it.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is negative, or <paramref name="behavior"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The sizes differ, there are no points, <paramref name="n"/> isn't the index of a point, or a part has too few
    /// points.
    /// </exception>
    public MixedLinearMonotonicParabolic(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int n,
        MixedInterpolationBehavior behavior = MixedInterpolationBehavior.ShareRanges)
        : base(
            x,
            y,
            n,
            behavior,
            CubicInterpolationDerivativeApprox.Parabolic,
            true,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0)
    {
    }
}
