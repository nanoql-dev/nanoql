// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A holiday or business-day list ending on Date's last day. QuantLib's loops step their date one day past the list's
/// end (<c>calendar.cpp:281</c>, <c>:292</c>), so a list to 31 December 2199 throws "Date's serial number (109575)
/// outside allowed range", in every calendar. nanoql's loops stop at the end; the lists are QuantLib's own to 30
/// December and its own <c>isHoliday</c> of the 31st.
/// </summary>
public class CalendarListEndTests
{
    private static readonly Date From = new(1, Month.December, 2199);
    private static readonly Date LastDay = new(31, Month.December, 2199);

    [Fact]
    public void AHolidayListEndsOnDatesLastDay()
    {
        Calendar target = new TARGET();

        // QuantLib: throws "Date's serial number (109575) outside allowed range [367-109574], i.e. [January 1st,
        // 1901-December 31st, 2199]"; to 30 December, [December 25th, 2199, December 26th, 2199]; 31 December isn't a
        // holiday
        Assert.Equal(
            new[] { new Date(25, Month.December, 2199), new Date(26, Month.December, 2199) },
            target.HolidayList(From, LastDay));
        Assert.Equal(target.HolidayList(From, LastDay - 1), target.HolidayList(From, LastDay));
        Assert.False(target.IsHoliday(LastDay));

        // with weekends: QuantLib's 11 to 30 December, the 31st a business day
        Assert.Equal(11, target.HolidayList(From, LastDay, true).Length);
        Assert.Empty(target.HolidayList(LastDay, LastDay, true));
    }

    [Fact]
    public void ABusinessDayListEndsOnDatesLastDay()
    {
        Calendar target = new TARGET();

        // QuantLib: throws as above; to 30 December 19 business days, and the 31st is one
        Assert.Equal(20, target.BusinessDayList(From, LastDay).Length);
        Assert.Equal(LastDay, target.BusinessDayList(From, LastDay)[^1]);
        Assert.Equal(19, target.BusinessDayList(From, LastDay - 1).Length);
        Assert.Equal(new[] { LastDay }, target.BusinessDayList(LastDay, LastDay));
    }
}
