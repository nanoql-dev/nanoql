// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using NanoQL.Bind.Model;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// An Excel build's surface as <c>functions.json</c>: each worksheet function's Excel name, C name, kind, category,
/// experimental mark, arguments and their kinds, result, loop mask, and a family's key and its constructions, each
/// marked; the add-in's own functions; and the functions the completeness check withholds, with what each lacks.
/// nanoql's experimental build's is checked in as <c>bindings/excel/functions.json</c>, which <c>excel check</c> diffs;
/// one function a line, so a change shows in review.
/// </summary>
internal static class ExcelManifest
{
    /// <summary>The add-in's own functions a build registers: the runtime's, and <c>nqlxlStats</c> in a test build.</summary>
    /// <param name="testBuild">Whether the build is a test build.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> Own(bool testBuild) => [.. ExcelNames.Own.Where(n => testBuild || n != "nqlxlStats")];

    /// <summary>Writes a build's surface.</summary>
    /// <param name="selection">The build's functions.</param>
    /// <param name="options">The build's options.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(ExcelSelection selection, ExcelOptions options)
    {
        var sb = new StringBuilder();
        sb.Append("{\"version\": ").Append(Js(Versions.Text)).Append(", \"build\": ").Append(Js(options.Experimental ? "experimental" : "stable")).Append(",\n \"functions\": [\n");
        for (int i = 0; i < selection.Units.Count; i++)
        {
            sb.Append("  ").Append(Function(selection.Units[i])).Append(i + 1 < selection.Units.Count ? ",\n" : "\n");
        }

        sb.Append(" ],\n \"own\": [").Append(string.Join(", ", Own(options.TestBuild).Select(Js))).Append("],\n \"withheld\": [\n");
        for (int i = 0; i < selection.Withheld.Count; i++)
        {
            ExcelWithheld w = selection.Withheld[i];
            sb.Append($"  {{\"excel\": {Js(w.Name)}, \"c\": {Js(w.Function.CName)}, \"lacks\": [{string.Join(", ", w.Lacks.Select(Js))}]}}")
                .Append(i + 1 < selection.Withheld.Count ? ",\n" : "\n");
        }

        return sb.Append(" ]}\n").ToString();
    }

    private static string Function(ExcelUnit u)
    {
        CFunction f = u.Function;
        var args = new List<string>();
        if (u.Family != null)
        {
            args.AddRange(f.Type.GetGenericTypeDefinition().GetGenericArguments().Select(t => $"{{\"name\": {Js(t.Name)}, \"kind\": \"type\"}}"));
        }

        args.AddRange(f.Params.Select(p => $"{{\"name\": {Js(p.Name)}, \"kind\": {Js(p.Kind.Text)}{(p.Optional ? ", \"optional\": true" : string.Empty)}}}"));
        string category = "nanoql." + (f.Type.Namespace ?? "NanoQL").Replace("NanoQL.", string.Empty, StringComparison.Ordinal);
        string mask = ExcelWriter.Mask(u) is { } m ? $", \"loops\": {Js(m)}" : string.Empty;
        string returns = f.Return == null ? "null" : Js(f.Return.Text);
        if (u.Family == null)
        {
            return $"{{\"excel\": {Js(u.Name)}, \"c\": {Js(f.CName)}, \"kind\": {Js(f.Kind)}, \"category\": {Js(category)}, \"experimental\": {Bool(u.Experimental)}, \"args\": [{string.Join(", ", args)}], \"returns\": {returns}{mask}}}";
        }

        // A family's entry: its key, a stable construction's C name where it has one, each construction marked.
        string c = (u.Family.FirstOrDefault(m => !m.Experimental) ?? u.Family[0]).CName;
        string constructions = string.Join(", ", u.Family.Concat(u.Refused).OrderBy(m => m.CName, StringComparer.Ordinal).Select(m =>
            $"{{\"args\": {Js(string.Join("/", m.Type.GetGenericArguments().Select(ExcelSelection.ArgName)))}, \"c\": {Js(m.CName)}, \"experimental\": {Bool(m.Experimental)}}}"));
        return $"{{\"excel\": {Js(u.Name)}, \"key\": {Js(ExcelSelection.FamilyKey(f))}, \"c\": {Js(c)}, \"kind\": {Js(f.Kind)}, \"category\": {Js(category)}, \"experimental\": {Bool(u.Experimental)}, \"args\": [{string.Join(", ", args)}], \"returns\": {returns}{mask}, \"constructions\": [{constructions}]}}";
    }

    private static string Bool(bool b) => b ? "true" : "false";

    private static string Js(string s) => JsonSerializer.Serialize(s);
}
