// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The TARGET calendar's vectors, <c>tests/vectors/time/calendars/target.json</c>: its name, its weekend, and every
/// holiday on a weekday from 1901 to 2199.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TargetVectorTests
{
    [Fact]
    public void TargetVectors()
    {
        var file = VectorFile.Load("time/calendars/target");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new TARGET();
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(c, "holidays", calendar.HolidayList(c.DateInput("from"), c.DateInput("to")));
        }

        check.Complete();
    }
}
