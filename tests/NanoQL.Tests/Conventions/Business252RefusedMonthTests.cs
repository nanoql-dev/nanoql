// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Business252"/> counts each month of 1901 to 2199 once, at construction; a month its calendar refuses is
/// left out of that table and counted when a count reads it, as QuantLib's walk counts it
/// (<c>business252.cpp:78-135</c>), so the calendar's own refusal comes where QuantLib's does. QuantLib's
/// <c>Business252(Russia(MOEX))</c> counts 15 January 2015 to 15 January 2020 as 1,260 days and refuses a span from
/// 2010 with "MOEX calendar for the year 2010 does not exist." (<c>russia.cpp:230-232</c>).
/// </summary>
public class Business252RefusedMonthTests
{
    [Fact]
    public void ACalendarsRefusedYearsAreLeftToTheCountAndRefusedThere()
    {
        var calendar = new FromYear2012();

        // the construction counts every other month, and refuses nothing
        var dc = new Business252(calendar);

        Date d1 = new(15, Month.January, 2015), d2 = new(15, Month.January, 2020);
        Assert.Equal(calendar.BusinessDaysBetween(d1, d2), dc.DayCount(d1, d2));
        Assert.Equal(calendar.BusinessDaysBetween(d1, d2) / 252.0, dc.YearFraction(d1, d2));

        // a span from a refused year is refused at the count, with the calendar's text
        NanoQLException refused = Assert.Throws<NanoQLException>(
            () => dc.DayCount(new Date(15, Month.January, 2010), d2));
        Assert.Equal("MOEX calendar for the year 2010 does not exist.", refused.Message);
    }

    // TARGET's rules from 2012, refusing every earlier year with MOEX's text, as QuantLib's MOEX calendar does.
    private sealed class FromYear2012 : Calendar
    {
        private static readonly Calendar Rules = new TARGET();

        public override string Name => "From 2012";

        public override bool IsWeekend(Weekday w) => Rules.IsWeekend(w);

        protected override bool IsBusinessDayCore(Date d)
        {
            if (d.Year < 2012)
            {
                throw new NanoQLException($"MOEX calendar for the year {d.Year} does not exist.");
            }

            return Rules.IsBusinessDay(d);
        }
    }
}
