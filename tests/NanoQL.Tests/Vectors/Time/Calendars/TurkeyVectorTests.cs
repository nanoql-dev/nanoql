// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Turkish calendar's vectors, <c>tests/vectors/time/calendars/turkey.json</c>: the name, the weekend, and every
/// holiday on a weekday from 1901 to 2199.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TurkeyVectorTests
{
    [Fact]
    public void TurkeyVectors()
    {
        var file = VectorFile.Load("time/calendars/turkey");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new Turkey();
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(c, "holidays", calendar.HolidayList(c.DateInput("from"), c.DateInput("to")));
        }

        check.Complete();
    }
}
