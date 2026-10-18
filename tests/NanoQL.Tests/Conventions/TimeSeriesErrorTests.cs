// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of <see cref="TimeSeries{T}"/>'s refusals, which a vector's <c>{"throws": true}</c> can't tell
/// apart: the first and last dates of an empty series throw <see cref="NanoQLException"/> with QuantLib's message; a
/// constructor given fewer values than dates, a path QuantLib leaves undefined, throws
/// <see cref="ArgumentException"/> on <c>values</c>; and a run of values from a first date that QuantLib's date
/// increment refuses throws <see cref="ArgumentOutOfRangeException"/> on <c>values</c>, with the increment's message.
/// </summary>
public class TimeSeriesErrorTests
{
    [Fact]
    public void TimeSeriesRefusals()
    {
        var empty = new TimeSeries<double>([], []);
        Assert.Equal("empty timeseries", Assert.Throws<NanoQLException>(() => empty.FirstDate()).Message);
        Assert.Equal("empty timeseries", Assert.Throws<NanoQLException>(() => empty.LastDate()).Message);
        var noValues = new TimeSeries<double>(new Date(15, Month.March, 2005), []);
        Assert.Equal("empty timeseries", Assert.Throws<NanoQLException>(() => noValues.LastDate()).Message);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new TimeSeries<double>([new Date(15, Month.March, 2005), new Date(16, Month.March, 2005)], [1.0]));
        Assert.Equal("values", e.ParamName);
        Assert.StartsWith("fewer values than dates", e.Message);

        // QuantLib moves the date past the last value too, so a run ending on the last valid date throws, with the
        // date's range message (cxx/timeseries).
        ArgumentOutOfRangeException range = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TimeSeries<double>(Date.MaxDate - 1, [1.0, 2.0]));
        Assert.Equal("values", range.ParamName);
        Assert.StartsWith("Date's serial number (109575) outside allowed range [367-109574]", range.Message);
        Assert.Equal(Date.MaxDate - 1, new TimeSeries<double>(Date.MaxDate - 2, [1.0, 2.0]).LastDate());
    }
}
