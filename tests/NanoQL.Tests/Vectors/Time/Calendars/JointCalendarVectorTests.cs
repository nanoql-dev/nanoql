// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The joint calendars' vectors, <c>tests/vectors/time/calendars/jointcalendar.json</c>: five joins of two, three and
/// four calendars under both rules, and a join of five calendars built from a sequence, each with its name, its
/// weekend, and every holiday on a weekday from 1990 to 2060.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JointCalendarVectorTests
{
    [Fact]
    public void JointCalendarVectors()
    {
        var file = VectorFile.Load("time/calendars/jointcalendar");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = Make(c.Input("calendar"));
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(c, "holidays", calendar.HolidayList(c.DateInput("from"), c.DateInput("to")));
        }

        check.Complete();
    }

    // A calendar from its description in the vector file, as the generator builds it: a joint calendar of two, three
    // or four calendars through the constructor taking that many, any other through the one taking a sequence.
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
            case "WeekendsOnly":
                return new WeekendsOnly();
            case "JointCalendar":
                Calendar[] cs = spec.GetProperty("calendars").EnumerateArray().Select(Make).ToArray();
                var r = (JointCalendarRule)spec.GetProperty("rule").GetInt32();
                return cs.Length switch
                {
                    2 => new JointCalendar(cs[0], cs[1], r),
                    3 => new JointCalendar(cs[0], cs[1], cs[2], r),
                    4 => new JointCalendar(cs[0], cs[1], cs[2], cs[3], r),
                    _ => new JointCalendar(cs, r),
                };
            default:
                throw new InvalidOperationException($"unknown calendar type '{type}'");
        }
    }
}
