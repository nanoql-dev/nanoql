// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The log-cubic interpolation factory and traits.</summary>
/// <remarks>
/// <para>
/// A configuration struct holding QuantLib's six parameters, which it doesn't expose, as QuantLib doesn't. QuantLib's
/// <c>LogCubic</c> has no default constructor, since its derivative approximation has no default, so its zero value
/// (<c>default</c>, <c>new LogCubic()</c>, an unassigned field or array element) is refused by every member with
/// <see cref="InvalidOperationException"/>, as <see cref="System.Collections.Immutable.ImmutableArray{T}"/>'s default is.
/// </para>
/// <para>
/// QuantLib's presets <see cref="MonotonicLogCubic"/> and <see cref="KrugerLog"/> are structs of their own, each
/// converting to the <see cref="LogCubic"/> it is.
/// </para>
/// </remarks>
public readonly struct LogCubic : IInterpolator
{
    // Null only in the zero value, which the constructor never makes.
    private readonly CubicInterpolationDerivativeApprox? da;
    private readonly bool monotonic;
    private readonly CubicInterpolationBoundaryCondition leftType;
    private readonly CubicInterpolationBoundaryCondition rightType;
    private readonly double leftValue;
    private readonly double rightValue;

    /// <summary>Initializes a new instance of the <see cref="LogCubic"/> struct.</summary>
    /// <param name="da">The first-derivative approximation of the logarithms.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter to the logarithms' cubic.</param>
    /// <param name="leftCondition">The left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">The left condition's value, where it takes one.</param>
    /// <param name="rightCondition">The right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    public LogCubic(
        CubicInterpolationDerivativeApprox da,
        bool monotonic = true,
        CubicInterpolationBoundaryCondition leftCondition = CubicInterpolationBoundaryCondition.SecondDerivative,
        double leftConditionValue = 0.0,
        CubicInterpolationBoundaryCondition rightCondition = CubicInterpolationBoundaryCondition.SecondDerivative,
        double rightConditionValue = 0.0)
    {
        // The constructor stores its enums, so it refuses a value outside each type on entry.
        EnumArguments.Check(da, nameof(da));
        EnumArguments.Check(leftCondition, nameof(leftCondition));
        EnumArguments.Check(rightCondition, nameof(rightCondition));
        this.da = da;
        this.monotonic = monotonic;
        leftType = leftCondition;
        rightType = rightCondition;
        leftValue = leftConditionValue;
        rightValue = rightConditionValue;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>.</summary>
    public static bool Global => true;

    /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
    public static int RequiredPoints => 2;

    /// <summary>Builds a <see cref="LogCubicInterpolation"/> over the given points, which it copies, with this factory's parameters.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">The sizes differ, there are too few points, or a y value isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The scheme or a condition isn't implemented, or the spline's system has a zero pivot.
    /// </exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new LogCubicInterpolation(
            x, y, DerivativeApprox(), monotonic, leftType, leftValue, rightType, rightValue);

    /// <inheritdoc/>
    public void CheckPoints(int points) =>
        CubicInterpolation.CheckConstruction(points, DerivativeApprox(), leftType, rightType);

    private CubicInterpolationDerivativeApprox DerivativeApprox() =>
        da ?? throw new InvalidOperationException(
            "a LogCubic's derivative approximation has no default: build it with a derivative approximation");
}
