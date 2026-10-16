// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The 30/360 vectors, <c>tests/vectors/time/daycounters/thirty360.json</c>: the name, the day count and the year
/// fraction under all nine conventions, and under the German and ISDA ones with a termination date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Thirty360VectorTests
{
    [Fact]
    public void Thirty360()
    {
        var file = VectorFile.Load("time/daycounters/thirty360");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var dc = new Thirty360((Thirty360Convention)c.Int("convention"), c.NullableDateInput("terminationDate"));
            Date d1 = c.DateInput("d1");
            Date[] d2 = c.DateInputs("d2");
            check.Output(c, "name", dc.Name);
            check.Output(c, "dayCount", Each(d2, x => dc.DayCount(d1, x)));
            check.Output(c, "yearFraction", Each(d2, x => dc.YearFraction(d1, x)));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) =>
        dates.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
