// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An interpolation is complete when it is built, and owns copies of its inputs (playbook §2.5). QuantLib's
/// interpolations keep iterators into their caller's data, and its 2-D ones a reference to the caller's matrix, and
/// compute their coefficients in <c>update()</c>, which a caller runs again when the data change; nanoql ports no
/// <c>update()</c>, so a change of data is a new interpolation.
/// </summary>
public class InterpolationConstructionTests
{
    // QuantLib's testInterpolateWithoutUpdate data: xRange(-2.0, 2.0, 4) and parabolic(x), and the exponential of the
    // parabola for the log interpolations, which take positive values.
    private static readonly double[] X = [-2.0, -2.0 + (4.0 / 3), -2.0 + (2 * (4.0 / 3)), 2.0];
    private static readonly double[] Y = [.. X.Select(x => -x * x)];
    private static readonly double[] LogY = [.. Y.Select(Math.Exp)];
    private static readonly double[] Points = [-3.0, -2.0, -1.5, -0.5, 0.0, 0.25, 1.0, 1.9, 2.0, 2.5];

    // Replaces QuantLib's testInterpolateWithoutUpdate, which checks that its factories' detail::interpolateWithoutUpdate
    // builds an interpolation whose update() hasn't run yet, whether or not the factory's interpolate takes an update
    // flag. nanoql has no update step: Interpolation declares no Update, and a factory's Interpolate returns an
    // interpolation already computed, equal, bit for bit, to the class built directly.
    [Fact]
    public void AnInterpolationIsCompleteAtConstruction()
    {
        Assert.Null(typeof(Interpolation).GetMethod("Update"));
        Assert.Null(typeof(CubicInterpolation).GetMethod("Update"));
        Assert.Null(typeof(Interpolation2D).GetMethod("Update"));

        AssertSame(new Linear().Interpolate(X, Y), new LinearInterpolation(X, Y));
        AssertSame(new BackwardFlat().Interpolate(X, Y), new BackwardFlatInterpolation(X, Y));
        AssertSame(new LogLinear().Interpolate(X, LogY), new LogLinearInterpolation(X, LogY));
        AssertSame(new ForwardFlat().Interpolate(X, Y), new ForwardFlatInterpolation(X, Y));
        AssertSame(new Cubic().Interpolate(X, Y), new KrugerCubic(X, Y));
        AssertSame(
            new Cubic(CubicInterpolationDerivativeApprox.Spline, true, CubicInterpolationBoundaryCondition.NotAKnot, 0.0, CubicInterpolationBoundaryCondition.FirstDerivative, 1.5)
                .Interpolate(X, Y),
            new CubicInterpolation(
                X, Y, CubicInterpolationDerivativeApprox.Spline, true, CubicInterpolationBoundaryCondition.NotAKnot, 0.0, CubicInterpolationBoundaryCondition.FirstDerivative, 1.5));
        AssertSame(new LogCubic(CubicInterpolationDerivativeApprox.Parabolic).Interpolate(X, LogY), new MonotonicLogParabolic(X, LogY));
        AssertSame(new MonotonicLogCubic().Interpolate(X, LogY), new MonotonicLogCubicNaturalSpline(X, LogY));
        AssertSame(new KrugerLog().Interpolate(X, LogY), new KrugerLogCubic(X, LogY));
    }

