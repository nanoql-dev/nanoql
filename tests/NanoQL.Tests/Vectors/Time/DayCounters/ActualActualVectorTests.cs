// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Actual/Actual vectors, <c>tests/vectors/time/daycounters/actualactual.json</c>: the name, the day count and the
/// year fraction under the ISDA and AFB conventions and their aliases; the name and the year fraction under the ISMA
/// and Bond conventions with reference periods and with schedules.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ActualActualVectorTests
{
    [Fact]
    public void ActualActual()
    {
        var file = VectorFile.Load("time/daycounters/actualactual");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var convention = (ActualActualConvention)c.Int("convention");
            if (c.Inputs.TryGetProperty("schedule", out JsonElement spec))
            {
                var dc = new ActualActual(convention, BuildSchedule(spec, Context(c)));
                Date[] d1 = c.DateInputs("d1");
                Date[] d2 = c.DateInputs("d2");
                check.Output(c, "name", dc.Name);
                check.Output(
                    c,
                    "yearFraction",
                    d1.Zip(d2, (a, b) => (Func<object?>)(() => dc.YearFraction(a, b))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("refPeriodStart", out _))
            {
                var dc = new ActualActual(convention);
                Date d1 = c.DateInput("d1");
                Date[] d2 = c.DateInputs("d2");
                Date refPeriodStart = c.DateInput("refPeriodStart"), refPeriodEnd = c.DateInput("refPeriodEnd");
                check.Output(c, "name", dc.Name);
                check.Output(c, "yearFraction", Each(d2, x => dc.YearFraction(d1, x, refPeriodStart, refPeriodEnd)));
            }
            else
            {
                var dc = new ActualActual(convention);
                Date d1 = c.DateInput("d1");
                Date[] d2 = c.DateInputs("d2");
                check.Output(c, "name", dc.Name);
                if (c.Expected.TryGetProperty("dayCount", out _))
                {
                    check.Output(c, "dayCount", Each(d2, x => dc.DayCount(d1, x)));
                }

                check.Output(c, "yearFraction", Each(d2, x => dc.YearFraction(d1, x)));
            }
        }

        check.Complete();
    }

    private static EvaluationContext Context(VectorCase c) =>
        new() { EvaluationDate = new Date(c.Input("settings").GetProperty("evaluationDate").GetInt32()) };

    private static Schedule BuildSchedule(JsonElement spec, EvaluationContext context)
    {
        Assert.Equal("NullCalendar", spec.GetProperty("calendar").GetString());
        JsonElement tenor = spec.GetProperty("tenor");
        return new Schedule(
            new Date(spec.GetProperty("effectiveDate").GetInt32()),
            new Date(spec.GetProperty("terminationDate").GetInt32()),
            new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()),
            new NullCalendar(),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            (BusinessDayConvention)spec.GetProperty("terminationDateConvention").GetInt32(),
            (DateGenerationRule)spec.GetProperty("rule").GetInt32(),
            spec.GetProperty("endOfMonth").GetBoolean(),
            context,
            NullableDate(spec.GetProperty("firstDate")),
            NullableDate(spec.GetProperty("nextToLastDate")));
    }

    private static Date? NullableDate(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : new Date(e.GetInt32());

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) =>
        dates.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
