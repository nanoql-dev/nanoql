// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The local volatility surfaces' modifiers return copies:
/// <see cref="FixedLocalVolSurface.WithInterpolation{TInterpolator}"/>, QuantLib's <c>setInterpolation</c>, and
/// <see cref="GridModelLocalVolSurface.WithParams"/>, QuantLib's <c>setParams</c> with its <c>generateArguments</c>,
/// leave the receiver as it was, and the copy is the surface built afresh on the new interpolation or parameters.
/// </summary>
public class LocalVolCopiesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void WithInterpolationReturnsACopy()
    {
        double[] times = [0.25, 0.5, 1.0];
        double[] strikes = [70.0, 90.0, 100.0, 110.0, 130.0];
        Matrix vols = Vols(strikes.Length, times.Length);
        var linear = new FixedLocalVolSurface(Today, times, strikes, vols, Dc);
        double before = linear.LocalVol(0.75, 95.0);

        FixedLocalVolSurface cubic = linear.WithInterpolation<Cubic>();

        Assert.Equal(before, linear.LocalVol(0.75, 95.0));
        Assert.NotEqual(before, cubic.LocalVol(0.75, 95.0));
        FixedLocalVolSurface again = new FixedLocalVolSurface(Today, times, strikes, vols, Dc).WithInterpolation<Cubic>();
        foreach (double t in new[] { 0.25, 0.4, 0.75, 1.0 })
        {
            foreach (double k in new[] { 75.0, 95.0, 100.0, 125.0 })
            {
                Assert.Equal(again.LocalVol(t, k), cubic.LocalVol(t, k));
            }
        }
    }

    [Fact]
    public void WithParamsReturnsACopy()
    {
        Date[] dates = [Today + 91, Today + 365];
        double[][] strikes = [[80.0, 100.0, 120.0], [70.0, 100.0, 130.0]];
        var grid = new GridModelLocalVolSurface(Today, dates, strikes, Dc);
        Vector before = grid.Params();

        var parameters = new Vector([0.30, 0.31, 0.25, 0.26, 0.28, 0.29]);
        var copy = (GridModelLocalVolSurface)grid.WithParams(parameters);

        Assert.Equal(before.AsSpan().ToArray(), grid.Params().AsSpan().ToArray());
        Assert.Equal(1.0, grid.LocalVol(0.5, 100.0));
        Assert.Equal(parameters.AsSpan().ToArray(), copy.Params().AsSpan().ToArray());

        // the copy is the fixed surface on its parameters, row by row of strikes
        var vols = new Matrix(3, 2);
        for (int k = 0; k < 6; ++k)
        {
            vols[k / 2, k % 2] = parameters[k];
        }

        var expected = new FixedLocalVolSurface(
            Today, dates.Select(d => Dc.YearFraction(Today, d)), strikes.Select(s => (IEnumerable<double>)s), vols, Dc);
        foreach (double t in new[] { 0.1, 0.5, 1.0 })
        {
            foreach (double k in new[] { 75.0, 100.0, 125.0 })
            {
                Assert.Equal(expected.LocalVol(t, k, true), copy.LocalVol(t, k, true));
            }
        }
    }

    private static Matrix Vols(int rows, int columns)
    {
        var m = new Matrix(rows, columns);
        for (int i = 0; i < rows; ++i)
        {
            for (int j = 0; j < columns; ++j)
            {
                m[i, j] = 0.2 + (0.04 * Math.Abs(i - (rows / 2))) + (0.01 * j) + (0.003 * i * i);
            }
        }

        return m;
    }
}
