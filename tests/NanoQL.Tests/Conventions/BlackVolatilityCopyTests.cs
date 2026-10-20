// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="BlackVarianceCurve.SetInterpolation{TInterpolator}"/> and
/// <see cref="BlackVarianceSurface.SetInterpolation{TInterpolator}"/> return a copy re-interpolated with the other
/// interpolator, and the structure they are called on is unchanged; QuantLib re-interpolates the structure itself and
/// notifies its observers (<c>blackvariancecurve.hpp:70-74</c>, <c>blackvariancesurface.hpp:72-78</c>).
/// </summary>
public class BlackVolatilityCopyTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void TheCurvesInterpolationIsSetOnACopy()
    {
        var linear = new BlackVarianceCurve(Today, [Today + 91, Today + 365, Today + 730], [0.25, 0.22, 0.21], new Actual365Fixed());
        double before = linear.BlackVol(0.5, 100.0);

        BlackVarianceCurve cubic = linear.SetInterpolation<Cubic>();

        Assert.NotSame(linear, cubic);
        Assert.Equal(before, linear.BlackVol(0.5, 100.0));
        Assert.NotEqual(before, cubic.BlackVol(0.5, 100.0));
        Assert.Equal(linear.ReferenceDate, cubic.ReferenceDate);
        Assert.Equal(linear.MaxDate, cubic.MaxDate);
    }

    [Fact]
    public void TheSurfacesInterpolationIsSetOnACopy()
    {
        var vols = new Matrix(3, 3);
        double[,] values = { { 0.30, 0.28, 0.27 }, { 0.22, 0.21, 0.205 }, { 0.25, 0.235, 0.23 } };
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                vols[i, j] = values[i, j];
            }
        }

        var bilinear = new BlackVarianceSurface(
            Today, new TARGET(), [Today + 91, Today + 365, Today + 730], [80.0, 100.0, 120.0], vols, new Actual365Fixed());
        double before = bilinear.BlackVol(0.5, 95.0);

        BlackVarianceSurface bicubic = bilinear.SetInterpolation<Bicubic>();

        Assert.NotSame(bilinear, bicubic);
        Assert.Equal(before, bilinear.BlackVol(0.5, 95.0));
        Assert.NotEqual(before, bicubic.BlackVol(0.5, 95.0));
        Assert.Equal(bilinear.MinStrike(), bicubic.MinStrike());
    }
}
