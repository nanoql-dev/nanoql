// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="TimeSeries{T}"/> is a value: two series are equal when they hold the same dates with equal values,
/// however they were built, and equal series hash equal. QuantLib's <c>TimeSeries</c> has no <c>operator==</c>.
/// </summary>
public class TimeSeriesEqualityTests
{
    [Fact]
    public void SeriesWithTheSameDataAreEqualAndOthersAreNot()
    {
        Date d1 = new(15, Month.March, 2005), d2 = new(16, Month.March, 2005), d3 = new(17, Month.March, 2005);
        var a = new TimeSeries<double>([d1, d2, d3], [1.0, 2.0, 3.0]);

        // Another order, a date given twice with its last value kept, surplus values, and consecutive dates.
        TimeSeries<double>[] same =
        [
            new([d3, d1, d2], [3.0, 1.0, 2.0]),
            new([d2, d3, d1, d2], [9.0, 3.0, 1.0, 2.0]),
            new([d1, d2, d3], [1.0, 2.0, 3.0, 4.0]),
            new(d1, [1.0, 2.0, 3.0]),
        ];
        foreach (TimeSeries<double> b in same)
        {
            Assert.True(a.Equals(b));
            Assert.True(a.Equals((object)b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        TimeSeries<double>[] other =
        [
            new([d1, d2, d3], [1.0, 2.0, 3.5]),
            new([d1, d2, d2 + 2], [1.0, 2.0, 3.0]),
            new([d1, d2], [1.0, 2.0]),
            new([], []),
        ];
        foreach (TimeSeries<double> b in other)
        {
            Assert.False(a.Equals(b));
            Assert.False(a.Equals((object)b));
        }

        Assert.False(a.Equals(null));
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals(new TimeSeries<int>([d1, d2, d3], [1, 2, 3])));
        Assert.Equal(new TimeSeries<double>([], []), new TimeSeries<double>(d1, []));
    }
}
