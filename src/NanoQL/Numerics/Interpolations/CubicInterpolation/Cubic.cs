// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2004, 2008, 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 Sylvain Bertrand
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2016 Nicholas Bertocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/cubicinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The cubic-interpolation factory and traits.</summary>
/// <remarks>
/// A configuration struct holding QuantLib's six parameters, which it doesn't expose, as QuantLib doesn't. Its zero value
/// (<c>default</c>, <c>new Cubic()</c>, an unassigned field or array element) is QuantLib's <c>Cubic()</c>: Kruger's
/// approximation, not monotonic, and second-derivative boundary conditions of 0.
/// </remarks>
public readonly struct Cubic : IInterpolator
{
    // QuantLib's defaults aren't zero for the derivative approximation and the boundary conditions, so they are stored
    // as nullable enums, whose zero value, null, reads as the default (Kruger, and SecondDerivative).
    private readonly CubicInterpolationDerivativeApprox? da;
    private readonly bool monotonic;
    private readonly CubicInterpolationBoundaryCondition? leftType;
    private readonly CubicInterpolationBoundaryCondition? rightType;
    private readonly double leftValue;
    private readonly double rightValue;

    /// <summary>Initializes a new instance of the <see cref="Cubic"/> struct.</summary>
    /// <param name="da">The first-derivative approximation.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter.</param>
    /// <param name="leftCondition">The left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">The left condition's value, where it takes one.</param>
    /// <param name="rightCondition">The right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    public Cubic(
        CubicInterpolationDerivativeApprox da = CubicInterpolationDerivativeApprox.Kruger,
        bool monotonic = false,
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

    /// <summary>Builds a <see cref="CubicInterpolation"/> over the given points, which it copies, with this factory's parameters.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ, or there are too few points.</exception>
    /// <exception cref="NanoQLException">
    /// The scheme or a condition isn't implemented, or the spline's system has a zero pivot.
    /// </exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new CubicInterpolation(
            x,
            y,
            da ?? CubicInterpolationDerivativeApprox.Kruger,
            monotonic,
            leftType ?? CubicInterpolationBoundaryCondition.SecondDerivative,
            leftValue,
            rightType ?? CubicInterpolationBoundaryCondition.SecondDerivative,
            rightValue);

    /// <inheritdoc/>
    public void CheckPoints(int points) =>
        CubicInterpolation.CheckConstruction(
            points,
            da ?? CubicInterpolationDerivativeApprox.Kruger,
            leftType ?? CubicInterpolationBoundaryCondition.SecondDerivative,
            rightType ?? CubicInterpolationBoundaryCondition.SecondDerivative);
}
