// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The calendar interface's vectors, <c>tests/vectors/time/calendar.json</c>, on TARGET, the US settlement calendar,
/// the London stock exchange and a joint calendar: business days, start and end of month, <c>adjust</c> under every
/// convention, <c>advance</c> and <c>businessDaysBetween</c> on every third day from December 2025 to February 2027; a
/// TARGET calendar with one holiday added and one removed; and holiday and business-day lists.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CalendarVectorTests
{
    [Fact]
    public void CalendarVectors()
    {
        var file = VectorFile.Load("time/calendar");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = Make(c.Input("calendar"));
            switch (c.String("kind"))
            {
                case "date":
                    CheckDate(check, c, calendar);
                    break;
                case "edited":
                    // QuantLib edits the calendar in place; nanoql's AddHoliday and RemoveHoliday return the edited copy.
                    foreach (Date added in c.DateInputs("added"))
                    {
                        calendar = calendar.AddHoliday(added);
                    }

                    foreach (Date removed in c.DateInputs("removed"))
                    {
                        calendar = calendar.RemoveHoliday(removed);
                    }

                    check.Output(c, "isBusinessDay", c.DateInputs("dates").Select(calendar.IsBusinessDay).ToArray());
                    check.Output(c, "addedHolidays", calendar.AddedHolidays);
                    check.Output(c, "removedHolidays", calendar.RemovedHolidays);
                    check.Output(c, "name", calendar.Name);
                    break;
                case "lists":
                    Date from = c.DateInput("from"), to = c.DateInput("to");
                    check.Output(c, "holidayListWithWeekends", calendar.HolidayList(from, to, true));
                    check.Output(c, "businessDayList", calendar.BusinessDayList(from, to));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckDate(VectorAssert check, VectorCase c, Calendar calendar)
    {
        Date d = c.DateInput("date");
        check.Output(c, "isBusinessDay", calendar.IsBusinessDay(d));
        check.Output(c, "isHoliday", calendar.IsHoliday(d));
        check.Output(c, "isStartOfMonth", calendar.IsStartOfMonth(d));
        check.Output(c, "startOfMonth", calendar.StartOfMonth(d));
        check.Output(c, "isEndOfMonth", calendar.IsEndOfMonth(d));
        check.Output(c, "endOfMonth", calendar.EndOfMonth(d));
        check.Output(
            c,
            "adjust",
            c.Ints("conventions").Select(x => (object)calendar.Adjust(d, (BusinessDayConvention)x)).ToArray());

        // advance: one row per period, one column per convention and end-of-month setting.
        var conventions = c.Input("advanceConventions").EnumerateArray()
            .Select(e => (Convention: (BusinessDayConvention)e[0].GetInt32(), EndOfMonth: e[1].GetBoolean()))
            .ToArray();
        check.Output(
            c,
            "advance",
            c.Input("advances").EnumerateArray()
                .Select(a => conventions
                    .Select(x => (Func<object?>)(() =>
                        calendar.Advance(d, a[0].GetInt32(), (TimeUnit)a[1].GetInt32(), x.Convention, x.EndOfMonth)))
                    .ToArray())
                .ToArray());

        // businessDaysBetween: one row per span, one column per inclusion setting.
        var inclusions = c.Input("inclusions").EnumerateArray()
            .Select(e => (IncludeFirst: e[0].GetBoolean(), IncludeLast: e[1].GetBoolean()))
            .ToArray();
        check.Output(
            c,
            "businessDaysBetween",
            c.Ints("businessDaysBetween")
                .Select(k => inclusions
                    .Select(i => calendar.BusinessDaysBetween(d, d + k, i.IncludeFirst, i.IncludeLast))
                    .ToArray())
                .ToArray());
    }

    // A calendar from its description in the vector file, as the generator builds it.
    private static Calendar Make(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        switch (type)
        {
            case "TARGET":
                return new TARGET();
            case "UnitedStates":
                return new UnitedStates((UnitedStatesMarket)spec.GetProperty("market").GetInt32());
            case "UnitedKingdom":
                return new UnitedKingdom((UnitedKingdomMarket)spec.GetProperty("market").GetInt32());
            case "JointCalendar":
                Calendar[] cs = spec.GetProperty("calendars").EnumerateArray().Select(Make).ToArray();
                var r = (JointCalendarRule)spec.GetProperty("rule").GetInt32();
                return cs.Length switch
                {
                    2 => new JointCalendar(cs[0], cs[1], r),
                    _ => throw new InvalidOperationException($"no joint calendar of {cs.Length} calendars in the vectors"),
                };
            default:
                throw new InvalidOperationException($"unknown calendar type '{type}'");
        }
    }
}
