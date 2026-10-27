// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2010 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/timeseries.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TimeSeriesTests
{
    // Adapted: nanoql's series is immutable. The empty series filled through the inserting operator[] (lines 39-42)
    // is built from the three dates and values; each later assignment to 15 March (lines 52 and 58) builds a series
    // whose date list gives 15 March again with the new value, which the series keeps, as operator[]'s assignment
    // keeps the last; and the check after line 58 reads the new series' first datum, where QuantLib's reads it through
    // the iterator taken before the assignment (line 53).
    [Fact]
    public void TestConstruction()
    {
        // Testing time series construction...

        Date[] dates = [new(25, March, 2005), new(29, March, 2005), new(15, March, 2005)];
        Real[] values = [1.2, 2.3, 0.3];
        var ts = new TimeSeries<Real>(dates, values);

        KeyValuePair<Date, Real> cur = ts.First();
        if (cur.Key != new Date(15, March, 2005))
        {
            Assert.Fail("date does not match");
        }

        if (cur.Value != 0.3)
        {
            Assert.Fail("value does not match");
        }

        ts = new TimeSeries<Real>([.. dates, new(15, March, 2005)], [.. values, 4.0]);
        cur = ts.First();
        if (cur.Value != 4.0)
        {
            Assert.Fail($"replaced value does not match{cur.Value}\n");
        }

        ts = new TimeSeries<Real>([.. dates, new(15, March, 2005), new(15, March, 2005)], [.. values, 4.0, 3.5]);
        cur = ts.First();
        if (cur.Value != 3.5)
        {
            Assert.Fail($"set value operator not match{cur.Value}\n");
        }
    }

    [Fact]
    public void TestIntervalPrice()
    {
        // Testing time series interval price...
        Date[] date = [new(25, March, 2005), new(29, March, 2005)];
        Real[] open = [1.3, 2.3], close = [2.3, 3.4], high = [3.4, 3.5], low = [3.4, 3.2];

        TimeSeries<IntervalPrice> tsiq = IntervalPrice.MakeSeries(date, open, close, high, low);
        Assert.Equal(2, tsiq.Size);
    }

    [Fact]
    public void TestIteratingDefaultContainer()
    {
        // Testing iteration of time series with a default container which sorts by date...

        Date[] dates = [new(25, March, 2005), new(29, March, 2005), new(15, March, 2005)];
        Real[] prices = [25, 23, 20];
        var ts = new TimeSeries<Real>(dates, prices);

        // accessing dates
        {
            List<Date> tsDates = ts.Select(x => x.Key).ToList();
            Date[] expected = [dates[2], dates[0], dates[1]];
            Assert.Equal(expected, tsDates);
        }

        // accessing Values
        {
            List<Real> tsValues = ts.Select(x => x.Value).ToList();
            Real[] expected = [prices[2], prices[0], prices[1]];
            Assert.Equal(expected, tsValues);
        }
    }

    // Adapted: QuantLib's container parameter isn't ported, so the series of int over Boost's unordered map (lines
    // 116-118) is a TimeSeries<int>, which keeps its data in date order. Its filling through the inserting operator[]
    // (lines 121-123) becomes a list of the dates and one of the values, built by the same loop, and its reads through
    // operator[] (line 127) read with TryGetValue.
    [Fact]
    public void TestCustomContainer()
    {
        // Testing usage of a custom container for time series data...

        Date d0 = new(25, March, 2005), d1 = new(25, April, 2005), d = d0;
        var calendar = new UnitedStates(UnitedStatesMarket.NYSE);
        var dates = new List<Date>();
        var values = new List<int>();
        for (int i = 0; d < d1; ++i, d = calendar.Advance(d, 1, TimeUnit.Days))
        {
            dates.Add(d);
            values.Add(i);
        }

        var ts = new TimeSeries<int>(dates, values);

        d = d0;
        for (int i = 0; d < d1; ++i, d = calendar.Advance(d, 1, TimeUnit.Days))
        {
            Assert.True(ts.TryGetValue(d, out int value) && value == i);
        }
    }

    [Fact]
    public void TestInspectors()
    {
        // Testing inspectors of time series...

        Date[] dates = [new(25, March, 2005), new(29, March, 2005), new(15, March, 2005)];
        Real[] prices = [25, 23, 20];
        var ts = new TimeSeries<Real>(dates, prices);

        Assert.True(ts.FirstDate() == new Date(15, March, 2005));
        Assert.True(ts.LastDate() == new Date(29, March, 2005));
        Assert.True(ts.Size == 3);
        Assert.True(!ts.Empty);
    }

    // Adapted: find, which returns an iterator and inserts a date the series doesn't hold, isn't ported. Its lookups of
    // the three stored dates (lines 161-171) read each date's datum with TryGetValue, and the size checks after them
    // are kept. Its insertion of a missing date (lines 173-174) becomes Divergences/TimeSeriesFindTests.cs, which
    // checks that TryGetValue leaves the series unchanged.
    [Fact]
    public void TestUtilities()
    {
        // Testing time series utilities...

        Date[] dates = [new(25, March, 2005), new(29, March, 2005), new(15, March, 2005)];
        Real[] prices = [25, 23, 20];

        // find: reads without inserting
        {
            var series = new TimeSeries<Real>(dates, prices);

            Assert.True(series.TryGetValue(new Date(15, March, 2005), out Real found) && found == 20);
            Assert.True(series.Size == 3);

            Assert.True(series.TryGetValue(new Date(25, March, 2005), out found) && found == 25);
            Assert.True(series.Size == 3);

            Assert.True(series.TryGetValue(new Date(29, March, 2005), out found) && found == 23);
            Assert.True(series.Size == 3);
        }

        var ts = new TimeSeries<Real>(dates, prices);

        // dates()
        {
            Date[] expected = [dates[2], dates[0], dates[1]];
            Assert.Equal(expected, ts.Dates());
        }

        // values()
        {
            Real[] expected = [prices[2], prices[0], prices[1]];
            Assert.Equal(expected, ts.Values());
        }
    }
}
