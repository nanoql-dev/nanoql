// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NanoQL.Tests.Harness;

/// <summary>One case of a vector file: its name, its inputs and its expected outputs.</summary>
public sealed class VectorCase
{
    internal VectorCase(int index, JsonElement element)
    {
        Index = index;
        Name = element.GetProperty("name").GetString()!;
        Inputs = element.GetProperty("inputs");
        Expected = element.GetProperty("expected");
    }

    /// <summary>Gets the case's position in its file.</summary>
    public int Index { get; }

    /// <summary>Gets the case's name.</summary>
    public string Name { get; }

    /// <summary>Gets the case's inputs, including <c>settings</c>.</summary>
    public JsonElement Inputs { get; }

    /// <summary>Gets the case's expected outputs, by name.</summary>
    public JsonElement Expected { get; }

    /// <summary>Gets an input by name.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The input's JSON value.</returns>
    public JsonElement Input(string name) =>
        Inputs.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new KeyNotFoundException($"case '{Name}' has no input '{name}'");

    /// <summary>Gets an integer input.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The value.</returns>
    public int Int(string name) => Input(name).GetInt32();

    /// <summary>Gets an integer input that may be null.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public int? NullableInt(string name)
    {
        JsonElement e = Input(name);
        return e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();
    }

    /// <summary>Gets a real input.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The value.</returns>
    public double Double(string name) => Input(name).GetDouble();

    /// <summary>Gets a Boolean input.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The value.</returns>
    public bool Bool(string name) => Input(name).GetBoolean();

    /// <summary>Gets a Boolean input that may be null.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public bool? NullableBool(string name)
    {
        JsonElement e = Input(name);
        return e.ValueKind == JsonValueKind.Null ? null : e.GetBoolean();
    }

    /// <summary>Gets a string input.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The value.</returns>
    public string String(string name) => Input(name).GetString()!;

    /// <summary>
    /// The evaluation context the case's generator set, from <c>inputs.settings</c>: the four settings, and, when the
    /// case wrote them, the at-par switch (<c>useAtParCoupons</c>), the stored fixings (<c>fixings</c>, as
    /// <c>{name: [[serial, value], ...]}</c>), each history stored under its name through a
    /// <see cref="VectorFixingIndex"/>, the exchange rates added after QuantLib's known ones (<c>exchangeRates</c>, as
    /// <c>[{source, target, rate, startDate, endDate}, ...]</c>, the currencies by code and a null date QuantLib's
    /// default), and the money settings (<c>moneySettings</c>, as <c>{conversionType, baseCurrency}</c>). A value is
    /// encoded as the vectors encode reals: "NaN", "Infinity" and "-Infinity" as strings, and QuantLib's
    /// <c>Null&lt;Real&gt;()</c> as <c>null</c>, which QuantLib reads as no fixing, so that date stores none.
    /// </summary>
    /// <returns>The context.</returns>
    public EvaluationContext Context()
    {
        JsonElement s = Input("settings");
        JsonElement todays = s.GetProperty("includeTodaysCashFlows");
        var context = new EvaluationContext
        {
            EvaluationDate = new NanoQL.Time.Date(s.GetProperty("evaluationDate").GetInt32()),
            IncludeReferenceDateEvents = s.GetProperty("includeReferenceDateEvents").GetBoolean(),
            IncludeTodaysCashFlows = todays.ValueKind == JsonValueKind.Null ? null : todays.GetBoolean(),
            EnforcesTodaysHistoricFixings = s.GetProperty("enforcesTodaysHistoricFixings").GetBoolean(),
        };
        if (s.TryGetProperty("useAtParCoupons", out JsonElement atPar))
        {
            context = context with { UseAtParCoupons = atPar.GetBoolean() };
        }

        if (s.TryGetProperty("fixings", out JsonElement fixings))
        {
            NanoQL.Indexes.IndexFixings store = NanoQL.Indexes.IndexFixings.Empty;
            foreach (JsonProperty history in fixings.EnumerateObject())
            {
                JsonElement[] pairs = [.. history.Value.EnumerateArray().Where(p => p[1].ValueKind != JsonValueKind.Null)];
                store = store.AddFixings(
                    new VectorFixingIndex(history.Name),
                    pairs.Select(p => new NanoQL.Time.Date(p[0].GetInt32())),
                    pairs.Select(p => Real(p[1])));
            }

            context = context with { Fixings = store };
        }

        if (s.TryGetProperty("exchangeRates", out JsonElement rates))
        {
            NanoQL.Currencies.ExchangeRates registry = NanoQL.Currencies.ExchangeRates.Default;
            foreach (JsonElement r in rates.EnumerateArray())
            {
                var rate = new ExchangeRate(
                    VectorCurrencies.ByCode(r.GetProperty("source").GetString()!),
                    VectorCurrencies.ByCode(r.GetProperty("target").GetString()!),
                    Real(r.GetProperty("rate")));
                registry = registry.Add(rate, NullableDate(r.GetProperty("startDate")), NullableDate(r.GetProperty("endDate")));
            }

            context = context with { ExchangeRates = registry };
        }

        if (s.TryGetProperty("moneySettings", out JsonElement money))
        {
            JsonElement baseCurrency = money.GetProperty("baseCurrency");
            context = context with
            {
                MoneySettings = new MoneySettings
                {
                    ConversionType = (MoneyConversionType)money.GetProperty("conversionType").GetInt32(),
                    BaseCurrency = baseCurrency.ValueKind == JsonValueKind.Null
                        ? null
                        : VectorCurrencies.ByCode(baseCurrency.GetString()!),
                },
            };
        }

        return context;
    }

    /// <summary>A date written as its serial number, or <see langword="null"/>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The date, or <see langword="null"/>.</returns>
    private static NanoQL.Time.Date? NullableDate(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : new NanoQL.Time.Date(e.GetInt32());

    /// <summary>A real as the vectors encode it: a number, or "NaN", "Infinity" or "-Infinity".</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The real.</returns>
    internal static double Real(JsonElement e) =>
        e.ValueKind == JsonValueKind.String
            ? double.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : e.GetDouble();

    /// <summary>Gets a date input, written as its serial number.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The date.</returns>
    public NanoQL.Time.Date DateInput(string name) => new(Int(name));

    /// <summary>Gets a date input that may be null.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The date, or <see langword="null"/>.</returns>
    public NanoQL.Time.Date? NullableDateInput(string name) => NullableInt(name) is int serial ? new NanoQL.Time.Date(serial) : null;

    /// <summary>Gets an array of dates, written as serial numbers.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The dates.</returns>
    public NanoQL.Time.Date[] DateInputs(string name) => Ints(name).Select(s => new NanoQL.Time.Date(s)).ToArray();

    /// <summary>Gets an array of integers.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The values.</returns>
    public int[] Ints(string name) => Input(name).EnumerateArray().Select(e => e.GetInt32()).ToArray();

    /// <summary>Gets an array of reals.</summary>
    /// <param name="name">The input's name.</param>
    /// <returns>The values.</returns>
    public double[] Doubles(string name) => Input(name).EnumerateArray().Select(e => e.GetDouble()).ToArray();

    /// <inheritdoc/>
    public override string ToString() => Name;
}
