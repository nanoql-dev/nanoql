// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A <see cref="BespokeCalendar"/> is a value: <see cref="BespokeCalendar.AddWeekend"/>, like
/// <see cref="Calendar.AddHoliday"/>, returns a modified copy, where QuantLib's copies share one implementation that
/// an edit changes (<c>bespokecalendar.hpp:32-35</c>). Two bespoke calendars are equal when their names, weekends,
/// added holidays on working weekdays and removed holidays are, where QuantLib compares names alone
/// (<c>bespokecalendar.hpp:55-58</c>). QuantLib's members carry the value, so a calendar rebuilt from them, its weekend
/// first, equals the original whatever the order of its edits.
/// </summary>
public class BespokeCalendarValueTests
{
    private static readonly Weekday[] Weekdays =
    [
        Weekday.Sunday, Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday,
        Weekday.Saturday,
    ];

    [Fact]
    public void AddWeekendReturnsACopy()
    {
        var a = new BespokeCalendar("A");
        Date sunday = new(5, Month.October, 2008);

        BespokeCalendar b = a.AddWeekend(Weekday.Sunday);

        Assert.True(a.IsBusinessDay(sunday));
        Assert.False(a.IsWeekend(Weekday.Sunday));
        Assert.False(b.IsBusinessDay(sunday));
        Assert.True(b.IsWeekend(Weekday.Sunday));
        Assert.False(a == b);

        // a holiday added to the copy stays in the copy
        Date monday = new(6, Month.October, 2008);
        Calendar c = b.AddHoliday(monday);
        Assert.IsType<BespokeCalendar>(c);
        Assert.True(b.IsBusinessDay(monday));
        Assert.False(c.IsBusinessDay(monday));
    }

    [Fact]
    public void SameNamedCalendarsWithDifferentWeekendsAreUnequal()
    {
        // QuantLib: equal, by name
        BespokeCalendar a = new BespokeCalendar("X").AddWeekend(Weekday.Saturday).AddWeekend(Weekday.Sunday);
        var b = new BespokeCalendar("X");
        Assert.Equal(a.Name, b.Name);
        Assert.False(a == b);
        Assert.False(b == a);

        // the same weekend, added in another order, is equal and hashes alike
        BespokeCalendar c = new BespokeCalendar("X").AddWeekend(Weekday.Sunday).AddWeekend(Weekday.Saturday);
        Assert.True(a == c);
        Assert.Equal(a.GetHashCode(), c.GetHashCode());

        // and two unnamed calendars, QuantLib's same-name warning
        Assert.True(new BespokeCalendar() == new BespokeCalendar());
        Assert.False(new BespokeCalendar() == new BespokeCalendar().AddWeekend(Weekday.Friday));
    }

    [Fact]
    public void ACalendarRebuiltFromQuantLibsMembersIsEqual()
    {
        var original = (BespokeCalendar)new BespokeCalendar("Y")
            .AddWeekend(Weekday.Friday)
            .AddWeekend(Weekday.Saturday)
            .AddHoliday(new Date(1, Month.January, 2026))
            .AddHoliday(new Date(25, Month.December, 2026))
            .RemoveHoliday(new Date(4, Month.July, 2026));

        BespokeCalendar rebuilt = Rebuild(original);

        Assert.True(original == rebuilt);
        Assert.Equal(original.GetHashCode(), rebuilt.GetHashCode());
        Assert.Equal(original.AddedHolidays, rebuilt.AddedHolidays);
        Assert.Equal(original.RemovedHolidays, rebuilt.RemovedHolidays);
    }

    [Fact]
    public void AHolidayAddedBeforeItsWeekdayBecameAWeekendDayRoundTrips()
    {
        Date saturday = new(6, Month.June, 2026);

        // the holiday first, then the weekend that covers it: QuantLib records it, {June 6th, 2026}
        var original = (BespokeCalendar)new BespokeCalendar("X").AddHoliday(saturday);
        original = original.AddWeekend(Weekday.Saturday).AddWeekend(Weekday.Sunday);
        Assert.Equal(new[] { saturday }, original.AddedHolidays);

        // rebuilt weekend first, the same holiday isn't recorded, and every date answers alike
        BespokeCalendar rebuilt = Rebuild(original);
        Assert.Empty(rebuilt.AddedHolidays);
        for (Date d = new(1, Month.January, 2026); d <= new Date(31, Month.December, 2026); ++d)
        {
            Assert.Equal(original.IsBusinessDay(d), rebuilt.IsBusinessDay(d));
        }

        Assert.True(original == rebuilt);
        Assert.True(rebuilt == original);
        Assert.Equal(original.GetHashCode(), rebuilt.GetHashCode());
    }

    // The round trip from QuantLib's members: the name, the weekend over the seven weekdays, then the added and removed
    // holidays.
    private static BespokeCalendar Rebuild(BespokeCalendar c)
    {
        var rebuilt = new BespokeCalendar(c.Name);
        foreach (Weekday w in Weekdays)
        {
            if (c.IsWeekend(w))
            {
                rebuilt = rebuilt.AddWeekend(w);
            }
        }

        foreach (Date d in c.AddedHolidays)
        {
            rebuilt = (BespokeCalendar)rebuilt.AddHoliday(d);
        }

        foreach (Date d in c.RemovedHolidays)
        {
            rebuilt = (BespokeCalendar)rebuilt.RemoveHoliday(d);
        }

        return rebuilt;
    }
}
