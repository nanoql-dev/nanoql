// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Log-cubic interpolation between discrete points: the exponential of a <see cref="CubicInterpolation"/> of the
/// logarithms of the y values, which must be positive.
/// </summary>
/// <remarks>
/// It copies its inputs, as every <see cref="Interpolation"/> does. It has no primitive. An extension point, as in
/// QuantLib: the named log-cubic interpolations derive from it.
/// </remarks>
public class LogCubicInterpolation : Interpolation
{
    private readonly CubicInterpolation interpolation;

    /// <summary>Initializes a new instance of the <see cref="LogCubicInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2, and at least 4 for Akima's approximation or a Lagrange condition.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <param name="da">The first-derivative approximation of the logarithms.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter to the logarithms' cubic.</param>
    /// <param name="leftC">The left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">The left condition's value, where it takes one.</param>
    /// <param name="rightC">The right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentException">The sizes differ, there are too few points, or a y value isn't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">
    /// The scheme or a condition isn't implemented, or the spline's system has a zero pivot, or a not-a-knot end under the
    /// spline has only 2 points, where QuantLib reads past its arrays (a divergence: nanoql refuses it).
    /// </exception>
    public LogCubicInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        CubicInterpolationDerivativeApprox da,
        bool monotonic,
        CubicInterpolationBoundaryCondition leftC,
        double leftConditionValue,
        CubicInterpolationBoundaryCondition rightC,
        double rightConditionValue)
        : base(x, y, Cubic.RequiredPoints)
    {
        EnumArguments.Check(da, nameof(da));
        EnumArguments.Check(leftC, nameof(leftC));
        EnumArguments.Check(rightC, nameof(rightC));

        // QuantLib builds the cubic of the logarithms, which checks its points, before update() checks the y values.
        CubicInterpolation.CheckPoints(x.Length, da, leftC, rightC);
        interpolation = new CubicInterpolation(
            X, LogInterpolationImpl.Logs(Y), da, monotonic, leftC, leftConditionValue, rightC, rightConditionValue);
    }

    /// <inheritdoc/>
    protected sealed override double ValueCore(double x) => LogInterpolationImpl.Value(interpolation, x);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the log interpolations have no primitive.</exception>
    protected sealed override double PrimitiveCore(double x) => LogInterpolationImpl.Primitive();

    /// <inheritdoc/>
    protected sealed override double DerivativeCore(double x) => LogInterpolationImpl.Derivative(interpolation, x);

    /// <inheritdoc/>
    protected sealed override double SecondDerivativeCore(double x) =>
        LogInterpolationImpl.SecondDerivative(interpolation, x);
}
