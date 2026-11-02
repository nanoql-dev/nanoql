// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;

namespace NanoQL.Excel;

/// <summary>
/// XLR14's <c>"python"</c>: a vector's calls in S-bind's Python layer, each call its Python member (the template's, from
/// the tool's Python back end): handles as objects, a bound engine as its <c>bind</c>, draws as the snapshot's batch, a
/// matrix as a 2-D NumPy array, a map as a dict, a jagged range as a list of lists, a complex number as Python's
/// <c>complex</c>, the date system applied, the conventions and read files set up as the C# sets them.
/// </summary>
internal sealed class XlPython
{
    private static readonly string[] Named = ["global::NanoQL.Time.Calendar", "global::NanoQL.Time.DayCounter"];

    private readonly Dictionary<string, JsonElement> templates;
    private readonly Dictionary<string, JsonElement> py;
    private readonly JsonElement inputs;
    private readonly int shift;
    private readonly Dictionary<string, string> vars = new(StringComparer.Ordinal);
    private readonly List<string> lines = [];
    private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);
    private readonly SortedSet<string> modules = new(StringComparer.Ordinal);

    private XlPython(Dictionary<string, JsonElement> templates, Dictionary<string, JsonElement> py, JsonElement inputs)
    {
        this.templates = templates;
        this.py = py;
        this.inputs = inputs;
        shift = inputs.GetProperty("dateSystem").GetInt32() == 1904 ? 1462 : 0;
    }

    /// <summary>A vector's calls as a Python script.</summary>
    /// <param name="vector">The vector's text.</param>
    /// <param name="templates">The build's repro templates.</param>
    /// <returns>The script's lines.</returns>
    public static List<string> Render(string vector, string templates)
    {
        using JsonDocument v = JsonDocument.Parse(vector);
        using JsonDocument t = JsonDocument.Parse(templates);
        var byName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var byC = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement f in t.RootElement.GetProperty("functions").EnumerateArray())
        {
            byName[f.GetProperty("excel").GetString()!] = f;
            foreach (JsonElement m in f.TryGetProperty("family", out JsonElement fam) ? fam.EnumerateArray() : new[] { f }.AsEnumerable())
            {
                if (m.TryGetProperty("py", out JsonElement p))
                {
                    byC[m.GetProperty("c").GetString()!] = p;
                }
            }
        }

        var r = new XlPython(byName, byC, v.RootElement.GetProperty("cases")[0].GetProperty("inputs"));
        List<string> body = r.Body();
        var script = new List<string>(XlReproText.PythonHead.Split('\n'));
        script.AddRange(r.modules.Where(m => m is not ("nanoql" or "nanoql.experimental")).Select(m => "import " + m));
        script.Add(string.Empty);
        script.Add("def run():");
        script.Add("    saved = nanoql.ConventionProvider.default()");
        script.Add("    try:");
        script.AddRange(body.Select(l => "        " + l));
        script.Add("    finally:");
        script.Add("        nanoql.ConventionProvider.set_default(saved)");
        script.Add("        for p in _temps:");
        script.Add("            os.remove(p)");
        script.Add("        _temps.clear()");
        script.Add(string.Empty);
        script.Add("if __name__ == \"__main__\":");
        script.Add("    print(json.dumps(run()))");
        return script;
    }

    private static string Repr(string s) => "'" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal) + "'";

    private static string Float(double d)
    {
        string r = d.ToString("R", CultureInfo.InvariantCulture);
        return r.Contains('.', StringComparison.Ordinal) || r.Contains('E', StringComparison.Ordinal) ? r.Replace("E", "e", StringComparison.Ordinal) : r + ".0";
    }

    private static bool IsNull(JsonElement? x) => x is not { } e || e.ValueKind == JsonValueKind.Null;

    private static bool Empty(JsonElement? x) => IsNull(x) || (x!.Value.ValueKind == JsonValueKind.Object && x.Value.TryGetProperty("empty", out _));

    private static bool Has(JsonElement? x, string name) => x is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out _);

    private static (int Rows, int Cols, List<JsonElement?> Cells) Cells(JsonElement? x)
        => Has(x, "rows")
            ? (x!.Value.GetProperty("rows").GetInt32(), x.Value.GetProperty("cols").GetInt32(), [.. x.Value.GetProperty("cells").EnumerateArray().Select(c => (JsonElement?)c)])
            : (1, 1, [x]);

    private static bool Multi(JsonElement? x) => Has(x, "rows") && x!.Value.GetProperty("rows").GetInt32() * x.Value.GetProperty("cols").GetInt32() > 1;

    private static bool ScalarKind(string kind)
        => !(kind.StartsWith("array:", StringComparison.Ordinal) || kind.StartsWith("jagged:", StringComparison.Ordinal) || kind.StartsWith("map:", StringComparison.Ordinal)
            || kind.StartsWith("tuple:", StringComparison.Ordinal) || kind is "matrix" or "vector");

    private static string? S(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

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
                body.Add($"{fv} = _tmp({Repr(f.Value.GetString()!)})");
            }
        }

        if (inputs.TryGetProperty("conventions", out JsonElement conventions))
        {
            body.Add($"nanoql.ConventionProvider.set_default(nanoql.ConventionSet.quant_lib().with_(nanoql.ConventionsXml.read(_tmp({Repr(conventions.GetString()!)}))))");
        }

        foreach (JsonElement c in inputs.GetProperty("calls").EnumerateArray())
        {
            Call(c);
        }

        body.AddRange(lines);
        body.Add($"return _show({vars[inputs.GetProperty("result").GetString()!]}, {shift.ToString(CultureInfo.InvariantCulture)})");
        return body;
    }

    // A C function's Python member: its module, class, name, style and parameters' names.
    private (string Owner, string Member, string Style, List<string> Params) Member(string c)
    {
        JsonElement p = py.TryGetValue(c, out JsonElement found) ? found : throw new ArgumentException($"no Python form for {c}");
        string module = p[0].GetString()!;
        modules.Add(module);
        return (module + "." + p[1].GetString(), p[2].GetString()!, p[3].GetString()!, [.. p[4].EnumerateArray().Select(n => n.GetString()!)]);
    }

    // A calendar's or a day counter's name, looked up in the current convention set (XLR16).
    private string Lookup(string cs, string name)
    {
        (string owner, string member, _, _) = Member(cs.EndsWith("Calendar", StringComparison.Ordinal) ? "nql_ConventionSet_Calendar" : "nql_ConventionSet_DayCounter");
        return $"{owner}.{member}(None, {Repr(name)})";
    }

    private string Scalar(string kind, JsonElement? x, string cs)
    {
        string k = kind.TrimEnd('?');
        if (Empty(x))
        {
            return kind.EndsWith('?') ? "None" : throw new XlRefusal($"an empty cell where {kind} is required");
        }

        JsonElement v = x!.Value;
        if (Has(v, "ref"))
        {
            return vars[v.GetProperty("ref").GetString()!];
        }

        // Each value converted by the add-in's own conversions (X.Values), so the Python refuses what the cell refused; a
        // period's text and an enum's name parse in the program, as the add-in parses them.
        switch (k)
        {
            case "double":
                return Float(X.Converted(v, kind, X.Dbl));
            case "int32":
                return X.Converted(v, kind, X.Int).ToString(CultureInfo.InvariantCulture);
            case "int64":
                return X.Converted(v, kind, X.Long).ToString(CultureInfo.InvariantCulture);
            case "bool":
                return X.Converted(v, kind, X.Bool) ? "True" : "False";
            case "string":
            {
                string s = X.Converted(v, kind, X.Str);
                return files.GetValueOrDefault(s) ?? Repr(s);
            }

            case "date":
                return $"_d({(X.Converted(v, kind, X.Serial) + shift).ToString(CultureInfo.InvariantCulture)})";
            case "period":
                return $"nanoql.PeriodParser.parse({Repr(X.Converted(v, kind, X.Str))})";
            case "enum":
            {
                string type = cs[(cs.LastIndexOf('.') + 1)..];
                return $"_e({EnumModule(cs)}.{type}, {Repr(X.EnumName(v, kind))})";
            }

            case "complex":
            {
                System.Numerics.Complex c = X.Converted(v, kind, X.Cplx);
                return $"complex({Float(c.Real)}, {Float(c.Imaginary)})";
            }

            default:
                throw new ArgumentException("no Python literal for " + kind);
        }
    }

    // An enum's module: nanoql's experimental module, which defines the experimental enums and re-exports the stable ones.
    private static string EnumModule(string cs) => "nanoql.experimental";

    private string Arg(JsonElement p, JsonElement? x)
    {
        string kind = p.GetProperty("kind").GetString()!, cs = S(p, "cs") ?? "object";
        if (kind is "handle" or "boxed" or "boxed?" or "snapshot" or "engine")
        {
            if (Empty(x))
            {
                return "None";
            }

            (int r, int c, List<JsonElement?> xs) = Cells(x);
            if (r * c == 1 && xs[0] is { ValueKind: JsonValueKind.String } name && Named.Contains(cs))
            {
                return Lookup(cs, name.GetString()!);
            }

            JsonElement re = Has(x, "ref") ? x!.Value : xs[0]!.Value;
            return Has(re, "item")
                ? $"{vars[re.GetProperty("ref").GetString()!]}[{re.GetProperty("item").GetInt32().ToString(CultureInfo.InvariantCulture)}]"
                : vars[re.GetProperty("ref").GetString()!];
        }

        // A matrix's and a vector's cells as the add-in reads them: numbers alone, a vector's trailing empty cells trimmed and
        // an inner one refused (XLR2).
        if (kind == "matrix")
        {
            Numerics.Matrix mx = X.Converted(x ?? default, p.GetProperty("name").GetString()!, X.Mat);
            return "np.array([" + string.Join(", ", Enumerable.Range(0, mx.Rows).Select(i => "[" + string.Join(", ", Enumerable.Range(0, mx.Columns).Select(j => Float(mx[i, j]))) + "]")) + "], dtype=np.float64)";
        }

        if (kind == "vector")
        {
            double[] ds = X.Converted(x ?? default, p.GetProperty("name").GetString()!, X.Dbls);
            return "[" + string.Join(", ", ds.Select(Float)) + "]";
        }

        if (kind.StartsWith("tuple:", StringComparison.Ordinal))
        {
            (_, _, List<JsonElement?> xs) = Cells(x);
            return "(" + string.Join(", ", p.GetProperty("items").EnumerateArray().Select((it, j) => Element(it.GetProperty("kind").GetString()!, xs[j], it.GetProperty("cs").GetString()!))) + ",)";
        }

        if (kind.StartsWith("array:tuple:", StringComparison.Ordinal))
        {
            if (IsNull(x))
            {
                return "[]";
            }

            List<JsonElement> its = [.. p.GetProperty("items").EnumerateArray()];
            (int r, int c, List<JsonElement?> xs) = Cells(x);
            var rows = Enumerable.Range(0, r).Select(i => xs.GetRange(i * c, c)).ToList();
            while (rows.Count > 0 && rows[^1].All(Empty))
            {
                rows.RemoveAt(rows.Count - 1);
            }

            return "[" + string.Join(", ", rows.Select(row => "(" + string.Join(", ", its.Select((it, j) => Element(it.GetProperty("kind").GetString()!, row[j], it.GetProperty("cs").GetString()!))) + ",)")) + "]";
        }

        if (kind.StartsWith("array:", StringComparison.Ordinal))
        {
            string ek = kind[6..];
            (_, _, List<JsonElement?> xs) = Cells(x);
            if (!ek.EndsWith('?'))
            {
                while (xs.Count > 0 && Empty(xs[^1]))
                {
                    xs.RemoveAt(xs.Count - 1);
                }
            }

            return "[" + string.Join(", ", xs.Select(v => Element(ek, v, S(p, "el")!))) + "]";
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

            return "[" + string.Join(", ", rows.Select(row => "[" + string.Join(", ", row.Select(v => Element(ek, v, ek))) + "]")) + "]";
        }

        if (kind.StartsWith("map:", StringComparison.Ordinal))
        {
            (int r, _, List<JsonElement?> xs) = Cells(x);
            var pairs = Enumerable.Range(0, r).Where(i => !(Empty(xs[2 * i]) && Empty(xs[(2 * i) + 1])))
                .Select(i => $"{Element(S(p, "elKind")!, xs[2 * i], S(p, "el")!)}: {Element(S(p, "valKind")!, xs[(2 * i) + 1], S(p, "val")!)}");
            return "{" + string.Join(", ", pairs) + "}";
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
                return $"{vars[v!.Value.GetProperty("ref").GetString()!]}[{v.Value.GetProperty("item").GetInt32().ToString(CultureInfo.InvariantCulture)}]";
            }

            if (Has(v, "ref"))
            {
                return vars[v!.Value.GetProperty("ref").GetString()!];
            }

            // A name inside an array of calendars or day counters, as the runtime's Objs<T> takes it (round 7's B7-1).
            if (v is { ValueKind: JsonValueKind.String } name && Named.Contains(cs))
            {
                return Lookup(cs, name.GetString()!);
            }

            return "None";
        }

        return Scalar(ek, v, cs);
    }

    private string Expr(JsonElement f, string kind, List<JsonElement> ps, List<JsonElement?> args, (string Owner, string Member, string Style, List<string> Params) m)
    {
        if (kind == "draws")
        {
            JsonElement? count = Has(args[1], "rows") ? Cells(args[1]).Cells[0] : args[1];
            return $"{vars[args[0]!.Value.GetProperty("ref").GetString()!]}.{m.Member}({X.DrawsCount(count).ToString(CultureInfo.InvariantCulture)})";
        }

        var rendered = new List<string>();
        bool keyword = false;
        string? self = null;
        for (int i = 0; i < ps.Count; i++)
        {
            JsonElement p = ps[i];
            JsonElement? x = i < args.Count ? args[i] : null;
            if (p.GetProperty("self").GetBoolean())
            {
                self = Empty(x) ? "None" : Arg(p, x);
                continue;
            }

            if (IsNull(x) && (p.GetProperty("optional").GetBoolean() || kind is "bind" or "bindon"))
            {
                keyword = true;
                continue;
            }

            string lit = Arg(p, x);
            string name = i < m.Params.Count ? m.Params[i] : p.GetProperty("name").GetString()!;
            rendered.Add(keyword || kind is "bind" or "bindon" ? $"{name}={lit}" : lit);
        }

        string a = string.Join(", ", rendered);
        return m.Style switch
        {
            "new" => $"{m.Owner}({a})",
            "static" => $"{m.Owner}.{m.Member}({a})",
            "property" => $"{self}.{m.Member}",
            "method" => self == "None" ? $"{m.Owner}.{m.Member}({string.Join(", ", new[] { self }.Concat(rendered))})" : $"{self}.{m.Member}({a})",
            _ => throw new ArgumentException($"no Python form for the style {m.Style} ({S(f, "c")})"),
        };
    }

    private void Call(JsonElement c)
    {
        string fn = c.GetProperty("fn").GetString()!, id = c.GetProperty("id").GetString()!;
        string var = "v" + id[1..];
        List<JsonElement?> args = [.. c.GetProperty("args").EnumerateArray().Select(a => (JsonElement?)a)];
        int own = fn switch { "nqlxlDrawsValues" => 0, "nqlxlDrawsWeights" => 1, "nqlxlDrawsNext" => 2, _ => -1 };
        if (own >= 0)
        {
            lines.Add($"{var} = {vars[args[0]!.Value.GetProperty("ref").GetString()!]}[{own.ToString(CultureInfo.InvariantCulture)}]");
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

        var m = Member(f.GetProperty("c").GetString()!);
        List<JsonElement> ps = f.TryGetProperty("params", out JsonElement pa) ? [.. pa.EnumerateArray()] : [];
        int nParams = kind == "draws" ? 2 : ps.Count;
        while (args.Count < nParams)
        {
            args.Add(null);
        }

        // Loop exactly where the add-in's mask loops; a range where one value goes and the mask doesn't loop is the
        // add-in's refusal (round 7's B7-1).
        string mask = S(f, "loops") ?? string.Empty;
        var lifted = Enumerable.Range(0, args.Count).Where(i => i < mask.Length && mask[i] == '1' && Multi(args[i])).ToList();
        var unlooped = Enumerable.Range(0, ps.Count).Where(i => ScalarKind(ps[i].GetProperty("kind").GetString()!) && Multi(args[i]) && !lifted.Contains(i)).ToList();
        if (unlooped.Count > 0)
        {
            lines.Add($"{var} = _raise({Repr($"{fn}: {ps[unlooped[0]].GetProperty("name").GetString()}: a range where one value goes, which the add-in does not loop")})");
            vars[id] = var;
            return;
        }

        if (lifted.Count > 0)
        {
            int rr = lifted.Max(i => args[i]!.Value.GetProperty("rows").GetInt32()), cc = lifted.Max(i => args[i]!.Value.GetProperty("cols").GetInt32());
            var rows = new List<string>();
            for (int i = 0; i < rr; i++)
            {
                var row = new List<string>();
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
                    if (na)
                    {
                        row.Add("_NA()");
                    }
                    else if (err != null)
                    {
                        row.Add($"_Err({err.Value.GetProperty("error").GetInt32().ToString(CultureInfo.InvariantCulture)})");
                    }
                    else
                    {
                        try
                        {
                            row.Add($"_elem(lambda: {Expr(f, kind, ps, ea, m)})");
                        }
                        catch (ArgumentException e)
                        {
                            row.Add($"_elem(lambda: _raise({Repr(e.Message)}))");
                        }
                    }
                }

                rows.Add("[" + string.Join(", ", row) + "]");
            }

            lines.Add($"{var} = [" + string.Join(", ", rows) + "]");
            vars[id] = var;
            return;
        }

        try
        {
            lines.Add($"{var} = {Expr(f, kind, ps, args, m)}");
        }
        catch (XlRefusal e)
        {
            // A value the add-in's conversions refuse: the cell's call failed before nanoql's, so the Python call does.
            lines.Add($"{var} = _raise({Repr($"{fn}: {e.Message}")})");
        }

        vars[id] = var;
    }
}
