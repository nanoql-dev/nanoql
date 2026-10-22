// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bespoke calendars' vectors, <c>tests/vectors/time/calendars/bespokecalendar.json</c>, one case per calendar
/// built by QuantLib's members: the name, the weekend, the added and removed holidays, the holidays of 2026 and the
/// business days between pairs of dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BespokeCalendarVectorTests
{
    [Fact]
    public void BespokeCalendarVectors()
    {
        var file = VectorFile.Load("time/calendars/bespokecalendar");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var calendar = new BespokeCalendar(c.String("calendarName"));
            foreach (int w in c.Ints("weekend"))
            {
                calendar = calendar.AddWeekend((Weekday)w);
            }

            foreach (Date d in c.DateInputs("added"))
            {
                calendar = (BespokeCalendar)calendar.AddHoliday(d);
            }

            foreach (Date d in c.DateInputs("removed"))
            {
                calendar = (BespokeCalendar)calendar.RemoveHoliday(d);
            }

            Date[][] spans = [.. c.Input("spans").EnumerateArray().Select(s => s.EnumerateArray().Select(e => new Date(e.GetInt32())).ToArray())];
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(c, "addedHolidays", calendar.AddedHolidays);
            check.Output(c, "removedHolidays", calendar.RemovedHolidays);
            check.Output(c, "holidays", calendar.HolidayList(c.DateInput("from"), c.DateInput("to"), true));
            check.Output(c, "businessDaysBetween", spans.Select(s => (object?)calendar.BusinessDaysBetween(s[0], s[1])).ToArray());
        }

        check.Complete();
    }
}
