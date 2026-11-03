// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The Excel builds the tests read, each made once: nanoql's listed builds and its tests' builds, experimental and
/// stable, and the fixtures under the prefix <c>fx</c> beside nanoql's whole surface, on a scratch list of their own.
/// Each run's outputs are text: the generated C#, <c>functions.json</c> and the templates.
/// </summary>
internal static class ExcelSurfaces
{
    private static readonly Lazy<ExcelOutput> ListedExperimentalRun = new(() => Run(true, Nanoql()));
    private static readonly Lazy<ExcelOutput> ListedStableRun = new(() => Run(false, Nanoql()));
    private static readonly Lazy<ExcelOutput> WholeExperimentalRun = new(() => Run(true, Whole()));
    private static readonly Lazy<ExcelOutput> WholeStableRun = new(() => Run(false, Whole()));
    private static readonly Lazy<ExcelOutput> FixturesRun = new(() => Run(true, new() { ["nql"] = null, ["fx"] = List("NanoQL.Bind.Tests.Fixtures.Excel.Square", "NanoQL.Bind.Tests.Fixtures.Excel.PlateUser", "NanoQL.Bind.Tests.Fixtures.Excel.Selfish", "NanoQL.Bind.Tests.Fixtures.Excel.LeftOut") }, fixtures: true));

    private static readonly Regex Head = new("^    \\[ExcelFunction\\(Name = \"([^\"]+)\"", RegexOptions.Multiline);
    private static readonly Regex Serial = new("public static object F\\d+\\(");

    /// <summary>Gets nanoql's listed experimental build.</summary>
    public static ExcelOutput ListedExperimental => ListedExperimentalRun.Value;

    /// <summary>Gets nanoql's listed stable build.</summary>
    public static ExcelOutput ListedStable => ListedStableRun.Value;

    /// <summary>Gets the experimental tests' build: nanoql's whole filtered surface.</summary>
    public static ExcelOutput WholeExperimental => WholeExperimentalRun.Value;

    /// <summary>Gets the stable tests' build.</summary>
    public static ExcelOutput WholeStable => WholeStableRun.Value;

    /// <summary>Gets nanoql's whole surface with the fixtures on a list of four of their types: Square, PlateUser, Selfish, LeftOut.</summary>
    public static ExcelOutput Users => FixturesRun.Value;

    /// <summary>nanoql's list on its prefix.</summary>
    /// <returns>The lists.</returns>
    public static Dictionary<string, ExcelList?> Nanoql() => new(StringComparer.Ordinal) { ["nql"] = ExcelList.Nanoql() };

    /// <summary>No list: the whole filtered surface.</summary>
    /// <returns>The lists.</returns>
    public static Dictionary<string, ExcelList?> Whole() => new(StringComparer.Ordinal) { ["nql"] = null };

    /// <summary>An Excel build.</summary>
    /// <param name="experimental">Whether it holds the experimental API.</param>
    /// <param name="lists">Each prefix's list.</param>
    /// <param name="fixtures">Whether the fixtures are bound beside nanoql, under <c>fx</c>.</param>
    /// <param name="shipped">A functions.shipped.json, or null.</param>
    /// <returns>Its outputs.</returns>
    public static ExcelOutput Run(bool experimental, Dictionary<string, ExcelList?> lists, bool fixtures = false, string? shipped = null)
    {
        var inputs = new List<AssemblyInput>
        {
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")),
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")),
        };
        if (fixtures)
        {
            inputs.Add(new("fx", typeof(ExcelSurfaces).Assembly.Location));
        }

        var options = new ExcelOptions { Experimental = experimental, Lists = lists, Shipped = shipped };
        return Generation.Run(inputs, [Path.Combine(Surfaces.Repository, "bindings", "roles.json")], null, options).Excel!;
    }

    /// <summary>A scratch list of types, each in one task.</summary>
    /// <param name="types">The types' full names.</param>
    /// <returns>The list.</returns>
    public static ExcelList List(params string[] types)
        => ExcelList.Parse("{\"version\": 1, \"types\": [" + string.Join(", ", types.Select(t => $"{{\"type\": \"{t}\", \"task\": \"tests\"}}")) + "]}", "the tests' list");

    /// <summary>nanoql's list with types added or taken out.</summary>
    /// <param name="add">The types added.</param>
    /// <param name="remove">The types taken out.</param>
    /// <returns>The list.</returns>
    public static ExcelList NanoqlWith(string[] add, string[] remove)
        => List([.. ExcelList.Nanoql().Entries.Select(e => e.Type).Except(remove).Concat(add)]);

    /// <summary>A build's functions.json entries by Excel name.</summary>
    /// <param name="x">The build.</param>
    /// <returns>The entries.</returns>
    public static Dictionary<string, JsonElement> Manifest(ExcelOutput x)
    {
        using JsonDocument doc = JsonDocument.Parse(x.Manifest);
        return doc.RootElement.GetProperty("functions").EnumerateArray().ToDictionary(f => f.GetProperty("excel").GetString()!, f => f.Clone());
    }

    /// <summary>A build's withheld functions: their Excel names with what each lacks.</summary>
    /// <param name="x">The build.</param>
    /// <returns>The withheld.</returns>
    public static Dictionary<string, string[]> Withheld(ExcelOutput x)
    {
        using JsonDocument doc = JsonDocument.Parse(x.Manifest);
        return doc.RootElement.GetProperty("withheld").EnumerateArray().ToDictionary(
            w => w.GetProperty("c").GetString()!,
            w => w.GetProperty("lacks").EnumerateArray().Select(l => l.GetString()!).ToArray());
    }

    /// <summary>The generated worksheet functions' text by Excel name, the method's serial masked.</summary>
    /// <param name="x">The build.</param>
    /// <returns>Each function's text.</returns>
    public static Dictionary<string, string> Functions(ExcelOutput x)
    {
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string name, string text) in x.Files.Where(f => f.Key.StartsWith("XlFunctions", StringComparison.Ordinal)).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            string body = text[..text.TrimEnd().LastIndexOf('}')];
            var ms = Head.Matches(body);
            for (int i = 0; i < ms.Count; i++)
            {
                int end = i + 1 < ms.Count ? ms[i + 1].Index : body.Length;
                out_[ms[i].Groups[1].Value] = Serial.Replace(body[ms[i].Index..end], "public static object F#(");
            }
        }

        return out_;
    }

    /// <summary>A generated worksheet function's text by its Excel name.</summary>
    /// <param name="x">The build.</param>
    /// <param name="name">The Excel name.</param>
    /// <returns>Its text.</returns>
    public static string Function(ExcelOutput x, string name) => Functions(x)[name];

    /// <summary>The Excel name of a C function in a build.</summary>
    /// <param name="x">The build.</param>
    /// <param name="cname">The C name.</param>
    /// <returns>The Excel name, or null.</returns>
    public static string? NameOf(ExcelOutput x, string cname)
        => x.Selection.Units.FirstOrDefault(u => u.Functions.Any(f => f.CName == cname))?.Name;
}
