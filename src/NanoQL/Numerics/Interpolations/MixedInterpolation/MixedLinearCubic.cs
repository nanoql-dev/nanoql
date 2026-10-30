// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/mixedinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The mixed linear/cubic interpolation factory and traits.</summary>
/// <remarks>
/// A configuration struct holding QuantLib's eight parameters, which it doesn't expose, as QuantLib doesn't. QuantLib's
/// <c>MixedLinearCubic</c> has no default constructor, since its switch point, behavior and derivative approximation
/// have no defaults, so its zero value (<c>default</c>, <c>new MixedLinearCubic()</c>, an unassigned field or array
/// element) is refused by every member with <see cref="InvalidOperationException"/>, as
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/>'s default is.
/// </remarks>
public readonly struct MixedLinearCubic : IInterpolator
{
    private readonly int n;
    private readonly MixedInterpolationBehavior behavior;

    // Null only in the zero value, which the constructor never makes.
    private readonly CubicInterpolationDerivativeApprox? da;
    private readonly bool monotonic;
    private readonly CubicInterpolationBoundaryCondition leftType;
    private readonly CubicInterpolationBoundaryCondition rightType;
    private readonly double? leftValue;
    private readonly double rightValue;

    /// <summary>Initializes a new instance of the <see cref="MixedLinearCubic"/> struct.</summary>
    /// <param name="n">The index of the switch point, one of the points.</param>
    /// <param name="behavior">Whether the two interpolations share the points' range or split it.</param>
    /// <param name="da">The cubic part's first-derivative approximation.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter to the cubic part.</param>
    /// <param name="leftCondition">The cubic part's left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">
    /// The left condition's value, where it takes one; <see langword="null"/> with a first-derivative condition to
    /// match the linear part's derivative at the switch point (<see cref="MixedLinearCubicInterpolation"/>).
    /// </param>
    /// <param name="rightCondition">The cubic part's right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    public MixedLinearCubic(
        int n,
        MixedInterpolationBehavior behavior,
        CubicInterpolationDerivativeApprox da,
        bool monotonic = true,
        CubicInterpolationBoundaryCondition leftCondition = CubicInterpolationBoundaryCondition.SecondDerivative,
        double? leftConditionValue = 0.0,
        CubicInterpolationBoundaryCondition rightCondition = CubicInterpolationBoundaryCondition.SecondDerivative,
        double rightConditionValue = 0.0)
    {
        // The constructor stores its arguments, so it refuses a negative n and a value outside each enum on entry.
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        EnumArguments.Check(behavior, nameof(behavior));
        EnumArguments.Check(da, nameof(da));
        EnumArguments.Check(leftCondition, nameof(leftCondition));
        EnumArguments.Check(rightCondition, nameof(rightCondition));
        this.n = n;
        this.behavior = behavior;
        this.da = da;
        this.monotonic = monotonic;
        leftType = leftCondition;
        rightType = rightCondition;
        leftValue = leftConditionValue;
        rightValue = rightConditionValue;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>.</summary>
    public static bool Global => true;

    /// <summary>Gets the fewest points the interpolation takes: 3, QuantLib's figure.</summary>
    /// <remarks>
    /// The interpolation itself takes fewer, depending on its switch point and behavior: its construction checks
    /// (<see cref="CheckPoints"/>) decide.
    /// </remarks>
    public static int RequiredPoints => 3;

    /// <summary>
    /// Builds a <see cref="MixedLinearCubicInterpolation"/> over the given points, which it copies, with this factory's
    /// parameters.
    /// </summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">
    /// The derivatives are matched with <see cref="MixedInterpolationBehavior.ShareRanges"/>, the sizes differ, the
    /// switch point isn't one of the points, or a part has too few points.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The cubic part's scheme or a condition isn't implemented, or its spline's system has a zero pivot.
    /// </exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new MixedLinearCubicInterpolation(
            x, y, n, behavior, DerivativeApprox(), monotonic, leftType, leftValue, rightType, rightValue);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    public void CheckPoints(int points) =>
        MixedLinearCubicInterpolation.CheckConstruction(
            points, n, behavior, DerivativeApprox(), leftType, leftValue, rightType);

    private CubicInterpolationDerivativeApprox DerivativeApprox() =>
        da ?? throw new InvalidOperationException(
            "a MixedLinearCubic has no default: build it with its switch point, behavior and derivative approximation");
}
