// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The day-counter equality vector, <c>tests/vectors/time/daycounter.json</c>: every convention of the four day
/// counters, compared pairwise with <c>==</c> and <c>!=</c>, which QuantLib defines by name.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DayCounterVectorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    [Fact]
    public void DayCounterEquality()
    {
        var file = VectorFile.Load("time/daycounter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            DayCounter dc = Build(c.Input("dayCounter"));
            DayCounter[] others = [.. c.Input("others").EnumerateArray().Select(Build)];
            check.Output(c, "name", dc.Name);
            check.Output(c, "equal", others.Select(o => (object?)(dc == o)).ToArray());
            check.Output(c, "notEqual", others.Select(o => (object?)(dc != o)).ToArray());

            // Equal day counters hash alike, and Equals agrees with ==.
            foreach (DayCounter o in others)
            {
                Assert.Equal(dc == o, dc.Equals(o));
                Assert.Equal(dc == o, dc.Equals((object)o));
                if (dc == o)
                {
                    Assert.Equal(dc.GetHashCode(), o.GetHashCode());
                }
            }
        }

        check.Complete();
    }

    private static DayCounter Build(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "Actual360" => new Actual360(spec.GetProperty("includeLastDay").GetBoolean()),
        "Actual365Fixed" => new Actual365Fixed((Actual365FixedConvention)spec.GetProperty("convention").GetInt32()),
        "Thirty360" => new Thirty360((Thirty360Convention)spec.GetProperty("convention").GetInt32()),
        "ActualActual" => spec.TryGetProperty("schedule", out JsonElement s) && s.GetBoolean()
            ? new ActualActual((ActualActualConvention)spec.GetProperty("convention").GetInt32(), Schedule())
            : new ActualActual((ActualActualConvention)spec.GetProperty("convention").GetInt32()),
        var type => throw new ArgumentException($"unknown day counter {type}", nameof(spec)),
    };

    private static Schedule Schedule() => new(
        new Date(15, Month.February, 2020),
        new Date(15, Month.August, 2030),
        new Period(6, TimeUnit.Months),
        new NullCalendar(),
        BusinessDayConvention.Unadjusted,
        BusinessDayConvention.Unadjusted,
        DateGenerationRule.Backward,
        false,
        Context);
}
