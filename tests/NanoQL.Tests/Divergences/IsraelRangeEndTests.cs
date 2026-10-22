// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Israel's three markets at the ends of Date's range. Their rules read a neighbouring day by date arithmetic, up to 21
/// days back (Simchat Torah through Sukkot, Yom Kippur and the New Year) and one day ahead (the eves)
/// (<c>israel.cpp:374-383</c>), so QuantLib's <c>isBusinessDay</c> throws "Date's serial number (...) outside allowed
/// range" on the dates of January 1st to 21st, 1901, that no earlier rule settles (15 of them for the Tel Aviv stock
/// exchange and SHIR, 14 for Telbor), and on December 31st, 2199. nanoql reads the neighbour by serial number, in no
/// table there (the tables hold 2000 to 2050), so the rule reads false and the answer is QuantLib's weekend and
/// fixed-date rules, as on the dates just inside the range.
/// </summary>
public class IsraelRangeEndTests
{
    [Fact]
    public void TheFirstThreeWeeksOf1901Answer()
    {
        var tase = new Israel(IsraelMarket.TASE);
        var shir = new Israel(IsraelMarket.SHIR);
        var telbor = new Israel(IsraelMarket.Telbor);
        for (Date d = new(1, Month.January, 1901); d <= new Date(21, Month.January, 1901); ++d)
        {
            Weekday w = d.Weekday;

            // QuantLib: TASE answers only on Fridays and Saturdays (false), and refuses the rest
            Assert.Equal(w is not (Weekday.Friday or Weekday.Saturday), tase.IsBusinessDay(d));

            // QuantLib: SHIR answers only on weekends (false); Telbor on weekends and on January 1st (false)
            bool westernBusinessDay = w is not (Weekday.Saturday or Weekday.Sunday) && d.DayOfMonth != 1;
            Assert.Equal(westernBusinessDay, shir.IsBusinessDay(d));
            Assert.Equal(westernBusinessDay, telbor.IsBusinessDay(d));
        }

        // QuantLib's own, the first day it answers on every weekday
        Assert.True(tase.IsBusinessDay(new Date(22, Month.January, 1901)));
        Assert.True(shir.IsBusinessDay(new Date(22, Month.January, 1901)));
        Assert.True(telbor.IsBusinessDay(new Date(22, Month.January, 1901)));
    }

    [Fact]
    public void TheLastDayOf2199Answers()
    {
        var lastDay = new Date(31, Month.December, 2199);
        Assert.Equal(Weekday.Tuesday, lastDay.Weekday);
        foreach (IsraelMarket market in (IsraelMarket[])[IsraelMarket.TASE, IsraelMarket.SHIR, IsraelMarket.Telbor])
        {
            var c = new Israel(market);

            // QuantLib: throws "Date's serial number (109575) outside allowed range"; December 30th a business day
            Assert.True(c.IsBusinessDay(lastDay));
            Assert.True(c.IsBusinessDay(lastDay - 1));
            Assert.Equal(c.BusinessDayList(new Date(1, Month.December, 2199), lastDay)[^1], lastDay);
        }
    }
}
