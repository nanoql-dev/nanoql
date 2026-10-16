// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NanoQL.Tests.Harness;

/// <summary>
/// Compares nanoql's outputs with one vector file's, collecting every mismatch (docs/porting-playbook.md §7.2).
/// Booleans, strings and integers compare exactly; reals within <see cref="Tolerance"/>; <c>null</c> matches only
/// <c>null</c>; an expected <c>{"throws": true}</c> needs an <see cref="ArgumentException"/> or a
/// <see cref="NanoQLException"/>. <see cref="Complete"/> fails on any mismatch, and unless every output the file holds
/// was compared once.
/// </summary>
public sealed class VectorAssert
{
    private const int MaxReported = 50;

    private static readonly JsonSerializerOptions Reals = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly VectorFile file;
    private readonly HashSet<(int Case, string Output)> compared = [];
    private readonly List<string> mismatches = [];

    /// <summary>Initializes a new instance of the <see cref="VectorAssert"/> class for one file.</summary>
    /// <param name="file">The vector file.</param>
    public VectorAssert(VectorFile file)
    {
        this.file = file;
    }

    /// <summary>Gets the number of outputs compared so far.</summary>
    public int Compared { get; private set; }

    /// <summary>Gets the number of outputs where an expected throw was mapped to a documented value.</summary>
    public int MappedOutputs { get; private set; }

    /// <summary>Gets the mismatches found so far.</summary>
    public IReadOnlyList<string> Mismatches => mismatches;

    /// <summary>
    /// Compares one output of a case. The actual value is a <see cref="bool"/>, a <see cref="string"/>, an integer or
    /// an enum (compared as its integer), a real, <c>null</c>, a sequence of these (for an expected array), or a
    /// <see cref="Func{TResult}"/> producing one, which lets an expected throw be checked.
    /// </summary>
    /// <param name="vectorCase">The case.</param>
    /// <param name="output">The output's name in the case's <c>expected</c>.</param>
    /// <param name="actual">nanoql's value.</param>
    public void Output(VectorCase vectorCase, string output, object? actual) => Start(vectorCase, output, actual, null);

    /// <summary>Compares one output computed lazily, so that a throw is caught and checked.</summary>
    /// <param name="vectorCase">The case.</param>
    /// <param name="output">The output's name.</param>
    /// <param name="actual">Computes nanoql's value; <see langword="null"/> compares as a null value.</param>
    public void Output(VectorCase vectorCase, string output, Func<object?>? actual) =>
        Start(vectorCase, output, actual, null);

    /// <summary>
    /// Compares one output where a documented divergence makes nanoql return a value where QuantLib throws: an
    /// expected <c>{"throws": true}</c> is compared with <paramref name="whenThrows"/> instead.
    /// </summary>
    /// <param name="vectorCase">The case.</param>
    /// <param name="output">The output's name.</param>
    /// <param name="actual">nanoql's value, or a sequence of them, or a function computing it.</param>
    /// <param name="whenThrows">The value nanoql returns where QuantLib throws.</param>
    /// <param name="divergence">The documented divergence, for messages.</param>
    public void Mapped(VectorCase vectorCase, string output, object? actual, object? whenThrows, string divergence) =>
        Start(vectorCase, output, actual, new Mapping(whenThrows, divergence));

    /// <summary>Compares one lazily computed output where a documented divergence returns a value where QuantLib throws.</summary>
    /// <param name="vectorCase">The case.</param>
    /// <param name="output">The output's name.</param>
    /// <param name="actual">Computes nanoql's value.</param>
    /// <param name="whenThrows">The value nanoql returns where QuantLib throws.</param>
    /// <param name="divergence">The documented divergence, for messages.</param>
    public void Mapped(VectorCase vectorCase, string output, Func<object?> actual, object? whenThrows, string divergence) =>
        Start(vectorCase, output, actual, new Mapping(whenThrows, divergence));

    /// <summary>
    /// Fails, listing the first 50 mismatches and their total, if any output mismatched, an output in the file was
    /// not compared, or the outputs compared differ from the outputs the file holds.
    /// </summary>
    public void Complete()
    {
        foreach (VectorCase c in file.Cases)
        {
            foreach (JsonProperty output in c.Expected.EnumerateObject())
            {
                if (!compared.Contains((c.Index, output.Name)))
                {
                    mismatches.Add($"{c.Name} › {output.Name}: not compared");
                }
            }
        }

        if (Compared != file.Outputs)
        {
            mismatches.Add($"{file.Path}: compared {Compared} outputs of the {file.Outputs} the file holds");
        }

        if (mismatches.Count > 0)
        {
            Assert.Fail(
                $"{file.Path}: {mismatches.Count} mismatches (outputs compared {Compared}/{file.Outputs})"
                + Environment.NewLine
                + string.Join(Environment.NewLine, mismatches.Take(MaxReported)));
        }
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name,
    };

    private static bool IsInteger(object value) =>
        value is int or long or short or byte or sbyte or ushort or uint or ulong;

    private static bool IsAccepted(Exception e) => e is ArgumentException or NanoQLException;

    private static bool IsThrows(JsonElement expected) =>
        expected.ValueKind == JsonValueKind.Object && expected.TryGetProperty("throws", out _);

    private void Start(VectorCase vectorCase, string output, object? actual, Mapping? mapping)
    {
        if (!vectorCase.Expected.TryGetProperty(output, out JsonElement expected))
        {
            mismatches.Add($"{vectorCase.Name} › {output}: the case has no such output");
            return;
        }

        if (!compared.Add((vectorCase.Index, output)))
        {
            mismatches.Add($"{vectorCase.Name} › {output}: compared twice");
            return;
        }

        Compare($"{vectorCase.Name} › {output}", expected, actual, mapping);
    }

