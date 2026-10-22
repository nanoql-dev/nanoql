// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Botswana's Tuesday proviso. QuantLib's header quotes the Public Holidays Act: "if 2nd January, 1st October or Boxing
/// Day falls on a Monday, the following Tuesday shall be observed as a public holiday" (<c>botswana.hpp</c>), and its
/// code observes that Tuesday for January 2nd alone (<c>botswana.cpp</c>, <c>d == 3 &amp;&amp; w == Tuesday</c> in
/// January): Tuesday December 27th after a Monday Boxing Day, and Tuesday October 2nd after a Monday October 1st, are
/// business days in QuantLib in every such year from 1901 to 2199. nanoql observes them by the January branch's own
/// expression; QuantLib's January 2nd branch is asserted beside them.
/// </summary>
public class BotswanaTuesdayProvisoTests
{
    [Fact]
    public void TheTuesdayAfterAMondayBoxingDayIsAHoliday()
    {
        var botswana = new Botswana();

        // QuantLib: business days
        Assert.True(botswana.IsHoliday(new Date(27, Month.December, 2011)));
        Assert.True(botswana.IsHoliday(new Date(27, Month.December, 2016)));
        Assert.True(botswana.IsHoliday(new Date(27, Month.December, 2022)));

        int years = 0;
        for (int y = 1901; y <= 2199; ++y)
        {
            var boxingDay = new Date(26, Month.December, y);
            if (boxingDay.Weekday == Weekday.Monday)
            {
                ++years;
                Assert.True(botswana.IsHoliday(boxingDay + 1), $"{boxingDay + 1}");
            }
        }

        Assert.Equal(43, years);

        // QuantLib's own: the Tuesday after a Monday January 2nd
        Assert.True(botswana.IsHoliday(new Date(3, Month.January, 2012)));
        Assert.True(botswana.IsHoliday(new Date(3, Month.January, 2017)));
        Assert.True(botswana.IsHoliday(new Date(3, Month.January, 2023)));
    }

    [Fact]
    public void TheTuesdayAfterAMondayFirstOfOctoberIsAHoliday()
    {
        var botswana = new Botswana();

        // QuantLib: business days
        Assert.True(botswana.IsHoliday(new Date(2, Month.October, 2012)));
        Assert.True(botswana.IsHoliday(new Date(2, Month.October, 2018)));
        Assert.True(botswana.IsHoliday(new Date(2, Month.October, 2029)));

        int years = 0;
        for (int y = 1901; y <= 2199; ++y)
        {
            var first = new Date(1, Month.October, y);
            if (first.Weekday == Weekday.Monday)
            {
                ++years;
                Assert.True(botswana.IsHoliday(first + 1), $"{first + 1}");
            }
        }

        Assert.Equal(43, years);
    }
}
