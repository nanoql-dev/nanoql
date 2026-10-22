// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Malta's weekend. QuantLib's header lists Saturdays and Sundays, as the Malta Stock Exchange's trading calendar it
/// cites does, but its code overrides <c>WesternImpl</c>'s weekend with Friday and Saturday (<c>malta.cpp:30-32</c>),
/// so every Friday is a holiday and every Sunday a business day; the code, the header and QuantLib's test came in one
/// commit, the test listing what the code gives. nanoql keeps <c>WesternImpl</c>'s weekend with QuantLib's own rules,
/// which answer as QuantLib's on Mondays to Thursdays and Saturdays. <c>testMalta</c>'s list on Fridays and Sundays
/// (<c>test-suite/calendars.cpp:4142-4174</c>) moves here, QuantLib's recorded beside nanoql's.
/// </summary>
public class MaltaWeekendTests
{
    [Fact]
    public void FridaysAreBusinessDaysAndSundaysWeekendDays()
    {
        var malta = new Malta();

        // QuantLib: Friday and Saturday
        Assert.True(malta.IsWeekend(Weekday.Saturday));
        Assert.True(malta.IsWeekend(Weekday.Sunday));
        Assert.False(malta.IsWeekend(Weekday.Friday));

        // 2025 to 2027. QuantLib: all 157 Fridays holidays, 151 of the 156 Sundays business days
        int fridays = 0, businessFridays = 0, sundays = 0;
        for (Date d = new(1, Month.January, 2025); d <= new Date(31, Month.December, 2027); d++)
        {
            if (d.Weekday == Weekday.Friday)
            {
                ++fridays;
                businessFridays += malta.IsBusinessDay(d) ? 1 : 0;
            }
            else if (d.Weekday == Weekday.Sunday)
            {
                ++sundays;
                Assert.True(malta.IsHoliday(d), $"{d}");
            }
        }

        Assert.Equal(157, fridays);
        Assert.Equal(156, sundays);

        // the 11 Fridays the rules name: Good Friday and fixed dates
        Assert.Equal(157 - 11, businessFridays);
        Assert.True(malta.IsHoliday(new Date(3, Month.April, 2026)));
        Assert.True(malta.IsHoliday(new Date(25, Month.December, 2026)));
        Assert.True(malta.IsBusinessDay(new Date(10, Month.April, 2026)));
    }

    [Fact]
    public void TestMaltasListOnFridaysAndSundays()
    {
        var malta = new Malta();

        // QuantLib's (testMalta's): no Friday, and the Sundays December 13th, 2026, August 15th and December 26th, 2027
        Date[] quantLib =
        [
            new(1, Month.January, 2026), new(10, Month.February, 2026), new(19, Month.March, 2026),
            new(31, Month.March, 2026), new(6, Month.April, 2026), new(29, Month.June, 2026),
            new(8, Month.September, 2026), new(21, Month.September, 2026), new(8, Month.December, 2026),
            new(13, Month.December, 2026), new(24, Month.December, 2026), new(31, Month.December, 2026),
            new(10, Month.February, 2027), new(29, Month.March, 2027), new(31, Month.March, 2027),
            new(29, Month.June, 2027), new(15, Month.August, 2027), new(8, Month.September, 2027),
            new(21, Month.September, 2027), new(8, Month.December, 2027), new(13, Month.December, 2027),
            new(26, Month.December, 2027),
        ];

        // nanoql's: the Friday holidays, no Sunday
        Date[] expected =
        [
            new(1, Month.January, 2026), new(10, Month.February, 2026), new(19, Month.March, 2026),
            new(31, Month.March, 2026), new(3, Month.April, 2026), new(6, Month.April, 2026), new(1, Month.May, 2026),
            new(29, Month.June, 2026), new(8, Month.September, 2026), new(21, Month.September, 2026),
            new(8, Month.December, 2026), new(24, Month.December, 2026), new(25, Month.December, 2026),
            new(31, Month.December, 2026), new(1, Month.January, 2027), new(10, Month.February, 2027),
            new(19, Month.March, 2027), new(26, Month.March, 2027), new(29, Month.March, 2027),
            new(31, Month.March, 2027), new(29, Month.June, 2027), new(8, Month.September, 2027),
            new(21, Month.September, 2027), new(8, Month.December, 2027), new(13, Month.December, 2027),
            new(24, Month.December, 2027), new(31, Month.December, 2027),
        ];
        Assert.Equal(
            expected, malta.HolidayList(new Date(1, Month.January, 2026), new Date(31, Month.December, 2027)));

        // the two agree on the other days
        static bool Kept(Date d) => d.Weekday is not (Weekday.Friday or Weekday.Sunday);
        Assert.Equal(quantLib.Where(Kept), expected.Where(Kept));
    }
}
