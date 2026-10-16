// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>The date vector, <c>tests/vectors/time/date.json</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class DateVectorTests
{
    [Fact]
    public void DateVector()
    {
        var file = VectorFile.Load("time/date");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("accessors", StringComparison.Ordinal))
            {
                Date d = c.DateInput("date");
                check.Output(c, "weekday", d.Weekday);
                check.Output(c, "dayOfMonth", d.DayOfMonth);
                check.Output(c, "dayOfYear", d.DayOfYear);
                check.Output(c, "month", d.Month);
                check.Output(c, "year", d.Year);
                check.Output(c, "isLeap", Date.IsLeap(d.Year));
                check.Output(c, "startOfMonth", Date.StartOfMonth(d));
                check.Output(c, "isStartOfMonth", Date.IsStartOfMonth(d));
                check.Output(c, "endOfMonth", Date.EndOfMonth(d));
                check.Output(c, "isEndOfMonth", Date.IsEndOfMonth(d));
                check.Output(c, "nextWeekdayWednesday", () => Date.NextWeekday(d, Weekday.Wednesday));
                check.Output(c, "toString", d.ToString());
                check.Output(c, "iso", DateFunctions.IsoDate(d));
            }
            else if (c.Name.StartsWith("plus periods", StringComparison.Ordinal))
            {
                Date d = c.DateInput("date");
                var results = c.Input("periods").EnumerateArray()
                    .Select(p => new Period(p[0].GetInt32(), (TimeUnit)p[1].GetInt32()))
                    .Select(p => (Func<object?>)(() => d + p))
                    .ToArray();
                check.Output(c, "result", results);
            }
            else if (c.Name.StartsWith("nthWeekday", StringComparison.Ordinal))
            {
                int y = c.Int("year");
                var m = (Month)c.Int("month");
                var results = c.Ints("n")
                    .Select(n => c.Ints("weekday")
                        .Select(w => (Func<object?>)(() => Date.NthWeekday(n, (Weekday)w, m, y)))
                        .ToArray())
                    .ToArray();
                check.Output(c, "result", results);
            }
            else
            {
                Date d1 = c.DateInput("d1"), d2 = c.DateInput("d2");
                check.Output(c, "difference", d2 - d1);
                check.Output(c, "daysBetween", DateFunctions.DaysBetween(d1, d2));
            }
        }

        check.Complete();
    }
}
