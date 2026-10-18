// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/mixedinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Mixed linear/cubic interpolation between discrete points: linear before the switch point, the n-th point, and cubic
/// from it on.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="MixedInterpolationBehavior.ShareRanges"/> both interpolations are defined over all the points; with
/// <see cref="MixedInterpolationBehavior.SplitRanges"/> the linear one over the points up to the switch point and the
/// cubic one over the points from it on. Under <see cref="MixedInterpolationBehavior.SplitRanges"/>, a
/// <see cref="CubicInterpolationBoundaryCondition.FirstDerivative"/> left condition whose value is
/// <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>) matches the cubic's first derivative at the switch
/// point to the linear part's. Under any other left condition, a <see langword="null"/> value is QuantLib's
/// <c>Null&lt;Real&gt;()</c> as a number, 3.4028234663852886e38, which QuantLib's cubic reads as the condition's value.
/// </para>
/// <para>
/// It copies its inputs, as every <see cref="Interpolation"/> does. An extension point, as in QuantLib: the named mixed
/// interpolations derive from it.
/// </para>
/// </remarks>
public class MixedLinearCubicInterpolation : Interpolation
{
    // QuantLib's Null<Real>(), the largest float, which its cubic reads as a value where it isn't a sentinel.
    private const double NullReal = 3.4028234663852886e38;

    private readonly int n;
    private readonly Interpolation interpolation1;
    private readonly Interpolation interpolation2;

    /// <summary>Initializes a new instance of the <see cref="MixedLinearCubicInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <param name="n">The index of the switch point, one of the points.</param>
    /// <param name="behavior">Whether the two interpolations share the points' range or split it.</param>
    /// <param name="da">The cubic part's first-derivative approximation.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter to the cubic part.</param>
    /// <param name="leftC">The cubic part's left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">
    /// The left condition's value, where it takes one; <see langword="null"/> with a first-derivative condition to
    /// match the linear part's derivative at the switch point.
    /// </param>
    /// <param name="rightC">The cubic part's right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The derivatives are matched with <see cref="MixedInterpolationBehavior.ShareRanges"/>, the sizes differ, there
    /// are no points, <paramref name="n"/> isn't the index of a point, or a part has too few points.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The cubic part's scheme or a condition isn't implemented, its spline's system has a zero pivot, or a not-a-knot
    /// end under its spline has only 2 points (a divergence: QuantLib reads past its arrays there).
    /// </exception>
    public MixedLinearCubicInterpolation(
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
        : base(x, y, CheckArguments(n, behavior, da, leftC, leftConditionValue, rightC))
    {
        // MixedInterpolationImpl: the switch point is read, so it must be one of the points
        CheckSwitchPoint(X.Length, n);
        this.n = n;
        ReadOnlySpan<double> xs = X, ys = Y;
        if (behavior == MixedInterpolationBehavior.ShareRanges)
        {
            interpolation1 = new LinearInterpolation(xs, ys);
            interpolation2 = new CubicInterpolation(
                xs, ys, da, monotonic, leftC, leftConditionValue ?? NullReal, rightC, rightConditionValue);
        }
        else
        {
            interpolation1 = new LinearInterpolation(xs[..(n + 1)], ys[..(n + 1)]);

            // update(): the switch function sets the cubic's left value before the cubic is computed
            double leftValue = MatchesDerivatives(leftC, leftConditionValue)
                ? interpolation1.Derivative(xs[n], true)
                : leftConditionValue ?? NullReal;
            interpolation2 = new CubicInterpolation(
                xs[n..], ys[n..], da, monotonic, leftC, leftValue, rightC, rightConditionValue);
        }
    }

    // What MixedLinearCubicInterpolation refuses for a point count alone, in QuantLib's order: its matching of the
    // derivatives, the minimum MixedInterpolationImpl passes to templateImpl (1), its switch point, then the two parts'
    // constructions on their points, each with its own checks (the cubic's as Cubic.CheckPoints runs them).
    // MixedLinearCubic.CheckPoints and LogMixedLinearCubic.CheckPoints run it.
    internal static void CheckConstruction(
        int points,
        int n,
        MixedInterpolationBehavior behavior,
        CubicInterpolationDerivativeApprox da,
        CubicInterpolationBoundaryCondition leftC,
        double? leftConditionValue,
        CubicInterpolationBoundaryCondition rightC)
    {
        CheckMatching(behavior, leftC, leftConditionValue);
        CheckRequiredPoints(points, 1);
        CheckSwitchPoint(points, n);
        if (behavior == MixedInterpolationBehavior.ShareRanges)
        {
            CheckRequiredPoints(points, Linear.RequiredPoints);
            CubicInterpolation.CheckConstruction(points, da, leftC, rightC);
        }
        else
        {
            CheckRequiredPoints(n + 1, Linear.RequiredPoints);
            CubicInterpolation.CheckConstruction(points - n, da, leftC, rightC);
        }
    }

    /// <inheritdoc/>
    protected sealed override double ValueCore(double x) =>
        x < X[n] ? interpolation1.Evaluate(x, true) : interpolation2.Evaluate(x, true);

    /// <inheritdoc/>
    protected sealed override double PrimitiveCore(double x)
    {
        if (x < X[n])
        {
            return interpolation1.Primitive(x, true);
        }

        return interpolation2.Primitive(x, true) - interpolation2.Primitive(X[n], true)
            + interpolation1.Primitive(X[n], true);
    }

    /// <inheritdoc/>
    protected sealed override double DerivativeCore(double x) =>
        x < X[n] ? interpolation1.Derivative(x, true) : interpolation2.Derivative(x, true);

    /// <inheritdoc/>
    protected sealed override double SecondDerivativeCore(double x) =>
        x < X[n] ? interpolation1.SecondDerivative(x, true) : interpolation2.SecondDerivative(x, true);

    // The constructor's checks before MixedInterpolationImpl's: the arguments it stores or switches on, on entry, and
    // QuantLib's check of the derivatives' matching; then the minimum MixedInterpolationImpl passes to templateImpl.
    private static int CheckArguments(
        int n,
        MixedInterpolationBehavior behavior,
        CubicInterpolationDerivativeApprox da,
        CubicInterpolationBoundaryCondition leftC,
        double? leftConditionValue,
        CubicInterpolationBoundaryCondition rightC)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        EnumArguments.Check(behavior, nameof(behavior));
        EnumArguments.Check(da, nameof(da));
        EnumArguments.Check(leftC, nameof(leftC));
        EnumArguments.Check(rightC, nameof(rightC));
        CheckMatching(behavior, leftC, leftConditionValue);
        return 1;
    }

    private static bool MatchesDerivatives(CubicInterpolationBoundaryCondition leftC, double? leftConditionValue) =>
        leftC == CubicInterpolationBoundaryCondition.FirstDerivative && leftConditionValue is null;

    private static void CheckMatching(
        MixedInterpolationBehavior behavior, CubicInterpolationBoundaryCondition leftC, double? leftConditionValue)
    {
        if (!(!MatchesDerivatives(leftC, leftConditionValue) || behavior == MixedInterpolationBehavior.SplitRanges))
        {
            throw new ArgumentException("matching derivatives is only supported with SplitRanges", nameof(behavior));
        }
    }

    // The switch point xBegin_ + n is dereferenced in update() and value(), so it must be a valid element.
    private static void CheckSwitchPoint(int points, int n)
    {
        int maxN = points - 1;
        if (!(n <= maxN))
        {
            throw new ArgumentException($"n is too large ({n} > {maxN})", nameof(n));
        }
    }
}