    private void Compare(string label, JsonElement expected, object? actual, Mapping? mapping)
    {
        if (IsThrows(expected))
        {
            Compared++;
            CompareThrow(label, actual, mapping);
            return;
        }

        if (actual is Func<object?> lazy)
        {
            try
            {
                actual = lazy();
            }
            catch (Exception e)
            {
                int outputs = expected.ValueKind == JsonValueKind.Array ? CountOutputs(expected) : 1;
                Compared += outputs;
                mismatches.Add($"{label}: threw {e.GetType().Name} ({e.Message}), expected {expected}");
                return;
            }
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            CompareArray(label, expected, actual, mapping);
            return;
        }

        Compared++;
        CompareScalar(label, expected, actual);
    }

    private void CompareThrow(string label, object? actual, Mapping? mapping)
    {
        object? value;
        try
        {
            value = actual is Func<object?> lazy ? lazy() : actual;
        }
        catch (Exception e) when (mapping is null && IsAccepted(e))
        {
            return;
        }
        catch (Exception e)
        {
            string wanted = mapping is null ? "ArgumentException or NanoQLException" : $"{Show(mapping.Value)} ({mapping.Divergence})";
            mismatches.Add($"{label}: threw {e.GetType().Name} ({e.Message}), expected {wanted}");
            return;
        }

        if (mapping is null)
        {
            mismatches.Add($"{label}: returned {Show(value)}, expected a throw (ArgumentException or NanoQLException)");
            return;
        }

        MappedOutputs++;
        if (!Equals(value, mapping.Value))
        {
            mismatches.Add($"{label}: QuantLib throws; expected {Show(mapping.Value)} ({mapping.Divergence}), got {Show(value)}");
        }
    }

    private void CompareArray(string label, JsonElement expected, object? actual, Mapping? mapping)
    {
        if (actual is null or string || actual is not IEnumerable sequence)
        {
            Compared += CountOutputs(expected);
            mismatches.Add($"{label}: expected an array of {expected.GetArrayLength()}, got {Show(actual)}");
            return;
        }

        List<object?> items = sequence.Cast<object?>().ToList();
        if (items.Count != expected.GetArrayLength())
        {
            Compared += CountOutputs(expected);
            mismatches.Add($"{label}: expected {expected.GetArrayLength()} elements, got {items.Count}");
            return;
        }

        int i = 0;
        foreach (JsonElement e in expected.EnumerateArray())
        {
            Compare($"{label}[{i}]", e, items[i], mapping);
            i++;
        }
    }

    private void CompareScalar(string label, JsonElement expected, object? actual)
    {
        if (actual is Enum)
        {
            actual = Convert.ToInt64(actual, CultureInfo.InvariantCulture);
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Null:
                if (actual is not null)
                {
                    mismatches.Add($"{label}: expected null, got {Show(actual)}");
                }

                return;
            case JsonValueKind.True or JsonValueKind.False:
                if (actual is not bool b || b != expected.GetBoolean())
                {
                    mismatches.Add($"{label}: expected {expected}, got {Show(actual)}");
                }

                return;
            case JsonValueKind.String when actual is string s:
                if (s != expected.GetString())
                {
                    mismatches.Add($"{label}: expected \"{expected.GetString()}\", got {Show(s)}");
                }

                return;
            case JsonValueKind.Object when expected.TryGetProperty("value", out JsonElement value):
                double scale = expected.TryGetProperty("scale", out JsonElement scaleValue) ? scaleValue.GetDouble() : 1.0;
                double relative = expected.TryGetProperty("tolerance", out JsonElement toleranceValue)
                    ? toleranceValue.GetDouble()
                    : Tolerance.Relative;
                CompareReal(label, value, actual, scale, relative);
                return;
            case JsonValueKind.Number when actual is not null && IsInteger(actual):
                long a = Convert.ToInt64(actual, CultureInfo.InvariantCulture);
                if (!expected.TryGetInt64(out long e) || e != a)
                {
                    mismatches.Add($"{label}: expected {expected}, got {a}");
                }

                return;
            case JsonValueKind.Number or JsonValueKind.String:
                CompareReal(label, expected, actual, 1.0, Tolerance.Relative);
                return;
            default:
                mismatches.Add($"{label}: unsupported expected value {expected}");
                return;
        }
    }

    private void CompareReal(string label, JsonElement expected, object? actual, double scale, double relative)
    {
        double e;
        try
        {
            e = expected.Deserialize<double>(Reals);
        }
        catch (JsonException)
        {
            mismatches.Add($"{label}: expected {expected}, got {Show(actual)}");
            return;
        }

        if (actual is not (double or float))
        {
            mismatches.Add($"{label}: expected the real {expected}, got {Show(actual)}");
            return;
        }

        double a = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
        bool equal = double.IsNaN(e) || double.IsInfinity(e) || double.IsNaN(a) || double.IsInfinity(a)
            ? e.Equals(a)
            : Tolerance.Within(e, a, scale, relative);
        if (!equal)
        {
            mismatches.Add($"{label}: expected {Show(e)}, got {Show(a)} (scale {Show(scale)})");
        }
    }

    private int CountOutputs(JsonElement expected) =>
        expected.ValueKind == JsonValueKind.Array ? expected.EnumerateArray().Sum(CountOutputs) : 1;

    private sealed record Mapping(object? Value, string Divergence);
}
