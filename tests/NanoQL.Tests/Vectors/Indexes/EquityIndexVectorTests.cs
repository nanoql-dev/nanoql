// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The equity index's vectors, <c>tests/vectors/indexes/equityindex.json</c>, one case per set-up of curves, spot and
/// stored fixings: the name, the valid fixing dates, today's fixing and forecast, and fixings past and forecast.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EquityIndexVectorTests
{
    [Fact]
    public void EquityIndexVectors()
    {
        var file = VectorFile.Load("indexes/equityindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spot = c.Input("spot");
            var index = new EquityIndex(
                "eqIndex",
                new TARGET(),
                new EURCurrency(),
                Curve(c.Input("interest")),
                Curve(c.Input("dividend")),
                spot.ValueKind == JsonValueKind.Null ? null : spot.GetDouble());
            Date today = context.EvaluationDate;
            check.Output(c, "name", index.Name);
            check.Output(c, "isValidFixingDate", c.DateInputs("validDates").Select(d => (object?)index.IsValidFixingDate(d)).ToArray());
            check.Output(c, "todaysFixing", () => index.Fixing(today, context));
            if (c.Expected.TryGetProperty("todaysForecast", out _))
            {
                check.Output(c, "todaysForecast", () => index.Fixing(today, context, true));
            }

            check.Output(c, "fixing", c.DateInputs("dates").Select(d => (Func<object?>)(() => index.Fixing(d, context))).ToArray());
        }

        check.Complete();
    }

    private static YieldTermStructure? Curve(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new FlatForward(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                spec.GetProperty("rate").GetDouble(),
                new Actual365Fixed());
}
