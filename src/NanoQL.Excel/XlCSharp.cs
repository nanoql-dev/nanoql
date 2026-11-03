// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NanoQL.Excel;

/// <summary>
/// The repro's <c>"csharp"</c> format: a vector's calls as the nanoql calls the cell ran, from the Excel back end's
/// repro templates. Each Excel value is a C# literal by its parameter's type; a bound engine is the shared emitter's
/// adapter with its bound inputs; draws are the generator and its loop; the date system is applied; the conventions
/// file and each read file are set up from the vector. The program compiles against nanoql alone, and prints the cell's
/// outcome as a grid of cells.
/// </summary>
internal sealed class XlCSharp
{
    private const string Q = "global::";

    // The current convention set, as an empty set argument and a name read it.
    private const string CurrentSet = "(global::NanoQL.ConventionProvider.Current as global::NanoQL.Extensions.Conventions.ConventionSet ?? global::NanoQL.Extensions.Conventions.ConventionSet.QuantLib)";

    private static readonly string[] Named = ["global::NanoQL.Time.Calendar", "global::NanoQL.Time.DayCounter"];

    // Readable text: JSON's escapes for quotes, backslashes and control characters only (a file's markup stays as written).
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Dictionary<string, JsonElement> templates;
    private readonly JsonElement inputs;
    private readonly int shift;
    private readonly Dictionary<string, string> vars = new(StringComparer.Ordinal);
    private readonly List<string> lines = [];
    private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);
    private readonly SortedSet<int> adapters = [];
    private readonly SortedSet<string> sets = new(StringComparer.Ordinal);

    private XlCSharp(Dictionary<string, JsonElement> templates, JsonElement inputs)
    {
        this.templates = templates;
        this.inputs = inputs;
        shift = inputs.GetProperty("dateSystem").GetInt32() == 1904 ? 1462 : 0;
    }

    /// <summary>A vector's calls as a C# program.</summary>
    /// <param name="vector">The vector's text.</param>
    /// <param name="templates">The build's repro templates.</param>
    /// <param name="adapterSource">The build's engine adapters' source.</param>
    /// <param name="ns">A namespace for the program's types, or none.</param>
    /// <returns>The program's lines.</returns>
    public static List<string> Render(string vector, string templates, string adapterSource, string? ns = null)
    {
        using JsonDocument v = JsonDocument.Parse(vector);
        using JsonDocument t = JsonDocument.Parse(templates);
        var byName = t.RootElement.GetProperty("functions").EnumerateArray().ToDictionary(f => f.GetProperty("excel").GetString()!, StringComparer.Ordinal);
        var r = new XlCSharp(byName, v.RootElement.GetProperty("cases")[0].GetProperty("inputs"));
        List<string> body = r.Body();
        var program = new List<string>
        {
            "// nqlxlRepro: a cell's nanoql calls as C#. Compile it against NanoQL.dll and NanoQL.Extensions.dll and run it:",
            "// Main prints the cell's outcome as a grid of cells, each a double's bits, a text, a boolean, an object's type or an",
            "// error's code.",
            "#nullable disable",
            "#pragma warning disable",
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
        };
        if (ns != null)
        {
            program.Add($"namespace {ns};");
        }

        program.Add(string.Empty);
        program.Add("public static partial class Repro");
        program.Add("{");
        program.Add("    static readonly List<string> temps = new();");
        program.Add(string.Empty);
        program.Add("    public static string Run()");
        program.Add("    {");
        program.Add("        var saved_ = global::NanoQL.ConventionProvider.Default;");
        program.Add("        try");
        program.Add("        {");
        program.AddRange(body.Select(l => "            " + l));
        program.Add("        }");
        program.Add("        finally");
        program.Add("        {");
        program.Add("            global::NanoQL.ConventionProvider.Default = saved_;");
        program.Add("            foreach (var t_ in temps) System.IO.File.Delete(t_);");
        program.Add("            temps.Clear();");
        program.Add("        }");
        program.Add("    }");
        program.Add(string.Empty);
        program.Add("    public static void Main() => Console.WriteLine(Run());");
        foreach (int idx in r.adapters)
        {
            program.Add(string.Empty);
            program.AddRange(Adapter(adapterSource, idx, r.sets).Split('\n'));
        }

        program.Add("}");
        program.AddRange(XlReproText.CSharpHelpers.Split('\n'));
        return program;
    }

    private static string Num(double d)
    {
        string r = d.ToString("R", CultureInfo.InvariantCulture);
        return r.Contains('.', StringComparison.Ordinal) || r.Contains('E', StringComparison.Ordinal) ? r : r + ".0";
    }

    private static string Str(string s) => JsonSerializer.Serialize(s, Relaxed);

    private static bool IsNull(JsonElement? x) => x is not { } e || e.ValueKind == JsonValueKind.Null;

    private static bool Empty(JsonElement? x) => IsNull(x) || (x!.Value.ValueKind == JsonValueKind.Object && x.Value.TryGetProperty("empty", out _));

    private static bool Has(JsonElement? x, string name) => x is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out _);

    private static (int Rows, int Cols, List<JsonElement?> Cells) Cells(JsonElement? x)
        => Has(x, "rows")
            ? (x!.Value.GetProperty("rows").GetInt32(), x.Value.GetProperty("cols").GetInt32(), [.. x.Value.GetProperty("cells").EnumerateArray().Select(c => (JsonElement?)c)])
            : (1, 1, [x]);

    private static bool Multi(JsonElement? x) => Has(x, "rows") && x!.Value.GetProperty("rows").GetInt32() * x.Value.GetProperty("cols").GetInt32() > 1;

    // A kind one Excel cell holds, which a range given in its place loops over.
    private static bool ScalarKind(string kind)
        => !(kind.StartsWith("array:", StringComparison.Ordinal) || kind.StartsWith("jagged:", StringComparison.Ordinal) || kind.StartsWith("map:", StringComparison.Ordinal)
            || kind.StartsWith("tuple:", StringComparison.Ordinal) || kind is "matrix" or "vector");

    private static string? S(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // A complex number as the add-in reads it: a number, or Excel's complex text.
    private static (double Re, double Im) ComplexParts(JsonElement x)
    {
        System.Numerics.Complex c = X.Converted(x, "complex", X.Cplx);
        return (c.Real, c.Imaginary);
    }

    // The adapter A<idx> as the shared emitter wrote it, holding only the cases this program binds.
    private static string Adapter(string source, int idx, ISet<string> sets)
    {
        string head = $"    public static ";
        int at = source.IndexOf($" A{idx.ToString(CultureInfo.InvariantCulture)}(global::NanoQL.Excel.BoundEngine b, string member)\n    {{\n", StringComparison.Ordinal);
        if (at < 0)
        {
            throw new ArgumentException($"the build has no adapter A{idx}");
        }

        int start = source.LastIndexOf(head, at, StringComparison.Ordinal);
        int k = source.IndexOf('{', at) + 1;
        for (int depth = 1; depth > 0; k++)
        {
            depth += source[k] switch { '{' => 1, '}' => -1, _ => 0 };
        }

        string text = source[start..k];
        var keep = new List<string>();
        foreach (string s in sets)
        {
            int cm = text.IndexOf($"case \"{s}\":", StringComparison.Ordinal);
            if (cm < 0)
            {
                continue;
            }

            int b = text.IndexOf('{', cm) + 1, e = b;
            for (int depth = 1; depth > 0; e++)
            {
                depth += text[e] switch { '{' => 1, '}' => -1, _ => 0 };
            }

            keep.Add(text[cm..e]);
        }

        string sig = text[..text.IndexOf("switch (b.Set)", StringComparison.Ordinal)].Replace("global::NanoQL.Excel.BoundEngine", "BoundEngine", StringComparison.Ordinal);
        return sig + "switch (b.Set)\n        {\n            " + string.Join("\n            ", keep) + "\n            default: throw b.NoCalculation(member);\n        }\n    }";
    }

    private List<string> Body()
    {
        var body = new List<string>();
        if (inputs.TryGetProperty("files", out JsonElement fs))
        {
            int n = 0;
            foreach (JsonProperty f in fs.EnumerateObject())
            {
                string fv = "f" + (++n).ToString(CultureInfo.InvariantCulture);
                files[f.Name] = fv;
                body.Add($"var {fv} = {Q}System.IO.Path.Combine({Q}System.IO.Path.GetTempPath(), \"nqlxl-repro-\" + {Q}System.Guid.NewGuid().ToString(\"N\") + \".xml\");");
                body.Add($"{Q}System.IO.File.WriteAllText({fv}, {Str(f.Value.GetString()!)}); temps.Add({fv});");
            }
        }

        if (inputs.TryGetProperty("conventions", out JsonElement conventions))
        {
            body.Add($"{Q}NanoQL.ConventionProvider.Default = {Q}NanoQL.Extensions.Conventions.ConventionSet.QuantLib.With({Q}NanoQL.Extensions.Conventions.ConventionsXml.Read("
                + $"new {Q}System.IO.MemoryStream({Q}System.Text.Encoding.UTF8.GetBytes({Str(conventions.GetString()!)})), \"embedded\"));");
        }

        foreach (JsonElement c in inputs.GetProperty("calls").EnumerateArray())
        {
            Call(c);
        }

        body.AddRange(lines);
        body.Add($"return Show({vars[inputs.GetProperty("result").GetString()!]}, {shift.ToString(CultureInfo.InvariantCulture)});");
        return body;
    }

    // One Excel value as a C# literal of a scalar kind.
    private string Scalar(string kind, JsonElement? x, string cs)
    {
        string k = kind.TrimEnd('?');
        if (Empty(x))
        {
            return kind.EndsWith('?') ? $"({cs}?)null" : throw new XlRefusal($"an empty cell where {kind} is required");
        }

        JsonElement v = x!.Value;
        if (Has(v, "ref"))
        {
            return vars[v.GetProperty("ref").GetString()!];
        }

        // Each value converted by the add-in's own conversions (X.Values), so the C# refuses what the cell refused; a
        // period's text and an enum's name parse in the program, as the add-in parses them.
        return k switch
        {
            "double" => Num(X.Converted(v, kind, X.Dbl)),
            "int32" => X.Converted(v, kind, X.Int).ToString(CultureInfo.InvariantCulture),
            "int64" => X.Converted(v, kind, X.Long).ToString(CultureInfo.InvariantCulture) + "L",
            "bool" => X.Converted(v, kind, X.Bool) ? "true" : "false",
            "string" => files.GetValueOrDefault(X.Converted(v, kind, X.Str)) ?? Str(X.Converted(v, kind, X.Str)),
            "date" => $"new {Q}NanoQL.Time.Date({(X.Converted(v, kind, X.Serial) + shift).ToString(CultureInfo.InvariantCulture)})",
            "period" => $"{Q}NanoQL.Utilities.PeriodParser.Parse({Str(X.Converted(v, kind, X.Str))})",
            "enum" => $"{Q}System.Enum.Parse<{cs}>({Str(X.EnumName(v, kind))}, true)",
            "complex" => ComplexLiteral(v),
            "handle" or "boxed" or "snapshot" => throw new ArgumentException("a handle that isn't a reference"),
            _ => throw new ArgumentException("no literal for " + kind),
        };
    }

    private string ComplexLiteral(JsonElement v)
    {
        (double re, double im) = ComplexParts(v);
        return $"new {Q}System.Numerics.Complex({Num(re)}, {Num(im)})";
    }

    private string Arg(JsonElement p, JsonElement? x, string? memberC, bool optional)
    {
        string kind = p.GetProperty("kind").GetString()!, cs = S(p, "cs") ?? "object";
        if (IsNull(x) && optional)
        {
            return S(p, "default") ?? "default";
        }

        if (kind == "engine")
        {
            if (IsNull(x))
            {
                return "null";
            }

            int idx = p.GetProperty("adapter").GetInt32();
            adapters.Add(idx);
            return $"A{idx.ToString(CultureInfo.InvariantCulture)}({vars[x!.Value.GetProperty("ref").GetString()!]}, {Str(memberC!)})";
        }

        if (kind is "handle" or "boxed" or "boxed?" or "snapshot")
        {
            if (Empty(x))
            {
                return $"({cs})null";
            }

            JsonElement v = x!.Value;
            if (Has(v, "item"))
            {
                return $"({cs}){Q}System.Linq.Enumerable.ElementAt((global::System.Collections.Generic.IEnumerable<object>){vars[v.GetProperty("ref").GetString()!]}, {v.GetProperty("item").GetInt32().ToString(CultureInfo.InvariantCulture)})";
            }

            if (Has(v, "ref"))
            {
                return vars[v.GetProperty("ref").GetString()!];
            }

            (int r, int c, List<JsonElement?> xs) = Cells(v);
            if (r * c == 1 && Has(xs[0], "ref"))
            {
                return vars[xs[0]!.Value.GetProperty("ref").GetString()!];
            }

            // A calendar's or a day counter's name, looked up in the current convention set.
            if (r * c == 1 && xs[0] is { ValueKind: JsonValueKind.String } name && Named.Contains(cs))
            {
                return $"{CurrentSet}.{cs[(cs.LastIndexOf('.') + 1)..]}({Str(name.GetString()!)})";
            }

            throw new ArgumentException("a handle argument that isn't a reference");
        }

        // A matrix's and a vector's cells as the add-in reads them: numbers alone, a vector's trailing empty cells trimmed and
        // an inner one refused.
        if (kind == "matrix")
        {
            Numerics.Matrix m = X.Converted(x ?? default, p.GetProperty("name").GetString()!, X.Mat);
            string rows = string.Join(", ", Enumerable.Range(0, m.Rows).Select(i => "{" + string.Join(", ", Enumerable.Range(0, m.Columns).Select(j => Num(m[i, j]))) + "}"));
            return $"Mat(new double[,] {{ {rows} }})";
        }

        if (kind == "vector")
        {
            double[] ds = X.Converted(x ?? default, p.GetProperty("name").GetString()!, X.Dbls);
            return $"new {Q}NanoQL.Numerics.Vector(new double[] {{ {string.Join(", ", ds.Select(Num))} }})";
        }

        // A tuple across a row of its cells; an array of tuples a row per element, the empty rows at its end trimmed.
        if (kind.StartsWith("tuple:", StringComparison.Ordinal))
        {
            (_, _, List<JsonElement?> xs) = Cells(x);
            return "(" + string.Join(", ", p.GetProperty("items").EnumerateArray().Select((it, j) => Element(it.GetProperty("kind").GetString()!, xs[j], it.GetProperty("cs").GetString()!))) + ")";
        }

        if (kind.StartsWith("array:tuple:", StringComparison.Ordinal))
        {
            List<JsonElement> its = [.. p.GetProperty("items").EnumerateArray()];
            string tt = "(" + string.Join(", ", its.Select(it => it.GetProperty("cs").GetString() + (it.GetProperty("kind").GetString()!.EndsWith('?') ? "?" : string.Empty))) + ")";
            if (IsNull(x))
            {
                return $"new {tt}[0]";
            }

            (int r, int c, List<JsonElement?> xs) = Cells(x);
            var rows = Enumerable.Range(0, r).Select(i => xs.GetRange(i * c, c)).ToList();
            while (rows.Count > 0 && rows[^1].All(Empty))
            {
                rows.RemoveAt(rows.Count - 1);
            }

            var lits = rows.Select(row => "(" + string.Join(", ", its.Select((it, j) => Element(it.GetProperty("kind").GetString()!, row[j], it.GetProperty("cs").GetString()!))) + ")");
            return $"new {tt}[] {{ {string.Join(", ", lits)} }}";
        }

        if (kind.StartsWith("array:", StringComparison.Ordinal))
        {
            string ek = kind[6..], el = S(p, "el")!;
            string elt = el + (ek.EndsWith('?') && !el.EndsWith('?') ? "?" : string.Empty);
            if (IsNull(x))
            {
                return $"new {elt}[0]";
            }

            (_, _, List<JsonElement?> xs) = Cells(x);
            if (!ek.EndsWith('?'))
            {
                while (xs.Count > 0 && Empty(xs[^1]))
                {
                    xs.RemoveAt(xs.Count - 1);
                }
            }

            return $"new {elt}[] {{ {string.Join(", ", xs.Select(v => Element(ek, v, el)))} }}";
        }

        if (kind.StartsWith("jagged:", StringComparison.Ordinal))
        {
            string ek = kind[7..];
            (int r, int c, List<JsonElement?> xs) = Cells(x);
            var rows = new List<List<JsonElement?>>();
            for (int i = 0; i < r; i++)
            {
                List<JsonElement?> row = xs.GetRange(i * c, c);
                while (row.Count > 0 && Empty(row[^1]))
                {
                    row.RemoveAt(row.Count - 1);
                }

                rows.Add(row);
            }

            while (rows.Count > 0 && rows[^1].Count == 0)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            return $"new {ek}[][] {{ {string.Join(", ", rows.Select(row => $"new {ek}[] {{ {string.Join(", ", row.Select(v => Element(ek, v, ek)))} }}"))} }}";
        }

        if (kind.StartsWith("map:", StringComparison.Ordinal))
        {
            (int r, _, List<JsonElement?> xs) = Cells(x);
            var pairs = new List<string>();
            for (int i = 0; i < r; i++)
            {
                JsonElement? kx = xs[2 * i], vx = xs[(2 * i) + 1];
                if (Empty(kx) && Empty(vx))
                {
                    continue;
                }

                pairs.Add($"[{Element(S(p, "elKind")!, kx, S(p, "el")!)}] = {Element(S(p, "valKind")!, vx, S(p, "val")!)}");
            }

            return $"new {Q}System.Collections.Generic.Dictionary<{S(p, "el")}, {S(p, "val")}> {{ {string.Join(", ", pairs)} }}";
        }

        if (Has(x, "rows"))
        {
            x = Cells(x).Cells[0];
        }

        return Scalar(kind, x, cs);
    }

    private string Element(string ek, JsonElement? v, string cs)
    {
        if (ek is "handle" or "boxed")
        {
            if (Has(v, "item"))
            {
                return $"({cs}){Q}System.Linq.Enumerable.ElementAt((global::System.Collections.Generic.IEnumerable<object>){vars[v!.Value.GetProperty("ref").GetString()!]}, {v.Value.GetProperty("item").GetInt32().ToString(CultureInfo.InvariantCulture)})";
            }

            if (Has(v, "ref"))
            {
                return vars[v!.Value.GetProperty("ref").GetString()!];
            }

            // A name inside an array of calendars or day counters, as the runtime's Objs<T> takes it.
            if (v is { ValueKind: JsonValueKind.String } name && Named.Contains(cs))
            {
                return $"{CurrentSet}.{cs[(cs.LastIndexOf('.') + 1)..]}({Str(name.GetString()!)})";
            }

            return $"({cs})null";
        }

        return Scalar(ek, v, cs);
    }

    // One call's C# expression: a bind's engine, a draws' result, or the member's call.
    private string Expr(JsonElement f, string kind, List<JsonElement?> args)
    {
        if (kind is "bind" or "bindon")
        {
            var given = new List<string>();
            string instrument = "null";
            int i = 0;
            foreach (JsonElement p in f.GetProperty("params").EnumerateArray())
            {
                JsonElement? x = i < args.Count ? args[i] : null;
                i++;
                if (IsNull(x) || Empty(x))
                {
                    continue;
                }

                if (p.GetProperty("name").GetString() == "instrument" && kind == "bindon")
                {
                    instrument = vars[x!.Value.GetProperty("ref").GetString()!];
                    continue;
                }

                given.Add($"[{Str(p.GetProperty("name").GetString()!)}] = {Arg(p, x, null, false)}");
            }

            sets.Add(f.GetProperty("set").GetString()!);
            return $"new BoundEngine({Str(f.GetProperty("set").GetString()!)}, {instrument}, new {Q}System.Collections.Generic.Dictionary<string, object> {{ {string.Join(", ", given)} }})";
        }

        if (kind == "draws")
        {
            bool seq = f.TryGetProperty("sequences", out JsonElement sq) && sq.ValueKind == JsonValueKind.True;
            JsonElement? count = Has(args[1], "rows") ? Cells(args[1]).Cells[0] : args[1];
            return $"Draws.{(seq ? "OfSequences" : "Of")}(({Q}NanoQL.Numerics.{(seq ? "IRsg" : "IRng")})(object){vars[args[0]!.Value.GetProperty("ref").GetString()!]}, {X.DrawsCount(count).ToString(CultureInfo.InvariantCulture)})";
        }

        string text = f.GetProperty("tpl").GetString()!;
        int k = 0;
        string c = f.GetProperty("c").GetString()!;
        foreach (JsonElement p in f.GetProperty("params").EnumerateArray())
        {
            JsonElement? x = k < args.Count ? args[k] : null;
            string lit = p.GetProperty("self").GetBoolean() && S(p, "cs") == "global::NanoQL.Extensions.Conventions.ConventionSet" && Empty(x)
                ? CurrentSet
                : Arg(p, x, c, p.GetProperty("optional").GetBoolean());
            text = text.Replace($"@@{k.ToString(CultureInfo.InvariantCulture)}@@", lit, StringComparison.Ordinal);
            k++;
        }

        return text;
    }

    private void Call(JsonElement c)
    {
        string fn = c.GetProperty("fn").GetString()!, id = c.GetProperty("id").GetString()!;
        string var = "v" + id[1..];
        List<JsonElement?> args = [.. c.GetProperty("args").EnumerateArray().Select(a => (JsonElement?)a)];
        if (fn is "nqlxlDrawsValues" or "nqlxlDrawsWeights" or "nqlxlDrawsNext")
        {
            string field = fn["nqlxlDraws".Length..];
            lines.Add($"var {var} = {vars[args[0]!.Value.GetProperty("ref").GetString()!]}.{field};");
            vars[id] = var;
            return;
        }

        JsonElement f = templates.TryGetValue(fn, out JsonElement found) ? found : throw new ArgumentException($"the build has no function {fn}");
        string kind = f.GetProperty("kind").GetString()!;
        if (f.TryGetProperty("family", out JsonElement family))
        {
            int nT = family[0].GetProperty("key").GetString()!.Split('|').Length;
            string key = string.Join("|", args.Take(nT).Select(a => (a!.Value.ValueKind == JsonValueKind.String ? a.Value.GetString()! : a.Value.GetRawText()).ToLowerInvariant()));
            f = family.EnumerateArray().FirstOrDefault(m => m.GetProperty("key").GetString() == key);
            if (f.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException($"{fn}: no construction {key}");
            }

            args = args.Skip(nT).ToList();
        }

        List<JsonElement> ps = f.TryGetProperty("params", out JsonElement pa) ? [.. pa.EnumerateArray()] : [];
        int nParams = kind == "draws" ? 2 : ps.Count;
        while (args.Count < nParams)
        {
            args.Add(null);
        }

        // Loop exactly where the add-in's mask loops (binds and draws included); a multi-cell range where one value
        // goes and the mask doesn't loop is the add-in's refusal, never its first cell; ranges pair as Excel pairs
        // them.
        string mask = S(f, "loops") ?? string.Empty;
        var lifted = Enumerable.Range(0, args.Count).Where(i => i < mask.Length && mask[i] == '1' && Multi(args[i])).ToList();
        var unlooped = Enumerable.Range(0, ps.Count).Where(i => ScalarKind(ps[i].GetProperty("kind").GetString()!) && Multi(args[i]) && !lifted.Contains(i)).ToList();
        if (unlooped.Count > 0)
        {
            string name = ps[unlooped[0]].GetProperty("name").GetString()!;
            lines.Add($"var {var} = Refused({Str($"{fn}: {name}: a range where one value goes, which the add-in does not loop")});");
            vars[id] = var;
            return;
        }

        bool isVoid = f.TryGetProperty("returns", out JsonElement ret) ? ret.ValueKind == JsonValueKind.Null : kind is not ("bind" or "bindon" or "draws");
        isVoid &= kind is not ("bind" or "bindon" or "draws");
        if (lifted.Count > 0)
        {
            int rr = lifted.Max(i => args[i]!.Value.GetProperty("rows").GetInt32()), cc = lifted.Max(i => args[i]!.Value.GetProperty("cols").GetInt32());
            lines.Add($"var {var} = new object[{rr.ToString(CultureInfo.InvariantCulture)}, {cc.ToString(CultureInfo.InvariantCulture)}];");
            for (int i = 0; i < rr; i++)
            {
                for (int j = 0; j < cc; j++)
                {
                    var ea = new List<JsonElement?>(args);
                    bool na = false;
                    foreach (int k in lifted)
                    {
                        (int r, int cl, List<JsonElement?> xs) = Cells(args[k]);
                        int ii = r == 1 ? 0 : i, jj = cl == 1 ? 0 : j;
                        if (ii >= r || jj >= cl)
                        {
                            na = true;
                            break;
                        }

                        ea[k] = xs[(ii * cl) + jj];
                    }

                    JsonElement? err = ea.FirstOrDefault(a => Has(a, "error"));
                    string elem;
                    if (na)
                    {
                        elem = "new ElemNA()";
                    }
                    else if (err != null)
                    {
                        elem = $"new ElemError({err.Value.GetProperty("error").GetInt32().ToString(CultureInfo.InvariantCulture)})";
                    }
                    else
                    {
                        try
                        {
                            string text = Expr(f, kind, ea);
                            elem = isVoid ? $"Elem(() => {{ {text}; return \"OK\"; }})" : $"Elem(() => (object)({text}))";
                        }
                        catch (ArgumentException e)
                        {
                            // An element no C# literal holds: Excel refused it, so the C# call does.
                            elem = $"Elem(() => throw new {Q}System.ArgumentException({Str(e.Message)}))";
                        }
                    }

                    lines.Add($"{var}[{i.ToString(CultureInfo.InvariantCulture)}, {j.ToString(CultureInfo.InvariantCulture)}] = {elem};");
                }
            }

            vars[id] = var;
            return;
        }

        string call;
        try
        {
            call = Expr(f, kind, args);
        }
        catch (XlRefusal e)
        {
            // A value the add-in's conversions refuse: the cell's call failed before nanoql's, so the C# call does.
            lines.Add($"var {var} = Refused({Str($"{fn}: {e.Message}")});");
            vars[id] = var;
            return;
        }

        lines.Add(isVoid ? $"{call}; object {var} = null;" : $"var {var} = {call};");
        vars[id] = var;
    }
}
