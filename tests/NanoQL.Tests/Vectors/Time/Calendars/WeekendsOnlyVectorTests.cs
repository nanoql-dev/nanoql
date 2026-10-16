// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The weekends-only calendar's vectors, <c>tests/vectors/time/calendars/weekendsonly.json</c>: its name, its weekend,
/// and its holidays on a weekday from 1901 to 2199, of which there are none.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class WeekendsOnlyVectorTests
{
    [Fact]
    public void WeekendsOnlyVectors()
    {
        var file = VectorFile.Load("time/calendars/weekendsonly");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new WeekendsOnly();
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(c, "holidays", calendar.HolidayList(c.DateInput("from"), c.DateInput("to")));
        }

        check.Complete();
    }
}
