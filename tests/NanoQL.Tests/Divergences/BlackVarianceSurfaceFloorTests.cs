// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BlackVarianceSurface"/>'s variance past its strikes. QuantLib extrapolates the bilinear variance linearly
/// in strike with no floor under its default extrapolation (<c>blackvariancesurface.cpp:67-84</c>), where its variance
/// curve and its time extrapolation floor theirs at zero (<c>blackvariancecurve.cpp:68</c>,
/// <c>blackvoltimeextrapolation.cpp:44</c>): volatilities of 30% at 90 and 20% at 110 read a variance of −0.010 and a
/// NaN volatility at 130. nanoql floors the variance at zero, QuantLib's convention for a Black variance; inside the
/// grid the variances are QuantLib's.
/// </summary>
public class BlackVarianceSurfaceFloorTests
{
    [Fact]
    public void TheExtrapolatedVarianceIsFlooredAtZero()
    {
        var today = new Date(15, Month.October, 2026);
        var vols = new Matrix(2, 1);
        vols[0, 0] = 0.30;
        vols[1, 0] = 0.20;
        BlackVarianceSurface s = new BlackVarianceSurface(today, null, [today + 365], [90.0, 110.0], vols, new Actual365Fixed())
            .EnableExtrapolation();

        Assert.Equal(0.0, s.BlackVariance(1.0, 130.0));
        Assert.Equal(0.0, s.BlackVol(1.0, 130.0));

        // inside the grid, QuantLib's bilinear variance
        Assert.Equal(((0.30 * 0.30) + (0.20 * 0.20)) / 2.0, s.BlackVariance(1.0, 100.0), 1e-15);
    }
}
