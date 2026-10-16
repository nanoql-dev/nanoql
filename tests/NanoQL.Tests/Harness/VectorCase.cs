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

    /// <summary>The evaluation context the case's generator set, from <c>inputs.settings</c>.</summary>
    /// <returns>The context.</returns>
    public EvaluationContext Context()
    {
        JsonElement s = Input("settings");
        JsonElement todays = s.GetProperty("includeTodaysCashFlows");
        return new EvaluationContext
        {
            EvaluationDate = new NanoQL.Time.Date(s.GetProperty("evaluationDate").GetInt32()),
            IncludeReferenceDateEvents = s.GetProperty("includeReferenceDateEvents").GetBoolean(),
            IncludeTodaysCashFlows = todays.ValueKind == JsonValueKind.Null ? null : todays.GetBoolean(),
            EnforcesTodaysHistoricFixings = s.GetProperty("enforcesTodaysHistoricFixings").GetBoolean(),
        };
    }

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
