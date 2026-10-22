// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Business/252's month counts. QuantLib caches them in two file-level maps keyed by the calendar's name, filled once
/// and never invalidated (<c>business252.cpp:31-68</c>, <c>:87-105</c>): after a holiday edit every
/// <c>Business252</c> on the calendar's name, a new one included, counts the days the first count saw, and two
/// calendars of one name share their counts. nanoql counts each month once per day counter, on its own calendar, so a
/// count is always its calendar's <c>businessDaysBetween</c>, QuantLib's own check.
/// </summary>
public class Business252CacheTests
{
    [Fact]
    public void ACountAfterAHolidayEditIsTheEditedCalendars()
    {
        Date d1 = new(10, Month.January, 2024), d2 = new(10, Month.January, 2026);
        var brazil = new Brazil(BrazilMarket.Settlement);
        Assert.Equal(505, new Business252(brazil).DayCount(d1, d2));

        // a business day made a holiday
        Calendar edited = brazil.AddHoliday(new Date(13, Month.March, 2024));

        // QuantLib: 505 after the edit, from every Business252 on Brazil, a new one included, where its calendar
        // counts 504; its year fraction 2.003968253968254
        Assert.Equal(504, edited.BusinessDaysBetween(d1, d2));
        Assert.Equal(504, new Business252(edited).DayCount(d1, d2));
        Assert.Equal(504 / 252.0, new Business252(edited).YearFraction(d1, d2));

        // the unedited calendar keeps its count
        Assert.Equal(505, new Business252(brazil).DayCount(d1, d2));
    }

    [Fact]
    public void TwoCalendarsOfOneNameCountTheirOwnDays()
    {
        Date d1 = new(1, Month.January, 2024), d2 = new(1, Month.January, 2025);
        BespokeCalendar weekends = new BespokeCalendar("X").AddWeekend(Weekday.Saturday).AddWeekend(Weekday.Sunday);
        var noWeekend = new BespokeCalendar("X");

        Assert.Equal(262, new Business252(weekends).DayCount(d1, d2));

        // QuantLib: 270, the first calendar's cached months, where the second's own count is 366; and the two day
        // counters are equal by name
        Assert.Equal(366, new Business252(noWeekend).DayCount(d1, d2));
        Assert.Equal(noWeekend.BusinessDaysBetween(d1, d2), new Business252(noWeekend).DayCount(d1, d2));
        Assert.False(new Business252(weekends) == new Business252(noWeekend));

        // a span within the year: QuantLib's 214, where the second calendar counts 284
        Date m1 = new(10, Month.February, 2024), m2 = new(20, Month.November, 2024);
        Assert.Equal(284, new Business252(noWeekend).DayCount(m1, m2));
    }
}
