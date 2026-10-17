// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An interpolation is complete when it is built, and owns copies of its inputs (playbook §2.5). QuantLib's
/// interpolations keep iterators into their caller's data and compute their coefficients in <c>update()</c>, which a
/// caller runs again when the data change; nanoql ports no <c>update()</c>, so a change of data is a new interpolation.
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

        AssertSame(new Linear().Interpolate(X, Y), new LinearInterpolation(X, Y));
        AssertSame(new BackwardFlat().Interpolate(X, Y), new BackwardFlatInterpolation(X, Y));
        AssertSame(new LogLinear().Interpolate(X, LogY), new LogLinearInterpolation(X, LogY));
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
    }

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
