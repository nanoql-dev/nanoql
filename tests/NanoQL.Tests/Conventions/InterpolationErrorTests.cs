// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the interpolations' refusals, which the vectors' <c>{"throws": true}</c> can't tell apart
/// (playbook §9.4): a refused argument throws <see cref="ArgumentException"/> naming it; a condition or scheme QuantLib
/// doesn't implement, a point outside the range, and the log interpolations' primitive throw
/// <see cref="NanoQLException"/>.
/// </summary>
public class InterpolationErrorTests
{
    private static readonly double[] X = [0.0, 1.0, 2.0];
    private static readonly double[] Y = [1.0, 2.0, 1.5];

    [Fact]
    public void RefusalsThrowTheirTypes()
    {
        const CubicInterpolationDerivativeApprox spline = CubicInterpolationDerivativeApprox.Spline;
        const CubicInterpolationBoundaryCondition second = CubicInterpolationBoundaryCondition.SecondDerivative;

        // QuantLib's "not enough points to interpolate", and the y values the x values need.
        AssertArgument("x", "not enough points to interpolate: at least 2 required, 1 provided", () => new LinearInterpolation([1.0], [2.0]));
        AssertArgument("x", "not enough points to interpolate: at least 1 required, 0 provided", () => new BackwardFlatInterpolation([], []));
        AssertArgument("x", "not enough points to interpolate: at least 2 required, 1 provided", () => new Cubic().Interpolate([1.0], [2.0]));
        AssertArgument("y", "2 y values given for 3 x values", () => new LinearInterpolation(X, Y[..2]));

        // Too few points for Akima's scheme or a Lagrange condition, and a y value a log interpolation can't take.
        AssertArgument("x", "Akima approximation requires at least 4 points (3 are given)", () => new AkimaCubicInterpolation(X, Y));
        AssertArgument(
            "x",
            "Lagrange boundary condition requires at least 4 points (3 are given)",
            () => new CubicInterpolation(X, Y, spline, false, CubicInterpolationBoundaryCondition.Lagrange, 0.0, second, 0.0));
        AssertArgument("y", "invalid value (0) at index 1", () => new LogLinearInterpolation(X, [1.0, 0.0, 2.0]));
        AssertArgument("y", "invalid value (-1) at index 2", () => new LogCubic(spline).Interpolate(X, [1.0, 2.0, -1.0]));

        // What QuantLib doesn't implement, and what it refuses at evaluation.
        AssertFailure(
            "this end condition is not implemented yet",
            () => new CubicInterpolation(X, Y, spline, false, CubicInterpolationBoundaryCondition.Periodic, 0.0, second, 0.0));
        AssertFailure(
            "this end condition is not implemented yet",
            () => new CubicInterpolation(X, Y, spline, false, second, 0.0, CubicInterpolationBoundaryCondition.Periodic, 0.0));

        // Two not-a-knot ends on 3 points, whose system QuantLib 1.44 finds singular (TridiagonalOperator's "division
        // by zero"): QuantLib's master's count refuses them at construction (#2888).
        AssertArgument(
            "x",
            "not-a-knot boundary condition requires at least 4 points (3 are given)",
            () => new CubicInterpolation(
                X,
                Y,
                spline,
                false,
                CubicInterpolationBoundaryCondition.NotAKnot,
                0.0,
                CubicInterpolationBoundaryCondition.NotAKnot,
                0.0));
        AssertFailure(
            "FourthOrder not implemented yet",
            () => new CubicInterpolation(X, Y, CubicInterpolationDerivativeApprox.FourthOrder, false, second, 0.0, second, 0.0));
        AssertFailure("interpolation range is [0, 2]: extrapolation at 2.5 not allowed", () => new CubicNaturalSpline(X, Y).Evaluate(2.5));
        AssertFailure("LogInterpolation primitive not implemented", () => new LogCubicNaturalSpline(X, Y).Primitive(1.0));
        AssertFailure("LogInterpolation primitive not implemented", () => new LogLinearInterpolation(X, Y).Primitive(1.0, true));
    }

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertFailure(string message, Action action) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(action).Message);
}
