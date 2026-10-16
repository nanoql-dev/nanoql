// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The United States calendars' vectors, <c>tests/vectors/time/calendars/unitedstates.json</c>, one case per market:
/// the name, the weekend, and every holiday on a weekday from 1901 to 2199.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UnitedStatesVectorTests
{
    [Fact]
    public void UnitedStatesVectors()
    {
        var file = VectorFile.Load("time/calendars/unitedstates");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar calendar = new UnitedStates((UnitedStatesMarket)c.Int("market"));
            check.Output(c, "name", calendar.Name);
            check.Output(c, "isWeekend", c.Ints("weekdays").Select(w => calendar.IsWeekend((Weekday)w)).ToArray());
            check.Output(c, "holidays", calendar.HolidayList(c.DateInput("from"), c.DateInput("to")));
        }

        check.Complete();
    }
}
