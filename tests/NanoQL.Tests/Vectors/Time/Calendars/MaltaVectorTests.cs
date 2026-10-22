// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Malta calendars' vectors, <c>tests/vectors/time/calendars/malta.json</c>, one case per market: the name, the
/// weekend on Mondays to Thursdays and Saturdays, and every holiday on a weekday from 1901 to 2199, the Fridays and
/// Sundays a documented divergence changes left out.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MaltaVectorTests
{
    [Fact]
    public void MaltaVectors()
    {
        var file = VectorFile.Load("time/calendars/malta");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new Malta((MaltaMarket)c.Int("market"));
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
