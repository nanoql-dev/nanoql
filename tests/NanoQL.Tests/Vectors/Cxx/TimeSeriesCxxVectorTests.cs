// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's time-series vectors, <c>tests/vectors/cxx/timeseries.json</c>: what the wheel doesn't expose of
/// <see cref="TimeSeries{T}"/>, the constructor from a first date, iteration in date order and the lookup that leaves
/// the series unchanged, <see cref="TimeSeries{T}.TryGetValue"/>, over first-date series (one whose last value falls
/// on the last valid date refused, as QuantLib refuses it) and date-list series with duplicate dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeSeriesCxxVectorTests
{
    [Fact]
    public void TimeSeriesCxxVector()
    {
        var file = VectorFile.Load("cxx/timeseries");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("series");
            double[] values = [.. spec.GetProperty("values").EnumerateArray().Select(e => e.GetDouble())];
            Func<TimeSeries<double>> build = spec.TryGetProperty("firstDate", out JsonElement first)
                ? () => new TimeSeries<double>(new Date(first.GetInt32()), values)
                : () => new TimeSeries<double>(
                    spec.GetProperty("dates").EnumerateArray().Select(e => new Date(e.GetInt32())), values);
            check.Output(c, "iteration", () => build().Select(kv => new object?[] { kv.Key, kv.Value }).ToList());
            Date[] lookups = c.DateInputs("lookups");
            if (lookups.Length == 0)
            {
                check.Output(c, "lookup", Array.Empty<object?>());
                continue;
            }

            TimeSeries<double> ts = build();
            check.Output(c, "lookup", lookups.Select(d => ts.TryGetValue(d, out double x) ? (object?)x : null));
        }

        check.Complete();
    }
}
