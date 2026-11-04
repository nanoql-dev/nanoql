// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind;

/// <summary>
/// <c>nanoql-bind freeze</c>: a release's record of the names it ships (docs/bindings.md, "Names"; docs/excel.md,
/// "Names"). From one generation of nanoql's two assemblies, the one <c>check</c> and <c>excel check</c> read, it
/// writes <c>bindings/surface.shipped.json</c>, the C surface the release ships, and
/// <c>bindings/excel/functions.shipped.json</c>, every Excel name of the experimental build with its C name or its
/// family's key. It refuses, writing nothing, unless both checks are green, so the freeze records the checked-in
/// surface and no other. A name an earlier release shipped that the surface or the build no longer holds, a removed or
/// re-keyed function's or a type's, stays in its file, retired, and no later function or type takes it: under the C
/// file's <c>"retired"</c>, which is written only when it holds a name, so a first release's file is
/// <c>surface.json</c> byte for byte, and under the Excel file's.
/// </summary>
internal static class FreezeCommand
{
    /// <summary>Freezes the repository's surface.</summary>
    /// <param name="repo">The repository.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string repo)
    {
        var sw = Stopwatch.StartNew();
        string bindings = Path.Combine(repo, "bindings");
        string shipped = Path.Combine(bindings, "surface.shipped.json");
        string excelShipped = Path.Combine(bindings, "excel", "functions.shipped.json");
        var assemblies = new List<AssemblyInput>
        {
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")),
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")),
        };
        Generation g = Generation.Run(assemblies, [Path.Combine(bindings, "roles.json")], shipped, ExcelOptions.Nanoql(true) with { Shipped = excelShipped });
        ExcelOutput x = g.Excel!;
        int same = Program.Check(g, bindings);
        int excel = ExcelCommand.Check(x, Path.Combine(bindings, "excel", "functions.json"), sw);
        if (same != g.Files.Count || excel != 0)
        {
            throw new BindException($"freeze records the checked-in surface, and 'check' reads {same}/{g.Files.Count} and 'excel check' {(excel == 0 ? 1 : 0)}/1: "
                + "'nanoql-bind update' and 'nanoql-bind excel update' regenerate them; nothing was written");
        }

        // Every name the last release held, released or retired, that the surface or the build no longer holds stays,
        // retired, so no later function or type takes it.
        ShippedSurface last = ShippedSurface.Read(shipped);
        var functions = g.Surface.Functions.Select(f => f.CName).ToHashSet(StringComparer.Ordinal);
        var types = g.Surface.Types.Select(t => t.CName).ToHashSet(StringComparer.Ordinal);
        var retiredFunctions = last.FunctionNames.Where(n => !functions.Contains(n)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var retiredTypes = last.TypeNames.Where(n => !types.Contains(n)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var names = x.Selection.Units.Select(u => u.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var retired = ExcelShipped.Read(excelShipped).Reserved.Where(n => !names.Contains(n)).Order(StringComparer.Ordinal).ToList();
        string surface = g.Files["surface.json"];
        File.WriteAllBytes(shipped, Encoding.UTF8.GetBytes(retiredFunctions.Count + retiredTypes.Count == 0 ? surface : WithRetired(surface, retiredFunctions, retiredTypes)));
        File.WriteAllBytes(excelShipped, Encoding.UTF8.GetBytes(ExcelNames(x, retired)));
        int families = x.Selection.Units.Count(u => u.Family != null);
        Console.WriteLine($"freeze: bindings/surface.shipped.json, {g.Surface.Functions.Count:N0} functions and {g.Surface.Types.Count:N0} types, "
            + $"{retiredFunctions.Count:N0} function and {retiredTypes.Count:N0} type names retired; "
            + $"bindings/excel/functions.shipped.json, {x.Selection.Units.Count:N0} names ({families:N0} by family key, {x.Selection.Units.Count - families:N0} by C name), "
            + $"{retired.Count:N0} retired ({sw.Elapsed.TotalSeconds:F1} s)");
        return 0;
    }

    // surface.json's text with the retired names before its closing brace: "retired", its functions' and its types'.
    private static string WithRetired(string surface, List<string> functions, List<string> types)
    {
        const string End = "\n}\n";
        if (!surface.EndsWith(End, StringComparison.Ordinal))
        {
            throw new BindException("surface.json's text doesn't end as the freeze reads it");
        }

        string List(List<string> names) => string.Join(", ", names.Select(Js));
        return surface[..^End.Length] + $",\n \"retired\": {{\"functions\": [{List(functions)}], \"types\": [{List(types)}]}}" + End;
    }

    // The Excel names, one a line in functions.json's order: a family by its key, any other function by its C name.
    private static string ExcelNames(ExcelOutput x, List<string> retired)
    {
        var sb = new StringBuilder();
        sb.Append("{\"version\": ").Append(Js(Versions.Text)).Append(",\n \"functions\": [\n");
        IReadOnlyList<ExcelUnit> units = x.Selection.Units;
        for (int i = 0; i < units.Count; i++)
        {
            ExcelUnit u = units[i];
            string id = u.Family != null ? $"\"key\": {Js(ExcelSelection.FamilyKey(u.Function))}" : $"\"c\": {Js(u.Function.CName)}";
            sb.Append($"  {{\"excel\": {Js(u.Name)}, {id}}}").Append(i + 1 < units.Count ? ",\n" : "\n");
        }

        return sb.Append(" ],\n \"retired\": [").Append(string.Join(", ", retired.Select(Js))).Append("]}\n").ToString();
    }

    private static string Js(string s) => JsonSerializer.Serialize(s);
}
