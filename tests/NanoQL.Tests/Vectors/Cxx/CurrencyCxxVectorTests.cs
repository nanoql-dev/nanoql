// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's currency vectors, <c>tests/vectors/cxx/currency.json</c>: what the wheel doesn't expose of
/// <see cref="Currency"/>, the nine-argument constructor with its minor unit codes, which
/// <see cref="Currency.MinorUnitCodes"/> holds sorted and without duplicates, and QuantLib's <c>operator&lt;&lt;</c>,
/// the code, as <see cref="Currency.ToString"/> gives it and as the internal <c>Currency.Format</c> writes a currency
/// into messages, "null currency" for QuantLib's empty currency, a null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CurrencyCxxVectorTests
{
    private static readonly MethodInfo Format =
        typeof(Currency).GetMethod("Format", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void CurrencyCxxVector()
    {
        var file = VectorFile.Load("cxx/currency");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Currency? currency = CurrencyOf(c.Input("currency"));
            if (currency is null)
            {
                check.Output(c, "format", () => Format.Invoke(null, [null]));
                continue;
            }

            check.Output(c, "name", currency.Name);
            check.Output(c, "code", currency.Code);
            check.Output(c, "numericCode", currency.NumericCode);
            check.Output(c, "symbol", currency.Symbol);
            check.Output(c, "fractionSymbol", currency.FractionSymbol);
            check.Output(c, "fractionsPerUnit", currency.FractionsPerUnit);
            check.Output(c, "triangulationCurrency", currency.TriangulationCurrency?.Code);
            check.Output(c, "minorUnitCodes", currency.MinorUnitCodes);
            check.Output(c, "rounding", c.Doubles("rounded").Select(x => (object?)currency.Rounding.Evaluate(x)));
            check.Output(c, "toString", currency.ToString());
            check.Output(c, "format", () => Format.Invoke(null, [currency]));
            check.Output(c, "empty", currency is null);
        }

        check.Complete();
    }

    // The harness's currency spec: null for QuantLib's empty currency.
    private static Currency? CurrencyOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new Currency(
                spec.GetProperty("name").GetString()!,
                spec.GetProperty("code").GetString()!,
                spec.GetProperty("numericCode").GetInt32(),
                spec.GetProperty("symbol").GetString()!,
                spec.GetProperty("fractionSymbol").GetString()!,
                spec.GetProperty("fractionsPerUnit").GetInt32(),
                CurrencyVectorTests.RoundingOf(spec.GetProperty("rounding")),
                CurrencyOf(spec.GetProperty("triangulationCurrency")),
                spec.GetProperty("minorUnitCodes").EnumerateArray().Select(e => e.GetString()!));
}
