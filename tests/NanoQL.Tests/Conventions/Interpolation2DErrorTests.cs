// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the 2-D interpolations' refusals, which the vectors' <c>{"throws": true}</c> can't tell apart
/// (playbook §9.4): a refused argument throws <see cref="ArgumentException"/> naming it, QuantLib's "not enough x
/// points" and "not enough y points" and nanoql's refusal of a matrix smaller than the grid; a null argument
/// <see cref="ArgumentNullException"/>; and a point outside the range, which the flat extrapolator's decorated
/// interpolation refuses for a NaN coordinate, <see cref="NanoQLException"/>, with QuantLib's text in C#'s number
/// formatting.
/// </summary>
public class Interpolation2DErrorTests
{
    private static readonly double[] X = [1.0, 2.0, 3.0];
    private static readonly double[] Y = [10.0, 20.0];
    private static readonly Matrix Z = new(new[,] { { 110.0, 210.0, 310.0 }, { 120.0, 220.0, 320.0 } });

    [Fact]
    public void TheInterpolation2DRefusals()
    {
        AssertArgument("x", "not enough x points to interpolate: at least 2 required, 1 provided", () => new BilinearInterpolation([1.0], Y, Z));
        AssertArgument("y", "not enough y points to interpolate: at least 2 required, 0 provided", () => new BilinearInterpolation(X, [], Z));
        AssertArgument("x", "not enough x points to interpolate: at least 2 required, 0 provided", () => new Bilinear().Interpolate([], Y, Z));
        AssertArgument("zData", "Z value matrix has wrong number of rows", () => new BilinearInterpolation(X, Y, new Matrix(1, 3)));
        AssertArgument("z", "Z value matrix has wrong number of columns", () => new Bilinear().Interpolate(X, Y, new Matrix(2, 2)));
        Assert.Equal("zData", Assert.Throws<ArgumentNullException>(() => new BilinearInterpolation(X, Y, null!)).ParamName);
        Assert.Equal("z", Assert.Throws<ArgumentNullException>(() => new Bilinear().Interpolate(X, Y, null!)).ParamName);
        Assert.Equal(
            "decoratedInterpolation", Assert.Throws<ArgumentNullException>(() => new FlatExtrapolator2D(null!)).ParamName);

        var f = new BilinearInterpolation(X, Y, Z);
        AssertFailure("interpolation range is [1, 3] x [10, 20]: extrapolation at (4, 10) not allowed", () => f.Evaluate(4.0, 10.0));
        AssertFailure("interpolation range is [1, 3] x [10, 20]: extrapolation at (NaN, 15) not allowed", () => f.Evaluate(double.NaN, 15.0));
        Assert.Equal(410.0, f.Evaluate(4.0, 10.0, true));

        // The flat extrapolator refuses outside the grid as the base does, and when extrapolating reads the decorated
        // interpolation without extrapolation, which refuses a NaN coordinate with its own range text.
        var flat = new FlatExtrapolator2D(f);
        AssertFailure("interpolation range is [1, 3] x [10, 20]: extrapolation at (4, 25) not allowed", () => flat.Evaluate(4.0, 25.0));
        Assert.Equal(320.0, flat.Evaluate(4.0, 25.0, true));
        AssertFailure(
            "interpolation range is [1, 3] x [10, 20]: extrapolation at (NaN, 15) not allowed",
            () => flat.EnableExtrapolation().Evaluate(double.NaN, 15.0));
        Assert.True(double.IsNaN(new FlatExtrapolator2D(f.EnableExtrapolation()).Evaluate(double.NaN, 15.0, true)));
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
