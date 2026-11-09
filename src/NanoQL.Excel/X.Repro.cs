// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// Repro export: the build's own part, which the generated assembly hands over when it loads (its resources, the repro
/// templates and the adapters' source, and the path reporters' Excel names), and <c>nqlxlRepro</c>'s formats.
/// </summary>
public static partial class X
{
    // The characters a cell holds, each export line in a cell of its own.
    private const int CellCharacters = 32767;

    // A vector's call line's function.
    private static readonly Regex ReproFunction = new("\"fn\":\"([A-Za-z0-9_]+)\"", RegexOptions.CultureInvariant);

    private static Assembly? build;

    /// <summary>Gets the Excel names of the members reporting a read file's path, which an export refuses.</summary>
    internal static IReadOnlySet<string> PathReporters { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets nanoql's version, as nqlxlVersion gives it and a vector's generator names it: the build's informational
    /// version, the release's own (2.3.1), without the commit after its "+". Every assembly's version stays 2.0.0.0
    /// within 2.y, so it can't tell one release from another.
    /// </summary>
    internal static string VersionText { get; } = Informational(typeof(X).Assembly);

    // An assembly's informational version without its "+" suffix; its version's major and minor when it has none.
    private static string Informational(Assembly a)
    {
        string? v = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(v))
        {
            Version n = a.GetName().Version!;
            return string.Create(CultureInfo.InvariantCulture, $"{n.Major}.{n.Minor}");
        }

        int plus = v.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? v : v[..plus];
    }

    /// <summary>Takes the generated build's assembly and its path reporters, from the build's module initializer.</summary>
    /// <param name="assembly">The generated assembly, whose resources hold the repro templates and the adapters' source.</param>
    /// <param name="pathReporters">The Excel names of the members reporting a read file's path.</param>
    public static void UseBuild(Assembly assembly, string[] pathReporters)
    {
        build = assembly;
        PathReporters = new HashSet<string>(pathReporters, StringComparer.Ordinal);
    }

    /// <summary>A cell's calls in a format: the vector, or the C# or Python that runs them.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="value">Its value as Excel shows it.</param>
    /// <param name="format"><c>"vector"</c>, <c>"csharp"</c> or <c>"python"</c>.</param>
    /// <returns>The lines.</returns>
    internal static List<string> Repro(ExcelReference cell, object value, string format)
    {
        List<string> vector = XlVectorWriter.Of(cell, value);
        List<string> lines = format switch
        {
            "vector" => vector,
            "csharp" => XlCSharp.Render(string.Join("\n", vector), Resource("NanoQL.Excel.templates.json"), Resource("NanoQL.Excel.adapters.cs")),
            "python" => XlPython.Render(string.Join("\n", vector), Resource("NanoQL.Excel.templates.json")),
            _ => throw new ArgumentException($"format: \"vector\", \"csharp\" or \"python\", not \"{format}\"", nameof(format)),
        };

        // A line goes to a cell, which holds 32,767 characters: a longer one would be cut, so the export is refused.
        int at = lines.FindIndex(l => l.Length > CellCharacters);
        if (at >= 0)
        {
            Match fn = ReproFunction.Match(lines[at]);
            string what = fn.Success ? $"the call to {fn.Groups[1].Value}"
                : lines[at].TrimStart().StartsWith("\"files\"", StringComparison.Ordinal) ? "the read files' text"
                : lines[at].TrimStart().StartsWith("\"conventions\"", StringComparison.Ordinal) ? "the conventions file's text"
                : lines[at].TrimStart().StartsWith("\"expected\"", StringComparison.Ordinal) ? "the cell's outcome"
                : "a literal in the program";
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"line {at + 1} of the {format} holds {lines[at].Length:N0} characters, past the {CellCharacters:N0} a cell holds: {what}"));
        }

        return lines;
    }

    /// <summary>A vector's literal as the Excel value its cell or range held, which the renderers convert as the add-in does.</summary>
    /// <param name="v">The literal.</param>
    /// <returns>A number, a boolean, text, an empty cell, an Excel error, a missing argument, or a range of them.</returns>
    internal static object Cell(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => v.GetString()!,
        JsonValueKind.Null or JsonValueKind.Undefined => ExcelMissing.Value,
        JsonValueKind.Object when v.TryGetProperty("empty", out _) => ExcelEmpty.Value,
        JsonValueKind.Object when v.TryGetProperty("error", out JsonElement e) => (ExcelError)e.GetInt32(),
        JsonValueKind.Object when v.TryGetProperty("rows", out JsonElement rows) => Range(v, rows.GetInt32(), v.GetProperty("cols").GetInt32()),
        _ => throw new XlRefusal("a value no cell holds: " + v.GetRawText()),
    };

    // A range's literal by rows, each cell as its Excel value.
    private static object[,] Range(JsonElement v, int rows, int cols)
    {
        var o = new object[rows, cols];
        int k = 0;
        foreach (JsonElement c in v.GetProperty("cells").EnumerateArray())
        {
            o[k / cols, k % cols] = Cell(c);
            k++;
        }

        return o;
    }

    /// <summary>A literal converted by one of the add-in's own conversions, its refusal the renderer's.</summary>
    /// <typeparam name="T">The converted value.</typeparam>
    /// <param name="v">The literal.</param>
    /// <param name="kind">The parameter's kind, which names it in the refusal.</param>
    /// <param name="convert">The add-in's conversion.</param>
    /// <returns>The value.</returns>
    internal static T Converted<T>(JsonElement v, string kind, Func<object, string, T> convert)
    {
        try
        {
            return convert(Cell(v), kind);
        }
        catch (ArgumentException e) when (e is not XlRefusal)
        {
            throw new XlRefusal(e.ParamName is { } p ? e.Message.Replace($" (Parameter '{p}')", string.Empty, StringComparison.Ordinal) : e.Message);
        }
    }

    /// <summary>A batch of draws' count as the add-in takes it: a whole number, never a negative one.</summary>
    /// <param name="v">The literal, or none for a missing count.</param>
    /// <returns>The count.</returns>
    internal static int DrawsCount(JsonElement? v)
    {
        int n = Converted(v ?? default, "count", Int);
        return n >= 0 ? n : throw new XlRefusal($"count: expected a count of draws, got {n.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>An enum member's name as the add-in takes it: text starting with a letter, never a number's text.</summary>
    /// <param name="v">The literal.</param>
    /// <param name="kind">The parameter's kind.</param>
    /// <returns>The name, which the program parses as the add-in does.</returns>
    internal static string EnumName(JsonElement v, string kind)
    {
        string s = Converted(v, kind, Str);
        return s.Length > 0 && char.IsLetter(s[0]) ? s : throw new XlRefusal($"{kind}: expected an enum member's name, got \"{s}\"");
    }

    /// <summary>A refusal of the add-in's own: its message kept by the calling cell for nqlxlError, #VALUE! returned
    /// (error code 2).</summary>
    /// <param name="message">The message.</param>
    /// <returns>#VALUE!.</returns>
    internal static ExcelError Refused(string message)
    {
        if (CallerKey() is { } caller)
        {
            Errors[caller] = message;
        }

        return ExcelError.ExcelErrorValue;
    }

    private static string Resource(string name)
    {
        Assembly a = build ?? throw new InvalidOperationException("no build has loaded: the add-in's generated assembly hands over its repro templates when it loads");
        using Stream s = a.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"the build has no {name}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
