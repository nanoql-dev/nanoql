// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ExchangeRate vectors, <c>tests/vectors/exchangerate.json</c>: direct rates, chains of two rates in each of
/// QuantLib's four configurations and of a chain, a refused chain, and each rate's exchange of amounts in the
/// currencies it involves and one it doesn't.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExchangeRateVectorTests
{
    [Fact]
    public void ExchangeRateVector()
    {
        var file = VectorFile.Load("exchangerate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ExchangeRate[] rates = [.. c.Input("rates").EnumerateArray().Select(RateOf)];
            JsonElement chain = c.Input("chain");
            ExchangeRate Rate() => Build(chain, rates);
            check.Output(c, "source", () => Rate().Source?.Code);
            check.Output(c, "target", () => Rate().Target?.Code);
            check.Output(c, "rate", () => Rate().Rate);
            check.Output(c, "type", () => Rate().Type);
            Money[] amounts = [.. c.Input("amounts").EnumerateArray().Select(MoneyVectorTests.MoneyOf)];
            check.Output(c, "exchangeCurrencies", amounts.Select(m => (Func<object?>)(() => Rate().Exchange(m).Currency?.Code)));
            check.Output(c, "exchangeValues", amounts.Select(m => (Func<object?>)(() => Rate().Exchange(m).Value)));
        }

        check.Complete();
    }

    /// <summary>A direct rate as the vectors write it: <c>{source, target, rate}</c>, the currencies by code.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The rate.</returns>
    internal static ExchangeRate RateOf(JsonElement e) =>
        new(
            VectorCurrencies.ByCode(e.GetProperty("source").GetString()!),
            VectorCurrencies.ByCode(e.GetProperty("target").GetString()!),
            e.GetProperty("rate").GetDouble());

    // null: the one direct rate; [i, j]: chain(rates[i], rates[j]); [[...], j]: chain of a chain.
    private static ExchangeRate Build(JsonElement spec, ExchangeRate[] rates)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return rates[0];
        }

        JsonElement first = spec[0];
        ExchangeRate left = first.ValueKind == JsonValueKind.Number ? rates[first.GetInt32()] : Build(first, rates);
        return ExchangeRate.Chain(left, rates[spec[1].GetInt32()]);
    }
}