    [Fact]
    public void AnInterpolationCopiesItsInputs()
    {
        double[] x = [.. X], y = [.. Y], logY = [.. LogY];
        var vx = Vector.Create(X);
        var vy = Vector.Create(Y);
        Interpolation[] built =
        [
            new LinearInterpolation(x, y),
            new BackwardFlatInterpolation(x, y),
            new CubicNaturalSpline(x, y),
            new LogCubicNaturalSpline(x, logY),
            new LogLinearInterpolation(x, logY),
            new Cubic().Interpolate(vx.AsSpan(), vy.AsSpan()),
        ];
        double[][] before = [.. built.Select(Values)];

        // Changing the sources afterwards changes nothing.
        x[1] = 0.5;
        y[2] = 10.0;
        logY[0] = 3.0;
        vx[2] = 1.75;
        vy[0] = -7.0;
        for (int i = 0; i < built.Length; i++)
        {
            Assert.Equal(before[i], Values(built[i]));
            Assert.Equal(X, built[i].XValues());
        }

        Assert.Equal(Y, built[0].YValues());
        Assert.Equal(Y, built[5].YValues());

        // A 2-D interpolation copies its grid and its matrix, and gives a copy of the matrix.
        double[] gx = [0.0, 1.0, 3.0], gy = [10.0, 20.0];
        var z = new Matrix(new[,] { { 1.0, 2.0, 4.0 }, { 3.0, 5.0, 9.0 } });
        Interpolation2D[] grids = [new BilinearInterpolation(gx, gy, z), new Bilinear().Interpolate(gx, gy, z)];
        grids = [.. grids, new FlatExtrapolator2D(grids[0])];
        double[][] gridBefore = [.. grids.Select(Values2D)];
        gx[1] = 2.5;
        gy[0] = 0.0;
        z[1, 1] = -100.0;
        grids[0].ZData()[0, 0] = 50.0;
        for (int i = 0; i < grids.Length; i++)
        {
            Assert.Equal(gridBefore[i], Values2D(grids[i]));
            Assert.Equal([0.0, 1.0, 3.0], grids[i].XValues());
            Assert.Equal([10.0, 20.0], grids[i].YValues());
            Assert.Equal(new Matrix(new[,] { { 1.0, 2.0, 4.0 }, { 3.0, 5.0, 9.0 } }), grids[i].ZData());
        }
    }

    // Replaces QuantLib's testBicubicUpdate, which changes the caller's matrix after building a bicubic spline on it and
    // checks that update() moves the value at (x[2] + 0.1, y[4]) by at least 0.5 (interpolations.cpp:1858-1884). A
    // nanoql spline copies its matrix: changing the caller's matrix changes nothing, and a spline built on the changed
    // matrix, the rebuild that takes update()'s place, moves the value as QuantLib's update() does.
    [Fact]
    public void ABicubicSplineCopiesItsInputs()
    {
        const int n = 6;
        var x = new double[n];
        var y = new double[n];
        for (int i = 0; i < n; ++i)
        {
            x[i] = y[i] = i * 0.2;
        }

        var f = new Matrix(n, n);
        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                f[i, j] = x[j] * (x[j] + y[i]);
            }
        }

        Interpolation2D[] built = [new BicubicSpline(x, y, f), new Bicubic().Interpolate(x, y, f)];
        Matrix original = f.Clone();
        double px = x[2] + 0.1, py = y[4];
        double oldResult = built[0].Evaluate(px, py);
        double[][] before = [.. built.Select(Values2D)];

        // modify input matrix: the splines built on it don't change.
        f[4, 3] += 1.0;
        x[1] = 0.25;
        built[0].ZData()[4, 3] = -1.0;
        for (int i = 0; i < built.Length; i++)
        {
            Assert.Equal(before[i], Values2D(built[i]));
            Assert.Equal(original, built[i].ZData());
            Assert.Equal(oldResult, built[i].Evaluate(px, py));
        }

        // A spline built on the changed matrix moves, as QuantLib's update() does.
        x[1] = 0.2;
        double newResult = new BicubicSpline(x, y, f).Evaluate(px, py);
        Assert.True(Math.Abs(oldResult - newResult) >= 0.5, $"{oldResult} -> {newResult}");
    }

    private static double[] Values2D(Interpolation2D f) =>
        [.. Points.SelectMany(p => new[] { f.Evaluate(p, 12.5, true), f.Evaluate(p * 10.0, 17.0, true) })];

    private static double[] Values(Interpolation f) => [.. Points.Select(p => f.Evaluate(p, true))];

    private static void AssertSame(Interpolation fromFactory, Interpolation direct)
    {
        foreach (double p in Points)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(direct.Evaluate(p, true)), BitConverter.DoubleToInt64Bits(fromFactory.Evaluate(p, true)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(direct.Derivative(p, true)), BitConverter.DoubleToInt64Bits(fromFactory.Derivative(p, true)));
        }
    }
}
