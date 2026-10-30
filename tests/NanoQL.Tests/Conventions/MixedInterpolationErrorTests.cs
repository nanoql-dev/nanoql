// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the mixed and log-mixed interpolations' refusals, in QuantLib's order, which the vectors'
/// <c>{"throws": true}</c> can't tell apart (playbook §9.4): a negative switch point and a behavior outside its enum
/// throw <see cref="ArgumentOutOfRangeException"/>; QuantLib's checks of the arguments (the derivatives' matching, the
/// switch point, too few points for a part) throw <see cref="ArgumentException"/> naming them; a cubic part's scheme or
/// condition QuantLib doesn't implement, and the log interpolations' primitive, throw <see cref="NanoQLException"/>.
/// The factories' <see cref="IInterpolator.CheckPoints"/> refuses what the construction refuses, alike.
/// </summary>
public class MixedInterpolationErrorTests
{
    private static readonly double[] X = [0.0, 1.0, 2.0, 3.0, 4.0];
    private static readonly double[] Y = [1.0, 0.9, 1.4, 1.1, 2.0];

    private const MixedInterpolationBehavior Share = MixedInterpolationBehavior.ShareRanges;
    private const MixedInterpolationBehavior Split = MixedInterpolationBehavior.SplitRanges;
    private const CubicInterpolationDerivativeApprox Spline = CubicInterpolationDerivativeApprox.Spline;
    private const CubicInterpolationBoundaryCondition First = CubicInterpolationBoundaryCondition.FirstDerivative;
    private const CubicInterpolationBoundaryCondition Second = CubicInterpolationBoundaryCondition.SecondDerivative;

    [Fact]
    public void RefusalsThrowTheirTypes()
    {
        // the arguments the constructors store, on entry
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new MixedLinearCubicNaturalSpline(X, Y, -1)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new MixedLinearCubic(-1, Share, Spline)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new MonotonicLogMixedLinearCubic(-1)).ParamName);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LogMixedLinearCubic(1, (MixedInterpolationBehavior)5, Spline));
        Assert.Equal("behavior", e.ParamName);
        Assert.StartsWith("unknown mixed-interpolation behavior: 5", e.Message, StringComparison.Ordinal);
        Assert.Equal(
            "behavior",
            Assert.Throws<ArgumentOutOfRangeException>(() => new KrugerLogMixedLinearCubic(1, (MixedInterpolationBehavior)5)).ParamName);

        // QuantLib's checks, in its order: the derivatives' matching before the points
        AssertArgument(
            "behavior",
            "matching derivatives is only supported with SplitRanges",
            () => new MixedLinearCubicInterpolation([], [], 1, Share, Spline, false, First, null, Second, 0.0));
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 1 required, 0 provided",
            () => new MixedLinearCubicInterpolation([], [], 0, Split, Spline, false, First, null, Second, 0.0));
        AssertArgument("n", "n is too large (5 > 4)", () => new MixedLinearCubicNaturalSpline(X, Y, 5));
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 2 required, 1 provided",
            () => new MixedLinearKrugerCubic(X, Y, 0, Split));
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 2 required, 1 provided",
            () => new MixedLinearParabolic(X, Y, 4, Split));
        AssertArgument(
            "x",
            "Akima approximation requires at least 4 points (3 are given)",
            () => new MixedLinearCubic(2, Split, CubicInterpolationDerivativeApprox.Akima).Interpolate(X, Y));

        // the cubic part's update
        Assert.Equal(
            "this end condition is not implemented yet",
            Assert.Throws<NanoQLException>(
                () => new MixedLinearCubic(1, Share, Spline, false, CubicInterpolationBoundaryCondition.Periodic)
                    .Interpolate(X, Y)).Message);

        // the log interpolations: their minimum of 3 points before the mixed interpolation's checks, the y values after
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 3 required, 2 provided",
            () => new LogMixedLinearCubicInterpolation([0.0, 1.0], [1.0, 2.0], 5, Share, Spline, false, First, null, Second, 0.0));
        AssertArgument("n", "n is too large (5 > 4)", () => new LogMixedLinearCubicNaturalSpline(X, [1.0, 0.0, 1.4, 1.1, 2.0], 5));
        AssertArgument("y", "invalid value (0) at index 1", () => new LogMixedLinearCubicNaturalSpline(X, [1.0, 0.0, 1.4, 1.1, 2.0], 1));
        Assert.Equal(
            "LogInterpolation primitive not implemented",
            Assert.Throws<NanoQLException>(() => new KrugerLogMixedLinearCubic(1).Interpolate(X, Y).Primitive(1.5)).Message);
    }

    [Fact]
    public void CheckPointsRefusesWhatTheConstructionRefuses()
    {
        AssertArgument(
            "behavior",
            "matching derivatives is only supported with SplitRanges",
            () => new MixedLinearCubic(1, Share, Spline, false, First, null).CheckPoints(5));
        AssertArgument("x", "not enough points to interpolate: at least 1 required, 0 provided", () => new MixedLinearCubic(0, Split, Spline).CheckPoints(0));
        AssertArgument("n", "n is too large (5 > 4)", () => new MixedLinearCubic(5, Share, Spline).CheckPoints(5));
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 2 required, 1 provided",
            () => new MixedLinearCubic(4, Split, Spline).CheckPoints(5));
        AssertArgument(
            "x",
            "Lagrange boundary condition requires at least 4 points (3 are given)",
            () => new MixedLinearCubic(2, Split, Spline, false, CubicInterpolationBoundaryCondition.Lagrange).CheckPoints(5));
        AssertArgument(
            "x",
            "not-a-knot boundary condition requires at least 3 points (2 are given)",
            () => new MixedLinearCubic(3, Split, Spline, false, Second, 0.0, CubicInterpolationBoundaryCondition.NotAKnot)
                .CheckPoints(5));
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 3 required, 2 provided",
            () => new LogMixedLinearCubic(0, Share, Spline).CheckPoints(2));
        AssertArgument("n", "n is too large (3 > 2)", () => new MonotonicLogMixedLinearCubic(3).CheckPoints(3));
        AssertArgument(
            "x",
            "not enough points to interpolate: at least 2 required, 1 provided",
            () => new KrugerLogMixedLinearCubic(3, Split).CheckPoints(4));

        // what the construction accepts: the update's refusals (a Periodic end) are left to the build
        new MixedLinearCubic(1, Share, Spline, false, CubicInterpolationBoundaryCondition.Periodic).CheckPoints(5);
        new MixedLinearCubic(1, Split, Spline, false, First, null).CheckPoints(3);
        new LogMixedLinearCubic(4, Split, Spline).CheckPoints(6);
    }

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
