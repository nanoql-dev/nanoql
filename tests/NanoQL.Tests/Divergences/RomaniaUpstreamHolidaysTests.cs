// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Romania's Epiphany and St John the Baptist, January 6th and 7th from 2024, and Orthodox Good Friday from 2018.
/// QuantLib 1.44 has none of them (<c>romania.cpp:45-78</c>); QuantLib's master adds them to the public calendar
/// (#2919, <c>657346746</c>), and so to the Bucharest stock exchange's, which derives from it, with nine new test
/// lines, which nanoql takes ahead of the pin, as a fix. The lines below are upstream's; QuantLib 1.44's answers are
/// recorded beside them.
/// </summary>
public class RomaniaUpstreamHolidaysTests
{
    [Fact]
    public void EpiphanyStJohnAndOrthodoxGoodFriday()
    {
        // Upstream: QuantLib's master's lines for #2919 (calendars.cpp's testRomaniaCalendars)
        Calendar publicCalendar = new Romania(RomaniaMarket.Public);
        Calendar exchange = new Romania(RomaniaMarket.BVB);

        // QuantLib's master: holidays (1.44: business days)
        Assert.True(publicCalendar.IsHoliday(new Date(6, Month.April, 2018))); // Good Friday
        Assert.True(publicCalendar.IsHoliday(new Date(6, Month.January, 2025))); // Epiphany
        Assert.True(publicCalendar.IsHoliday(new Date(7, Month.January, 2025))); // St John the Baptist

        // business days in both: the years before each holiday's first
        Assert.True(publicCalendar.IsBusinessDay(new Date(14, Month.April, 2017)));
        Assert.True(publicCalendar.IsBusinessDay(new Date(6, Month.January, 2023)));

        // QuantLib's master: holidays (1.44: business days)
        Assert.True(exchange.IsHoliday(new Date(18, Month.April, 2025))); // Good Friday
        Assert.True(exchange.IsHoliday(new Date(6, Month.January, 2026)));
        Assert.True(exchange.IsHoliday(new Date(7, Month.January, 2026)));
        Assert.True(exchange.IsHoliday(new Date(10, Month.April, 2026))); // Good Friday
    }
}
