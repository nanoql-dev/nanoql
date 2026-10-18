// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The currency vectors, <c>tests/vectors/currency.json</c>: 20 bespoke <see cref="Currency"/> instances built from
/// eight arguments, each giving its accessors, its rounding applied to five values, its triangulation currency, and
/// its <c>==</c> and <c>!=</c> against each of the others and against QuantLib's empty currency, a null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CurrencyVectorTests
{
    [Fact]
    public void CurrencyVector()
    {
        var file = VectorFile.Load("currency");
        var check = new VectorAssert(file);
        var built = new List<Currency>();
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("currency");
            JsonElement tri = s.GetProperty("triangulationCurrency");
            built.Add(new Currency(
                s.GetProperty("name").GetString()!,
                s.GetProperty("code").GetString()!,
                s.GetProperty("numericCode").GetInt32(),
                s.GetProperty("symbol").GetString()!,
                s.GetProperty("fractionSymbol").GetString()!,
                s.GetProperty("fractionsPerUnit").GetInt32(),
                RoundingOf(s.GetProperty("rounding")),
                tri.ValueKind == JsonValueKind.Null ? null : built[tri.GetInt32()]));
        }

        Currency? empty = null;
        foreach (VectorCase c in file.Cases)
        {
            Currency currency = built[c.Index];
            check.Output(c, "name", currency.Name);
            check.Output(c, "code", currency.Code);
            check.Output(c, "numericCode", currency.NumericCode);
            check.Output(c, "symbol", currency.Symbol);
            check.Output(c, "fractionSymbol", currency.FractionSymbol);
            check.Output(c, "fractionsPerUnit", currency.FractionsPerUnit);
            check.Output(c, "rounding", c.Doubles("rounded").Select(x => (object?)currency.Rounding.Evaluate(x)));
            check.Output(c, "triangulationCurrencyEmpty", currency.TriangulationCurrency is null);
            check.Output(c, "triangulationCurrency", currency.TriangulationCurrency?.Code);
            check.Output(c, "empty", currency is null);
            check.Output(c, "equals", built.Select(o => (object?)(currency == o)));
            check.Output(c, "notEquals", built.Select(o => (object?)(currency != o)));
            check.Output(c, "equalsEmpty", currency == empty);
            check.Output(c, "notEqualsEmpty", currency != empty);
        }

        check.Complete();
    }

    /// <summary>
    /// The rounding a vector's spec describes: <see cref="Rounding()"/> for null, and otherwise the class of the
    /// spec's type, with its precision and digit, as the wheel's generator builds it; a type of
    /// <see cref="RoundingType.None"/> with a precision, which only the C++ harness builds, through
    /// <see cref="Rounding(int, RoundingType, int)"/>.
    /// </summary>
    /// <param name="spec">The spec, null or <c>{"precision", "type", "digit"}</c>.</param>
    /// <returns>The rounding.</returns>
    internal static Rounding RoundingOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return new Rounding();
        }

        int precision = spec.GetProperty("precision").GetInt32();
        int digit = spec.GetProperty("digit").GetInt32();
        return (RoundingType)spec.GetProperty("type").GetInt32() switch
        {
            RoundingType.Up => new UpRounding(precision, digit),
            RoundingType.Down => new DownRounding(precision, digit),
            RoundingType.Closest => new ClosestRounding(precision, digit),
            RoundingType.Floor => new FloorTruncation(precision, digit),
            RoundingType.Ceiling => new CeilingTruncation(precision, digit),
            RoundingType type => new Rounding(precision, type, digit),
        };
    }
}
