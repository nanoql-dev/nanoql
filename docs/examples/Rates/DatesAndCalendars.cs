// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Dates and calendars: rolling dates on a calendar, a coupon schedule and its accrual fractions.</summary>
public static class DatesAndCalendars
{
    /// <summary>The example's outputs.</summary>
    /// <returns>The rolled dates, and the schedule with each period's accrual fractions.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        // <roll>
        var calendar = new TARGET();
        var trade = new Date(23, Month.December, 2026);
        Date spot = calendar.Advance(trade, 2, TimeUnit.Days);
        Date oneYear = calendar.Advance(spot, new Period(1, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing);
        Date endOfMonth = calendar.Adjust(new Date(31, Month.October, 2026), BusinessDayConvention.ModifiedFollowing);
        // </roll>
        var rolled = new Table("Date", "Weekday", "Business day");
        foreach (Date d in new[] { trade, spot, oneYear, endOfMonth })
        {
            rolled.Add(Table.D(d), d.Weekday.ToString(), calendar.IsBusinessDay(d) ? "yes" : "no");
        }

        yield return ("dates-roll", rolled);

        // <schedule>
        Schedule schedule = new MakeSchedule()
            .From(spot)
            .To(calendar.Advance(spot, new Period(2, TimeUnit.Years)))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();

        DayCounter[] dayCounters =
        [
            new Actual360(), new Actual365Fixed(), new Thirty360(Thirty360Convention.BondBasis),
            new ActualActual(ActualActualConvention.ISDA),
        ];
        // </schedule>
        var periods = new Table("Start", "End", ">Days", ">Actual/360", ">Actual/365 (Fixed)", ">30/360", ">Actual/Actual (ISDA)");
        for (int i = 1; i < schedule.Size; i++)
        {
            Date start = schedule[i - 1], end = schedule[i];
            periods.Add(
                [Table.D(start), Table.D(end), (end - start).ToString(System.Globalization.CultureInfo.InvariantCulture),
                 .. dayCounters.Select(dc => Table.N(dc.YearFraction(start, end), 6))]);
        }

        yield return ("dates-schedule", periods);
    }
}
