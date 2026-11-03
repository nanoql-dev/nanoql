// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The series of the GARCH facts: QuantLib's own calibration case, 50,000 daily returns from 7 July 1962 simulated by
/// GARCH(1,1) at α 0.2, β 0.3 and V_L 0.4 on QuantLib's Mersenne twister at seed 48 and inverse normal, as
/// <c>garch.cpp</c>'s <c>testCalibration</c> builds it.
/// </summary>
internal static class GarchSeries
{
    internal static readonly TimeSeries<double> Returns = Simulate();

    internal static double[] Values => [.. Returns.Values()];

    private static TimeSeries<double> Simulate()
    {
        var garch = new Garch11(0.2, 0.3, 0.4);
        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(new MersenneTwisterUniformRng(48));
        double r = 0.0, v = 0.0;
        Date d = new(7, July, 1962);
        var dates = new Date[50000];
        var values = new double[50000];
        for (int i = 0; i < 50000; ++i, d += 1)
        {
            v = garch.Forecast(r, v);
            r = rng.Next().Value * Math.Sqrt(v);
            dates[i] = d;
            values[i] = r;
        }

        return new TimeSeries<double>(dates, values);
    }
}
