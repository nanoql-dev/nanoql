// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Sweden calendar's vectors, <c>tests/vectors/time/calendars/sweden.json</c>: the name, the weekend, and every
/// holiday on a weekday from 1901 to 2199.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwedenVectorTests
{
    [Fact]
    public void SwedenVectors()
    {
        var file = VectorFile.Load("time/calendars/sweden");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new Sweden();
            var skip = new HashSet<int>(c.Ints("skip"));
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(
                c,
                "holidays",
                calendar.HolidayList(c.DateInput("from"), c.DateInput("to"), c.Bool("includeWeekEnds"))
                    .Where(d => !skip.Contains(d.SerialNumber)));
        }

        check.Complete();
    }
}
