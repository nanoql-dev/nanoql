// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Log-mixed-linear-cubic interpolation between discrete points: the exponential of a
/// <see cref="MixedLinearCubicInterpolation"/> of the logarithms of the y values, which must be positive.
/// </summary>
/// <remarks>
/// It copies its inputs, as every <see cref="Interpolation"/> does. It has no primitive. An extension point, as in
/// QuantLib: <see cref="LogMixedLinearCubicNaturalSpline"/> derives from it.
/// </remarks>
public class LogMixedLinearCubicInterpolation : Interpolation
{
    private readonly MixedLinearCubicInterpolation interpolation;

    /// <summary>Initializes a new instance of the <see cref="LogMixedLinearCubicInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 3.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <param name="n">The index of the switch point, one of the points.</param>
    /// <param name="behavior">Whether the two interpolations share the points' range or split it.</param>
    /// <param name="da">The cubic part's first-derivative approximation of the logarithms.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter to the cubic part.</param>
    /// <param name="leftC">The cubic part's left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">
    /// The left condition's value, where it takes one; <see langword="null"/> with a first-derivative condition to
    /// match the linear part's derivative at the switch point (<see cref="MixedLinearCubicInterpolation"/>).
    /// </param>
    /// <param name="rightC">The cubic part's right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The sizes differ, there are fewer than 3 points, the derivatives are matched with
    /// <see cref="MixedInterpolationBehavior.ShareRanges"/>, <paramref name="n"/> isn't the index of a point, a part
    /// has too few points, or a y value isn't positive.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The cubic part's scheme or a condition isn't implemented, or its spline's system has a zero pivot.
    /// </exception>
    public LogMixedLinearCubicInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int n,
        MixedInterpolationBehavior behavior,
        CubicInterpolationDerivativeApprox da,
        bool monotonic,
        CubicInterpolationBoundaryCondition leftC,
        double? leftConditionValue,
        CubicInterpolationBoundaryCondition rightC,
        double rightConditionValue)
        : base(x, y, CheckArguments(n, behavior, da, leftC, rightC))
    {
        // QuantLib builds the mixed interpolation of the logarithms, which checks its points, before update() checks the
        // y values.
        MixedLinearCubicInterpolation.CheckConstruction(x.Length, n, behavior, da, leftC, leftConditionValue, rightC);
        interpolation = new MixedLinearCubicInterpolation(
            X,
            LogInterpolationImpl.Logs(Y),
            n,
            behavior,
            da,
            monotonic,
            leftC,
            leftConditionValue,
            rightC,
            rightConditionValue);
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

    // The arguments the constructor stores, refused on entry; then the minimum LogInterpolationImpl passes to
    // templateImpl, its inner factory's.
    private static int CheckArguments(
        int n,
        MixedInterpolationBehavior behavior,
        CubicInterpolationDerivativeApprox da,
        CubicInterpolationBoundaryCondition leftC,
        CubicInterpolationBoundaryCondition rightC)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        EnumArguments.Check(behavior, nameof(behavior));
        EnumArguments.Check(da, nameof(da));
        EnumArguments.Check(leftC, nameof(leftC));
        EnumArguments.Check(rightC, nameof(rightC));
        return MixedLinearCubic.RequiredPoints;
    }
}
