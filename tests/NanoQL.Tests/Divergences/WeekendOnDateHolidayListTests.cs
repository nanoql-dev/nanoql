// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A holiday list without weekends for a market whose weekend changed on a date. The Tel Aviv stock exchange kept
/// Friday and Saturday until January 4th, 2026 (<c>israel.cpp:436-446</c>), while <c>isWeekend(Weekday)</c> answers
/// today's weekend, Saturday and Sunday, so QuantLib's <c>holidayList(..., includeWeekEnds = false)</c>
/// (<c>calendar.cpp:277-286</c>) holds every Friday before 2026; QuantLib's own TASE test says "using checkHolidays
/// with includeWeekEnds = false doesn't work in this case" and filters the list by each date's weekend instead
/// (<c>test-suite/calendars.cpp:3699-3722</c>). nanoql's list leaves out the days that are weekend days on their own
/// date, which equals that test's filtered list; a joint calendar combines its components' weekends on the date as its
/// weekend combines theirs. <c>IsWeekend(Weekday)</c> keeps QuantLib's answer.
/// </summary>
public class WeekendOnDateHolidayListTests
{
    [Fact]
    public void TheTelAvivListsAreQuantLibsTestsFilteredLists()
    {
        var tase = new Israel(IsraelMarket.TASE);

        // QuantLib: 66 dates in 2013, 52 of them Fridays; 63 in 2025, 52 of them Fridays
        Date[] expected2013 =
        [
            new(24, Month.February, 2013), new(25, Month.March, 2013), new(26, Month.March, 2013),
            new(31, Month.March, 2013), new(1, Month.April, 2013), new(15, Month.April, 2013), new(16, Month.April, 2013),
            new(14, Month.May, 2013), new(15, Month.May, 2013), new(16, Month.July, 2013), new(4, Month.September, 2013),
            new(5, Month.September, 2013), new(18, Month.September, 2013), new(19, Month.September, 2013),
            new(25, Month.September, 2013), new(26, Month.September, 2013),
        ];
        Assert.Equal(expected2013, tase.HolidayList(new Date(1, Month.January, 2013), new Date(31, Month.December, 2013)));

        Date[] expected2025 =
        [
            new(13, Month.April, 2025), new(30, Month.April, 2025), new(1, Month.May, 2025), new(2, Month.June, 2025),
            new(3, Month.August, 2025), new(23, Month.September, 2025), new(24, Month.September, 2025),
            new(1, Month.October, 2025), new(2, Month.October, 2025), new(6, Month.October, 2025),
            new(7, Month.October, 2025), new(13, Month.October, 2025), new(14, Month.October, 2025),
        ];
        Assert.Equal(expected2025, tase.HolidayList(new Date(1, Month.January, 2025), new Date(31, Month.December, 2025)));

        // QuantLib's isWeekend(Weekday) stands: today's weekend
        Assert.True(tase.IsWeekend(Weekday.Sunday));
        Assert.False(tase.IsWeekend(Weekday.Friday));
    }

    [Fact]
    public void AJointCalendarCombinesItsComponentsWeekendsOnTheDate()
    {
        var joint = new JointCalendar(new Israel(IsraelMarket.TASE), new TARGET(), JointCalendarRule.JoinHolidays);

        // the joint weekend on a date: the Tel Aviv one of that date, or TARGET's
        static bool WeekendOn(Date d) =>
            d.Weekday is Weekday.Saturday or Weekday.Sunday
            || (d < new Date(5, Month.January, 2026) && d.Weekday == Weekday.Friday);

        foreach (int y in (int[])[2025, 2026])
        {
            Date from = new(1, Month.January, y), to = new(31, Month.December, y);
            Date[] expected = [.. joint.HolidayList(from, to, true).Where(d => !WeekendOn(d))];

            // QuantLib: 66 dates in 2025, 52 of them Fridays; 17 in 2026, January 2nd among them
            Assert.Equal(expected, joint.HolidayList(from, to));
        }

        Assert.Equal(14, joint.HolidayList(new Date(1, Month.January, 2025), new Date(31, Month.December, 2025)).Length);
        Assert.DoesNotContain(
            new Date(2, Month.January, 2026),
            joint.HolidayList(new Date(1, Month.January, 2026), new Date(31, Month.December, 2026)));
        Assert.Equal(16, joint.HolidayList(new Date(1, Month.January, 2026), new Date(31, Month.December, 2026)).Length);
    }
}
