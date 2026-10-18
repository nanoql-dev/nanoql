// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's constructor from two sequences reads one value per date from the values' start, with no bound
/// (<c>timeseries.hpp:64-69</c>: <c>values_[*(dBegin++)] = *(vBegin++)</c>), so fewer values than dates reads past
/// their end, a result C++ leaves undefined. nanoql refuses it with <see cref="ArgumentException"/> on
/// <c>values</c>, "fewer values than dates", and builds nothing. Surplus values stay unread, as in QuantLib.
/// </summary>
public class TimeSeriesFewerValuesTests
{
    [Fact]
    public void FewerValuesThanDatesAreRefused()
    {
        Date[] dates = [new(15, Month.March, 2005), new(25, Month.March, 2005), new(29, Month.March, 2005)];

        ArgumentException e = Assert.Throws<ArgumentException>(() => new TimeSeries<double>(dates, [1.0, 2.0]));
        Assert.Equal("values", e.ParamName);
        Assert.Throws<ArgumentException>(() => new TimeSeries<double>(dates, []));

        var surplus = new TimeSeries<double>(dates, [1.0, 2.0, 3.0, 4.0]);
        Assert.Equal(3, surplus.Size);
        Assert.Equal([1.0, 2.0, 3.0], surplus.Values());
    }
}
