// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Israel calendars' vectors, <c>tests/vectors/time/calendars/israel.json</c>, one case per market: the name, the
/// weekend, and every holiday on a weekday from 22 January 1901 to 2199, the Tel Aviv exchange's weekend days with
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IsraelVectorTests
{
    [Fact]
    public void IsraelVectors()
    {
        var file = VectorFile.Load("time/calendars/israel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new Israel((IsraelMarket)c.Int("market"));
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
