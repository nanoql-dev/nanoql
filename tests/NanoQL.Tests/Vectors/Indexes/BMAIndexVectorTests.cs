// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BMA index vectors, <c>tests/vectors/indexes/bmaindex.json</c>: <see cref="BMAIndex"/>'s conventions, its valid
/// fixing dates over three years, its date functions and fixing schedules, its forecasts; and, replacing QuantLib's
/// <c>testFixingObservability</c>, a fixing added through one BMA index and read through another, in a second context
/// whose store holds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BMAIndexVectorTests
{
    [Fact]
    public void BMAIndexVector()
    {
        var file = VectorFile.Load("indexes/bmaindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Name == "conventions")
            {
                var index = new BMAIndex(IndexInputs.CurveOf(c.Input("curve")));
                Date from = c.DateInput("from"), to = c.DateInput("to");
                check.Output(c, "name", index.Name);
                check.Output(c, "familyName", index.FamilyName);
                check.Output(c, "tenor", new object[] { index.Tenor.Length, index.Tenor.Units });
                check.Output(c, "fixingDays", index.FixingDays);
                check.Output(c, "currency", index.Currency!.Code);
                check.Output(c, "dayCounter", index.DayCounter.Name);
                check.Output(c, "fixingCalendar", index.FixingCalendar.Name);
                check.Output(c, "forwardingTermStructureEmpty", index.ForwardingTermStructure is null);
                check.Output(
                    c,
                    "isValidFixingDate",
                    Enumerable.Range(0, (to - from) + 1).Select(n => index.IsValidFixingDate(from + n)));
            }
            else if (c.Name.StartsWith("dates ", StringComparison.Ordinal))
            {
                var index = new BMAIndex();
                Date d = c.DateInput("date"), end = c.DateInput("end");
                check.Output(c, "fixingDate", () => index.FixingDate(d));
                check.Output(c, "valueDate", () => index.ValueDate(d));
                check.Output(c, "maturityDate", () => index.MaturityDate(d));
                check.Output(c, "fixingSchedule", () => index.FixingSchedule(d, end).Dates.Select(x => (object)x));
            }
            else if (c.Name.StartsWith("forecast ", StringComparison.Ordinal))
            {
                var index = new BMAIndex(IndexInputs.CurveOf(c.Input("curve")));
                check.Output(
                    c,
                    "fixing",
                    c.DateInputs("dates").Select(d => (Func<object?>)(() => index.Fixing(d, context))).ToArray());
            }
            else
            {
                CheckObservability(check, c, context);
            }
        }

        check.Complete();
    }

    // A fixing added through one index is read through another: the second context holds it, the first doesn't.
    private static void CheckObservability(VectorAssert check, VectorCase c, EvaluationContext before)
    {
        var writer = new BMAIndex();
        var reader = new BMAIndex(IndexInputs.CurveOf(c.Input("curve")));
        (Date added, double addedValue) = Pair(c.Input("addedFixing"));
        (Date today, double todaysValue) = Pair(c.Input("todaysFixing"));
        EvaluationContext after = before with { Fixings = before.Fixings.AddFixing(writer, added, addedValue) };
        EvaluationContext withToday = after with { Fixings = after.Fixings.AddFixing(writer, today, todaysValue) };
        Date invalid = c.DateInput("invalidDate");

        check.Output(c, "fixingBefore", () => reader.Fixing(added, before));
        check.Output(c, "fixingAfter", () => reader.Fixing(added, after));
        check.Output(c, "todaysStoredFixing", () => reader.Fixing(today, withToday));
        check.Output(c, "todaysForecastFixing", () => reader.Fixing(today, withToday, forecastTodaysFixing: true));
        check.Output(c, "invalidFixing", () => withToday.Fixings.AddFixing(writer, invalid, 0.03));
    }

    private static (Date Date, double Value) Pair(JsonElement e) => (new Date(e[0].GetInt32()), e[1].GetDouble());
}
