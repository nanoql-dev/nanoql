// SPDX-FileCopyrightText: Copyright (C) 2026 Abhay Chaudhary
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/extendedblackvariancesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ExtendedBlackVarianceSurfaceTests
{
    [Fact]
    public void TestVariancesAtNodes()
    {
        // Testing extended Black variance surface at its nodes...
        var today = new Date(15, Month.September, 2026);
        DayCounter dc = new Actual365Fixed();

        // more than two strikes, so that a wrong stride into the volatility
        // vector reads past its end
        Date[] dates = [today + new Period(1, TimeUnit.Years), today + new Period(2, TimeUnit.Years)];
        double[] strikes = [90.0, 100.0, 110.0];

        var volatilities = new List<double>();
        for (int i = 0; i < strikes.Length; ++i)
        {
            for (int j = 0; j < dates.Length; ++j)
            {
                volatilities.Add(0.10 + (0.10 * i) + (0.01 * j));
            }
        }

        var surface = new ExtendedBlackVarianceSurface(today, new NullCalendar(), dates, strikes, volatilities, dc);

        for (int i = 0; i < strikes.Length; ++i)
        {
            for (int j = 0; j < dates.Length; ++j)
            {
                double t = dc.YearFraction(today, dates[j]);
                double sigma = 0.10 + (0.10 * i) + (0.01 * j);
                double expected = t * sigma * sigma;
                double calculated = surface.BlackVariance(dates[j], strikes[i]);
                Assert.False(
                    Math.Abs(calculated - expected) > 1.0e-10,
                    $"failed to reproduce variance at a node: strike {strikes[i]}, time {t}, calculated {calculated}, "
                    + $"expected {expected}");
            }
        }
    }
}
