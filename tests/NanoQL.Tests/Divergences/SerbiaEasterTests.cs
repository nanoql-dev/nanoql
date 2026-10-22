// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Serbia's Easter. QuantLib's Belgrade calendar derives from <c>WesternImpl</c> (<c>serbia.hpp:50</c>), so its Good
/// Friday and Easter Monday follow Western Easter, where the Belgrade Stock Exchange's calendar, which the header
/// cites, closes on Orthodox Easter: May 3rd and 6th, 2024, April 10th and 13th, 2026. nanoql takes
/// <c>OrthodoxImpl</c>'s Easter Monday, the one QuantLib's Romania, Russia, Ukraine and North Macedonia read, with the
/// same weekend. <c>testSerbia</c>'s Easter dates (<c>test-suite/calendars.cpp:4275-4302</c>) move here, QuantLib's
/// recorded beside nanoql's.
/// </summary>
public class SerbiaEasterTests
{
    [Fact]
    public void GoodFridayAndEasterMondayFollowOrthodoxEaster()
    {
        var serbia = new Serbia();

        // Orthodox Good Friday and Easter Monday (QuantLib: business days, but in 2025, when the two Easters meet)
        Assert.True(serbia.IsHoliday(new Date(3, Month.May, 2024)));
        Assert.True(serbia.IsHoliday(new Date(6, Month.May, 2024)));
        Assert.True(serbia.IsHoliday(new Date(18, Month.April, 2025)));
        Assert.True(serbia.IsHoliday(new Date(21, Month.April, 2025)));
        Assert.True(serbia.IsHoliday(new Date(10, Month.April, 2026)));
        Assert.True(serbia.IsHoliday(new Date(13, Month.April, 2026)));
        Assert.True(serbia.IsHoliday(new Date(30, Month.April, 2027)));
        Assert.True(serbia.IsHoliday(new Date(3, Month.May, 2027)));

        // Western Good Friday and Easter Monday (QuantLib: holidays)
        Assert.True(serbia.IsBusinessDay(new Date(29, Month.March, 2024)));
        Assert.True(serbia.IsBusinessDay(new Date(1, Month.April, 2024)));
        Assert.True(serbia.IsBusinessDay(new Date(3, Month.April, 2026)));
        Assert.True(serbia.IsBusinessDay(new Date(6, Month.April, 2026)));
        Assert.True(serbia.IsBusinessDay(new Date(26, Month.March, 2027)));
        Assert.True(serbia.IsBusinessDay(new Date(29, Month.March, 2027)));
    }

    [Fact]
    public void TestSerbiasEasterDates()
    {
        // testSerbia's list, the Orthodox Good Friday and Easter Monday in place of QuantLib's April 3rd and 6th, 2026,
        // and March 26th and 29th, 2027
        Date[] expected =
        [
            new(1, Month.January, 2026), new(2, Month.January, 2026), new(7, Month.January, 2026),
            new(16, Month.February, 2026), new(10, Month.April, 2026), new(13, Month.April, 2026), new(1, Month.May, 2026),
            new(11, Month.November, 2026), new(31, Month.December, 2026),
            new(1, Month.January, 2027), new(7, Month.January, 2027), new(15, Month.February, 2027),
            new(16, Month.February, 2027), new(30, Month.April, 2027), new(3, Month.May, 2027),
            new(11, Month.November, 2027), new(31, Month.December, 2027),
        ];
        Assert.Equal(
            expected, new Serbia().HolidayList(new Date(1, Month.January, 2026), new Date(31, Month.December, 2027)));
    }
}
