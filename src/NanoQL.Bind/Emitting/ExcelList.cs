// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// A list of the types Excel holds, by a desk's tasks (XLR5, XLD33): <c>{"version": 1, "types": [{"type": …, "task":
/// …}, …]}</c>, each entry a type's full name (a generic type by its definition, for its family). nanoql's is
/// <c>bindings/excel/list.json</c>, which the tool carries; a user's build may give its own (XLD35).
/// </summary>
/// <param name="Source">Where the list came from, for messages.</param>
/// <param name="Entries">The entries: each type's full name and its task.</param>
internal sealed record ExcelList(string Source, IReadOnlyList<(string Type, string Task)> Entries)
{
    /// <summary>Gets the listed types' full names.</summary>
    public IReadOnlySet<string> Types { get; } = Entries.Select(e => e.Type).ToHashSet(StringComparer.Ordinal);

    /// <summary>nanoql's list, carried by the tool.</summary>
    /// <returns>The list.</returns>
    public static ExcelList Nanoql()
    {
        using Stream s = typeof(ExcelList).Assembly.GetManifestResourceStream("NanoQL.Bind.excel-list.json")
            ?? throw new InvalidOperationException("nanoql's Excel list isn't embedded in the tool");
        using var reader = new StreamReader(s);
        return Parse(reader.ReadToEnd(), "nanoql's list (bindings/excel/list.json)");
    }

    /// <summary>Reads a list file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The list.</returns>
    public static ExcelList Read(string path) => Parse(File.ReadAllText(path), path);

    /// <summary>Reads a list's text, refusing a malformed one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="source">Where it came from.</param>
    /// <returns>The list.</returns>
    public static ExcelList Parse(string text, string source)
    {
        using JsonDocument doc = JsonDocument.Parse(text);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out JsonElement v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != 1)
        {
            throw new BindException($"{source}: an Excel list is {{\"version\": 1, \"types\": [...]}}");
        }

        var entries = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement e in root.GetProperty("types").EnumerateArray())
        {
            string? type = e.TryGetProperty("type", out JsonElement t) ? t.GetString() : null;
            string? task = e.TryGetProperty("task", out JsonElement k) ? k.GetString() : null;
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(task))
            {
                throw new BindException($"{source}: each entry names a type and its task");
            }

            if (!seen.Add(type))
            {
                throw new BindException($"{source}: {type} is listed twice");
            }

            entries.Add((type, task));
        }

        return new ExcelList(source, entries);
    }
}
