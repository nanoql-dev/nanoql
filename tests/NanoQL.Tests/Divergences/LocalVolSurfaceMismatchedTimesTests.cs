// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="LocalVolSurface"/> on curves that measure time differently from its Black surface. QuantLib reads the
/// risk-free and dividend curves at the surface's times (<c>localvolsurface.cpp:82-118</c>), each measuring time from
/// its own reference date in its own day counter. nanoql reads each curve at the instant the surface's time denotes, on
/// its own clock, so a surface on Act/360 over curves on Act/365 gives the local volatility of the same surface
/// restated on Act/365, the same variances at every date: QuantLib's own surface on that market is the check, a local
/// volatility being a rate per the surface's own year, so the two differ by √(365/360). Dupire's time derivative steps
/// 1e-4 on each surface's own clock, so they agree to 1e-6; curves on the surface's day counter and reference date are
/// QuantLib's.
/// </summary>
public class LocalVolSurfaceMismatchedTimesTests
{
    [Fact]
    public void CurvesOnAnotherDayCounterAreReadAtTheSurfacesInstants()
    {
        var today = new Date(15, Month.October, 2026);
        DayCounter a365 = new Actual365Fixed();
        var vols = new Matrix(5, 3);
        double[,] values = { { 0.35, 0.33, 0.31 }, { 0.29, 0.28, 0.27 }, { 0.24, 0.235, 0.23 }, { 0.21, 0.205, 0.20 }, { 0.19, 0.185, 0.18 } };
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                vols[i, j] = values[i, j];
            }
        }

        var surface = new BlackVarianceSurface(
            today, new TARGET(), [today + 182, today + 365, today + 1095], [60.0, 80.0, 100.0, 120.0, 140.0], vols, new Actual360());
        var local = new LocalVolSurface(surface, new FlatForward(today, 0.04, a365), new FlatForward(today, 0.02, a365), 100.0);

        // QuantLib's local volatility of the surface restated on Act/365 (its volatilities times sqrt(365/360)); on the
        // mixed surface QuantLib reads, at 91 days and 70, 0.42937200550021143
        (int Days, double Strike, double Restated)[] points =
        [
            (91, 70.0, 0.43233632204285144), (91, 90.0, 0.29931524564678336), (91, 110.0, 0.21377099535133906),
            (300, 70.0, 0.3951330778683177), (300, 90.0, 0.2901484511444443), (300, 110.0, 0.2072344532905911),
            (700, 70.0, 0.3909371003557756), (700, 90.0, 0.30154105637041284), (700, 110.0, 0.21162191878947895),
        ];
        foreach ((int days, double strike, double restated) in points)
        {
            double own = local.LocalVol(days / 360.0, strike, true) * Math.Sqrt(365.0 / 360.0);
            Assert.True(Math.Abs((own / restated) - 1.0) < 1e-6, $"{days} days at {strike}: {own} against {restated}");
        }
    }
}
