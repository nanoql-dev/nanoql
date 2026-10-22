// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The year-fraction-to-date vectors, <c>tests/vectors/time/daycounters/yearfractiontodate.json</c>: the date a time
/// reaches from a reference date, for QuantLib's bulk case's 28 day counters and its rounding case's two.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YearFractionToDateVectorTests
{
    [Fact]
    public void YearFractionToDate()
    {
        var file = VectorFile.Load("time/daycounters/yearfractiontodate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            DayCounter dc = Build(c.Input("dayCounter"));
            Date[] references = c.DateInputs("referenceDates");
            double[] times = c.Doubles("times");
            check.Output(
                c,
                "dates",
                references.Select((r, i) => (Func<object?>)(() => YearFractionToDateFunctions.YearFractionToDate(dc, r, times[i])))
                    .ToArray());
        }

        check.Complete();
    }

    private static DayCounter Build(JsonElement spec)
    {
        bool IncludeLastDay() => spec.GetProperty("includeLastDay").GetBoolean();
        int Convention() => spec.GetProperty("convention").GetInt32();
        return spec.GetProperty("type").GetString() switch
        {
            "Actual365Fixed" => new Actual365Fixed((Actual365FixedConvention)Convention()),
            "Actual360" => new Actual360(IncludeLastDay()),
            "Actual36525" => new Actual36525(IncludeLastDay()),
            "Actual366" => new Actual366(IncludeLastDay()),
            "Actual364" => new Actual364(),
            "ActualActual" => new ActualActual((ActualActualConvention)Convention()),
            "Business252" => new Business252(),
            "Thirty360" => new Thirty360((Thirty360Convention)Convention()),
            "Thirty365" => new Thirty365(),
            "SimpleDayCounter" => new SimpleDayCounter(),
            var type => throw new ArgumentException($"unknown day counter {type}", nameof(spec)),
        };
    }
}
