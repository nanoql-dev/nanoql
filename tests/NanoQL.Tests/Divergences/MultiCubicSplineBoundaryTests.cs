// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The multi-cubic spline at and beyond a dimension's last grid point, and at a coordinate that isn't a number.
/// QuantLib treats the last point as extrapolation (<c>multicubicspline.hpp:550</c>), so it refuses it, its documented
/// bug ("cannot interpolate at the grid points on the boundary surface", <c>:463-464</c>); with extrapolation allowed,
/// its flat value there and beyond takes the last interval's index past it and reads one value past the data
/// (<c>:553-554</c> with <c>:295</c>), undefined; and a NaN coordinate fails both of its range comparisons, so its
/// search returns the grid's end and reads past it (<c>:557-558</c>). nanoql interpolates the last point on its last
/// interval, its value the node's, reads the last value beyond it with no read past the data, and refuses a NaN
/// coordinate: the node's value and QuantLib's just inside are the checks.
/// </summary>
public class MultiCubicSplineBoundaryTests
{
    private static readonly ImmutableArray<ImmutableArray<double>> Grid = [[0.0, 0.5, 1.2, 1.5, 2.3, 3.0], [0.0, 1.0, 2.0, 3.0]];

    [Fact]
    public void TheLastGridPointIsInterpolatedOnItsLastInterval()
    {
        var spline = new MultiCubicSpline(Grid, Values());
        Assert.Equal(F(3.0, 3.0), spline.Evaluate([3.0, 3.0]));
        Assert.Equal(F(1.2, 3.0), spline.Evaluate([1.2, 3.0]));

        // QuantLib's value just inside the last point, which it interpolates: within the spline's error of the node's
        Assert.Equal(F(3.0, 3.0), spline.Evaluate([3.0 - 1e-9, 3.0 - 1e-9]), 1e-8);
    }

    [Fact]
    public void BeyondTheGridTheLastValueIsReadWithNoReadPastTheData()
    {
        var spline = new MultiCubicSpline(Grid, Values(), [true, true]);
        Assert.Equal(F(3.0, 1.0), spline.Evaluate([4.0, 1.0]));
        Assert.Equal(F(3.0, 3.0), spline.Evaluate([4.0, 5.0]));
    }

    [Fact]
    public void ACoordinateThatIsntANumberIsRefused()
    {
        var spline = new MultiCubicSpline(Grid, Values(), [true, true]);
        ArgumentException e = Assert.Throws<ArgumentException>(() => spline.Evaluate([double.NaN, 1.0]));
        Assert.Equal("x", e.ParamName);
    }

    private static double F(double x, double y) => Math.Sin(x) + (0.3 * y * y);

    private static double[] Values() => [.. Grid[0].SelectMany(x => Grid[1].Select(y => F(x, y)))];
}
