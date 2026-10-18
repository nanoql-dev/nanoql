// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The time-series vectors, <c>tests/vectors/timeseries.json</c>: 20 <see cref="TimeSeries{T}"/> of reals built from
/// lists of 0 to 50 dates and their values, unsorted, with duplicate dates and surplus values, each giving its size,
/// emptiness, dates, values, first and last dates, and its value at each stored date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeSeriesVectorTests
{
    [Fact]
    public void TimeSeriesVector()
    {
        var file = VectorFile.Load("timeseries");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var ts = new TimeSeries<double>(c.DateInputs("dates"), c.Doubles("values"));
            check.Output(c, "size", ts.Size);
            check.Output(c, "empty", ts.Empty);
            check.Output(c, "dates", ts.Dates().Select(d => (object?)d));
            check.Output(c, "values", ts.Values());
            check.Output(c, "firstDate", () => ts.FirstDate());
            check.Output(c, "lastDate", () => ts.LastDate());
            check.Output(c, "valueAt", ts.Dates().Select(d => ts.TryGetValue(d, out double x) ? (object?)x : null));
        }

        check.Complete();
    }
}
