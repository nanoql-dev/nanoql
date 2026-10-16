// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>A <see cref="Date"/> converts to and from <see cref="DateOnly"/>, over the whole range.</summary>
public class DateConversionTests
{
    [Fact]
    public void EveryDateConvertsToTheSameDayAndBack()
    {
        for (int serial = Date.MinDate.SerialNumber; serial <= Date.MaxDate.SerialNumber; ++serial)
        {
            var d = new Date(serial);
            DateOnly o = d.ToDateOnly();

            Assert.Equal(d.Year, o.Year);
            Assert.Equal((int)d.Month, o.Month);
            Assert.Equal(d.DayOfMonth, o.Day);
            Assert.Equal((int)d.Weekday, (int)o.DayOfWeek + 1);
            Assert.Equal(d, Date.FromDateOnly(o));
        }
    }

    [Fact]
    public void TheRangeEndsMatch()
    {
        Assert.Equal(new DateOnly(1901, 1, 1), Date.MinDate.ToDateOnly());
        Assert.Equal(new DateOnly(2199, 12, 31), Date.MaxDate.ToDateOnly());
        Assert.Throws<ArgumentOutOfRangeException>(() => Date.FromDateOnly(new DateOnly(1900, 12, 31)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Date.FromDateOnly(new DateOnly(2200, 1, 1)));
    }
}
