// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ExchangeRateManager vectors, <c>tests/vectors/currencies/exchangeratemanager.json</c>, read through
/// <see cref="NanoQL.Currencies.ExchangeRates"/>: direct, inverse, triangulated and chained lookups on QuantLib's known
/// rates and on registries with dated entries, each lookup's rate and its exchange of 100 units of its source.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExchangeRateManagerVectorTests
{
    [Fact]
    public void ExchangeRateManagerVector()
    {
        var file = VectorFile.Load("currencies/exchangeratemanager");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement[] specs = [.. c.Input("lookups").EnumerateArray()];
            Func<ExchangeRate>[] lookups = [.. specs.Select(l => LookupOf(l, context))];
            check.Output(c, "sources", lookups.Select(f => (Func<object?>)(() => f().Source?.Code)));
            check.Output(c, "targets", lookups.Select(f => (Func<object?>)(() => f().Target?.Code)));
            check.Output(c, "rates", lookups.Select(f => (Func<object?>)(() => f().Rate)));
            check.Output(c, "types", lookups.Select(f => (Func<object?>)(() => f().Type)));

            // 100 units of the requested source, exchanged by the rate found
            check.Output(c, "exchanged", specs.Zip(lookups, (l, f) => (Func<object?>)(() =>
                f().Exchange(new Money(100.0, VectorCurrencies.ByCode(l.GetProperty("source").GetString()!))).Value)));
        }

        check.Complete();
    }

    private static Func<ExchangeRate> LookupOf(JsonElement l, EvaluationContext context)
    {
        Currency source = VectorCurrencies.ByCode(l.GetProperty("source").GetString()!);
        Currency target = VectorCurrencies.ByCode(l.GetProperty("target").GetString()!);
        JsonElement date = l.GetProperty("date");
        var type = (ExchangeRateType)l.GetProperty("type").GetInt32();
        return () => context.ExchangeRates.Lookup(
            source,
            target,
            context,
            date.ValueKind == JsonValueKind.Null ? null : new NanoQL.Time.Date(date.GetInt32()),
            type);
    }
}
