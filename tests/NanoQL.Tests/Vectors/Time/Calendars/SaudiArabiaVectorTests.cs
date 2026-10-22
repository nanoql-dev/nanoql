// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SaudiArabia calendars' vectors, <c>tests/vectors/time/calendars/saudiarabia.json</c>, one case per market: the
/// name, the weekend, and every holiday from 1901 to 2199, weekend days included.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SaudiArabiaVectorTests
{
    [Fact]
    public void SaudiArabiaVectors()
    {
        var file = VectorFile.Load("time/calendars/saudiarabia");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new SaudiArabia((SaudiArabiaMarket)c.Int("market"));
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
