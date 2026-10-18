// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's <c>TimeSeries::find</c> inserts a date the series doesn't hold, with a <c>Null&lt;T&gt;()</c> datum,
/// and returns an iterator to it (<c>timeseries.hpp:233-242</c>), as its non-const <c>operator[]</c> does
/// (<c>timeseries.hpp:102-105</c>): <c>testUtilities</c> checks that the size grows from 3 to 4
/// (<c>test-suite/timeseries.cpp:173-174</c>). nanoql's series is immutable and ports neither:
/// <see cref="TimeSeries{T}.TryGetValue"/> reads a missing date as absent and leaves the series unchanged.
/// </summary>
public class TimeSeriesFindTests
{
    [Fact]
    public void LookingUpAMissingDateLeavesTheSeriesUnchanged()
    {
        Date[] dates = [new(25, Month.March, 2005), new(29, Month.March, 2005), new(15, Month.March, 2005)];
        double[] prices = [25, 23, 20];
        var ts = new TimeSeries<double>(dates, prices);

        Assert.False(ts.TryGetValue(new Date(1, Month.March, 2005), out double value));
        Assert.Equal(0.0, value);
        Assert.Equal(3, ts.Size);
        Assert.Equal([dates[2], dates[0], dates[1]], ts.Dates());
        Assert.Equal(new TimeSeries<double>(dates, prices), ts);
    }
}
