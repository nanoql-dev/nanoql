// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ExcelDna.Integration;

namespace NanoQL.Excel.Replay;

/// <summary>
/// One trace's calls through a build's worksheet functions: each line's arguments from the trace's C forms to Excel's
/// values, its Excel result against C#'s recorded one, by kind (docs/excel.md, "The replay").
/// </summary>
/// <param name="enumNames">Each enum's members' names by value.</param>
/// <param name="enumValues">Each enum's members' values by name.</param>
/// <param name="flags">The flags enums.</param>
/// <param name="cell">The calling cell the host reports.</param>
internal sealed class TraceReplay(Dictionary<string, Dictionary<long, string>> enumNames, Dictionary<string, Dictionary<string, long>> enumValues, HashSet<string> flags, ExcelReference cell)
{
    private static readonly int ShowLimit = int.Parse(Environment.GetEnvironmentVariable("NANOQL_REPLAY_SHOW") ?? "40", CultureInfo.InvariantCulture);
    private int shown;

    /// <summary>Gets the trace's objects by id: each one's handle.</summary>
    public Dictionary<long, string> Objs { get; } = [];

    /// <summary>Gets the refusals matched, each the Excel error of its code.</summary>
    public long Refusals { get; private set; }

    /// <summary>Gets the mismatches.</summary>
    public long Mismatches { get; private set; }

    /// <summary>Gets the results matched through Excel's own forms (none as #N/A, a non-finite number as #NUM!).</summary>
    public long FormMatches { get; private set; }

    /// <summary>Gets or sets the trace's line, for messages.</summary>
    public long Line { get; set; }

    /// <summary>Gets or sets the function being replayed, for messages.</summary>
    public string Fn { get; set; } = string.Empty;

    /// <summary>Gets or sets the fact being replayed, for messages.</summary>
    public string FactName { get; set; } = string.Empty;

    /// <summary>Gets the results matched through Excel's own forms, by form.</summary>
    public Dictionary<string, long> Forms { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the mismatches by class: the function, what differed and the Excel error's message.</summary>
    public Dictionary<string, long> MismatchBy { get; } = new(StringComparer.Ordinal);

    private void Mismatch(string what, object? want, object? got)
    {
        Mismatches++;
        string msg = got is ExcelError ? " [" + X.ErrorAt(cell) + "]" : string.Empty;
        string cls = Fn + ": " + what + (got is ExcelError ? ": " + System.Text.RegularExpressions.Regex.Replace(X.ErrorAt(cell), "\"[^\"]*\"|[0-9.E+-]{4,}", "…") : got is null ? ": null" : ": " + got.GetType().Name);
        MismatchBy[cls] = MismatchBy.GetValueOrDefault(cls) + 1;
        if (shown++ < ShowLimit)
        {
            Console.WriteLine($"MISMATCH line {Line} {FactName} {Fn}: {what}: recorded {want}, Excel {Show(got)}{msg}");
        }
    }

    private void Form(string what)
    {
        FormMatches++;
        Forms[what] = Forms.GetValueOrDefault(what) + 1;
    }

    private static string Show(object? o) => o switch
    {
        null => "null",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        object[,] m => $"a {m.GetLength(0)}x{m.GetLength(1)} range [{string.Join(", ", m.Cast<object>().Take(6).Select(Show))}]",
        _ => o.ToString() ?? "",
    };

    /// <summary>A double from its 16 hexadecimal digits.</summary>
    /// <param name="v">The trace's value.</param>
    /// <returns>The double.</returns>
    public static double Hex(JsonElement v) => BitConverter.Int64BitsToDouble(long.Parse(v.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static string Head(string kind) => kind.Split(':')[0];

    private static string Rest(string kind) => kind.Contains(':') ? kind[(kind.IndexOf(':') + 1)..] : string.Empty;

    /// <summary>Replays one line: its call through the worksheet function, its result checked.</summary>
    /// <param name="fn">The C function's surface entry.</param>
    /// <param name="m">The worksheet function.</param>
    /// <param name="typeArgs">A family's construction's type arguments by name, or none.</param>
    /// <param name="rec">The trace's line.</param>
    public void Call(JsonElement fn, MethodInfo m, string[] typeArgs, JsonElement rec)
    {
        var ps = fn.GetProperty("params").EnumerateArray().ToList();
        var xargs = new object[m.GetParameters().Length];
        for (int i = 0; i < xargs.Length; i++) xargs[i] = ExcelMissing.Value;
        for (int i = 0; i < typeArgs.Length; i++) xargs[i] = typeArgs[i];
        var a = rec.TryGetProperty("a", out var aa) && aa.ValueKind == JsonValueKind.Array ? aa.EnumerateArray().ToList() : [];
        for (int i = 0; i < a.Count && i < ps.Count; i++)
        {
            xargs[typeArgs.Length + i] = In(ps[i], ps[i].GetProperty("kind").GetString()!, a[i]);
        }

        object got = m.Invoke(null, xargs)!;
        int status = rec.TryGetProperty("t", out var t) ? t.GetInt32() : 0;
        if (status != 0)
        {
            var want = X.ExcelOf(status);
            if (got is ExcelError e && e == want) Refusals++;
            else Mismatch("a refusal", $"code {status} ({want})", got);
            return;
        }

        var res = rec.TryGetProperty("r", out var rr) ? rr : default;
        if (!fn.TryGetProperty("returns", out var ret) || ret.ValueKind == JsonValueKind.Null)
        {
            if (got is not "OK") Mismatch("a call returning nothing", "OK", got);
            return;
        }

        Check(ret, ret.GetProperty("kind").GetString()!, res, got);
    }

    // ---- arguments: the trace's C form to an Excel value ----
    private object In(JsonElement p, string kind, JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("x", out _)) throw new StopException("an input the trace couldn't write");
        if (v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            // XLR2: an argument left out takes its default; where the default isn't none, no cell gives none
            if (p.TryGetProperty("optional", out var opt) && opt.ValueKind == JsonValueKind.True && p.TryGetProperty("default", out var def) && def.ValueKind != JsonValueKind.Null)
                throw new StopException("none where the argument's default isn't none, which no cell gives (XLR2)");
            return ExcelMissing.Value;
        }

        string head = Head(kind);
        switch (kind)
        {
            case "date?" when v.GetInt32() == 0:
                return ExcelMissing.Value;
            case "matrix":
            {
                int rows = v.GetProperty("r").GetInt32(), cols = v.GetProperty("c").GetInt32();
                if (rows == 0 || cols == 0) throw new StopException("an empty matrix, which no range gives");
                var vals = v.GetProperty("v").EnumerateArray().ToList();
                var o = new object[rows, cols];
                for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) o[i, j] = Num(Hex(vals[(i * cols) + j]));
                return o;
            }

            case "vector":
                return Column(v.EnumerateArray().Select(x => (object)Num(Hex(x))).ToList());
        }

        switch (head)
        {
            case "array":
            {
                string ek = Rest(kind);
                string? et = p.TryGetProperty("element", out var el) ? el.GetString() : null;
                if (ek.StartsWith("tuple", StringComparison.Ordinal))
                {
                    var rows = v.EnumerateArray().ToList();
                    if (rows.Count == 0) return ExcelMissing.Value;
                    var items = Items(Rest(ek));
                    var o = new object[rows.Count, items.Count];
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var it = rows[i].EnumerateArray().ToList();
                        for (int j = 0; j < items.Count; j++) o[i, j] = Cell(Scalar(items[j], null, it[j]));
                    }

                    return o;
                }

                return Column(v.EnumerateArray().Select(x => Cell(Scalar(ek, et, x))).ToList());
            }

            case "jagged":
            {
                var rows = v.EnumerateArray().Select(r => r.EnumerateArray().Select(x => Num(Hex(x))).ToList()).ToList();
                if (rows.Count == 0) return ExcelMissing.Value;
                int w = Math.Max(1, rows.Max(r => r.Count));
                var o = new object[rows.Count, w];
                for (int i = 0; i < rows.Count; i++) for (int j = 0; j < w; j++) o[i, j] = j < rows[i].Count ? rows[i][j] : ExcelEmpty.Value;
                return o;
            }

            case "map":
            {
                var parts = Rest(kind).Split(':');
                var pairs = v.EnumerateArray().ToList();
                if (pairs.Count == 0) return ExcelMissing.Value;
                string? kt = p.TryGetProperty("element", out var ke) ? ke.GetString() : null;
                string? vt = p.TryGetProperty("value", out var ve) ? ve.GetString() : null;
                var o = new object[pairs.Count, 2];
                for (int i = 0; i < pairs.Count; i++)
                {
                    o[i, 0] = Cell(Scalar(parts[0], kt, pairs[i][0]));
                    o[i, 1] = Cell(Scalar(parts[1], vt, pairs[i][1]));
                }

                return o;
            }

            case "tuple":
            {
                var items = Items(Rest(kind));
                var it = v.EnumerateArray().ToList();
                var o = new object[1, items.Count];
                for (int j = 0; j < items.Count; j++) o[0, j] = Cell(Scalar(items[j], null, it[j]));
                return o;
            }

            case "span":
            case "vector&":
            case "matrix&":
                throw new StopException("a writable argument, which Excel withholds");
        }

        return Scalar(kind, p.TryGetProperty("type", out var ty) ? ty.GetString() : null, v);
    }

    private static List<string> Items(string list) => [.. list.Split(',')];

    private static object Cell(object o) => o is ExcelMissing ? ExcelEmpty.Value : o;

    private static object Column(List<object> items)
    {
        if (items.Count == 0) return ExcelMissing.Value;
        var o = new object[items.Count, 1];
        for (int i = 0; i < items.Count; i++) o[i, 0] = items[i];
        return o;
    }

    private static double Num(double d) => double.IsFinite(d) ? d : throw new StopException("a non-finite number, which a cell can't hold");

    private object Scalar(string kind, string? type, JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("x", out _)) throw new StopException("an input the trace couldn't write");
        if (v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return ExcelMissing.Value;
        switch (kind.TrimEnd('?'))
        {
            case "double":
                return Num(Hex(v));
            case "int32":
            case "int64":
            {
                // A cell holds a double: a whole number past 2^53 reaches it rounded, so no cell gives it.
                long whole = v.GetInt64();
                if (whole is > 9007199254740992L or < -9007199254740992L)
                {
                    throw new StopException("a whole number past 2^53, which no cell holds exactly");
                }

                return (double)whole;
            }

            case "bool":
                return v.GetBoolean();
            case "string":
                return v.GetString()!;
            case "date":
                if (kind == "date?" && v.GetInt32() == 0) return ExcelMissing.Value;
                return (double)v.GetInt32();
            case "period":
            {
                var per = new NanoQL.Time.Period(v[0].GetInt32(), (NanoQL.Time.TimeUnit)v[1].GetInt32());
                string s = NanoQL.Time.PeriodFunctions.ShortPeriod(per);
                NanoQL.Time.Period back;
                try { back = NanoQL.Utilities.PeriodParser.Parse(s); }
                catch (Exception) { throw new StopException("a period whose text the parser doesn't read"); }
                if (back.Length != per.Length || back.Units != per.Units) throw new StopException("a period whose text reads back as another");
                return s;
            }

            case "complex":
                return X.CplxText(new Complex(Hex(v[0]), Hex(v[1])));
            case "enum":
            {
                string et = type!.Replace("System.Nullable`1<", string.Empty, StringComparison.Ordinal).TrimEnd('>');
                long val = v.GetInt64();
                if (enumNames[et].TryGetValue(val, out var name)) return name;
                if (flags.Contains(et))
                {
                    var parts = enumNames[et].Where(kv => kv.Key != 0 && (val & kv.Key) == kv.Key).ToList();
                    if (parts.Aggregate(0L, (s, kv) => s | kv.Key) == val) return string.Join(", ", parts.Select(kv => kv.Value));
                }

                throw new StopException("an enum value no member has");
            }

            case "handle":
            case "boxed":
            case "snapshot":
            case "engine":
            {
                long id = v.GetInt64();
                return Objs.TryGetValue(id, out var h) ? h : throw new StopException("an object the replay didn't make");
            }
        }

        throw new StopException("a kind the replayer has no form for: " + kind);
    }

    // ---- results: Excel's against C#'s ----
    private void Check(JsonElement ret, string kind, JsonElement res, object got)
    {
        bool none = res.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        string head = Head(kind);
        if (none && kind is not ("date?"))
        {
            if (got is ExcelError { } e0 && e0 == ExcelError.ExcelErrorNA) Form("nothing as #N/A");
            else Mismatch("nothing", "null", got);
            return;
        }

        switch (kind)
        {
            case "matrix":
            {
                int rows = res.GetProperty("r").GetInt32(), cols = res.GetProperty("c").GetInt32();
                var vals = res.GetProperty("v").EnumerateArray().ToList();
                if (rows * cols == 0)
                {
                    if (got is ExcelError) Form("an empty matrix as an error"); else Mismatch("an empty matrix", "0 cells", got);
                    return;
                }

                if (got is not object[,] g || g.GetLength(0) != rows || g.GetLength(1) != cols)
                {
                    Mismatch("a matrix's shape", $"{rows}x{cols}", got);
                    return;
                }

                for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) if (!SameDouble(Hex(vals[(i * cols) + j]), g[i, j])) { Mismatch("a matrix's value", Hex(vals[(i * cols) + j]), g[i, j]); return; }
                return;
            }

            case "vector":
                CheckList(res.EnumerateArray().ToList(), got, (x, g) => SameDouble(Hex(x), g));
                return;
            case "distribution":
            {
                var cols = res.EnumerateArray().Select(c => c.EnumerateArray().ToList()).ToList();
                int k = cols[0].Count;
                if (got is not object[,] g || g.GetLength(0) != k + 1 || g.GetLength(1) != 6) { Mismatch("a distribution's shape", $"{k + 1}x6", got); return; }
                for (int j = 0; j < 6; j++) for (int i = 0; i < k; i++) if (!SameDouble(Hex(cols[j][i]), g[i + 1, j])) { Mismatch("a distribution's value", Hex(cols[j][i]), g[i + 1, j]); return; }
                return;
            }

            case "draws":
            case "sequences":
            {
                if (got is not string h) { Mismatch("draws", "a handle", got); return; }
                var d = X.Obj<XlDraws>(h, "r");
                var parts = res.EnumerateArray().ToList();
                var wv = d.Weights.ToArray();
                var ww = parts[1].EnumerateArray().Select(Hex).ToList();
                if (wv.Length != ww.Count || !ww.Select((x, i) => SameDouble(x, wv[i])).All(b => b)) { Mismatch("draws' weights", ww.Count, wv.Length); return; }
                if (kind == "draws")
                {
                    var vv = (double[])d.Values;
                    var want = parts[0].EnumerateArray().Select(Hex).ToList();
                    if (vv.Length != want.Count || !want.Select((x, i) => SameDouble(x, vv[i])).All(b => b)) { Mismatch("draws' values", want.Count, vv.Length); return; }
                }
                else
                {
                    // the trace writes a batch of sequences' values flat, draw by draw, then the dimension
                    var vv = (double[,])d.Values;
                    var want = parts[0].EnumerateArray().Select(Hex).ToList();
                    int dim = parts[2].GetInt32();
                    if (dim != vv.GetLength(1) || want.Count != vv.Length) { Mismatch("sequences' shape", $"{want.Count} values of dimension {dim}", $"{vv.GetLength(0)}x{vv.GetLength(1)}"); return; }
                    for (int i = 0; i < want.Count; i++) if (!SameDouble(want[i], vv[i / dim, i % dim])) { Mismatch("sequences' values", want[i], vv[i / dim, i % dim]); return; }
                }

                // the next snapshot through nqlxlDrawsNext, as a cell gets it
                var next = parts[^1];
                Objs[next.TryGetProperty("n", out var nn) ? nn.GetInt64() : next.GetProperty("s").GetInt64()] = Handle(OwnFunctions.DrawsNext(h));
                return;
            }
        }

        switch (head)
        {
            case "array":
            {
                string ek = Rest(kind);
                string? et = ret.TryGetProperty("element", out var el) ? el.GetString() : null;
                var want = res.EnumerateArray().ToList();
                if (ek.StartsWith("tuple", StringComparison.Ordinal))
                {
                    var items = Items(Rest(ek));
                    if (want.Count == 0) { if (got is ExcelError { } e1 && e1 == ExcelError.ExcelErrorNA) Form("an empty collection as #N/A"); else Mismatch("an empty collection", "#N/A", got); return; }
                    if (got is not object[,] g || g.GetLength(0) != want.Count || g.GetLength(1) != items.Count) { Mismatch("a collection of tuples' shape", $"{want.Count}x{items.Count}", got); return; }
                    for (int i = 0; i < want.Count; i++)
                    {
                        if (want[i].ValueKind == JsonValueKind.Null)
                        {
                            for (int j = 0; j < items.Count; j++) if (g[i, j] is not ExcelError { } en || en != ExcelError.ExcelErrorNA) { Mismatch("a null tuple's row", "#N/A", g[i, j]); return; }
                            continue;
                        }

                        var it = want[i].EnumerateArray().ToList();
                        for (int j = 0; j < items.Count; j++) if (!CheckScalar(items[j], null, it[j], g[i, j])) return;
                    }

                    return;
                }

                CheckList(want, got, (x, g) => CheckScalar(ek, et, x, g, quiet: true));
                return;
            }

            case "jagged":
            {
                var rows = res.EnumerateArray().Select(r => r.EnumerateArray().Select(Hex).ToList()).ToList();
                if (rows.Count == 0) { if (got is ExcelError) Form("an empty collection as #N/A"); else Mismatch("an empty jagged array", "#N/A", got); return; }
                int w = Math.Max(1, rows.Max(r => r.Count));
                if (got is not object[,] g || g.GetLength(0) != rows.Count || g.GetLength(1) != w) { Mismatch("a jagged array's shape", $"{rows.Count}x{w}", got); return; }
                for (int i = 0; i < rows.Count; i++)
                    for (int j = 0; j < w; j++)
                    {
                        bool ok = j < rows[i].Count ? SameDouble(rows[i][j], g[i, j]) : g[i, j] is ExcelError { } en && en == ExcelError.ExcelErrorNA;
                        if (!ok) { Mismatch("a jagged array's cell", j < rows[i].Count ? rows[i][j] : "#N/A", g[i, j]); return; }
                    }

                return;
            }

            case "map":
            {
                var parts = Rest(kind).Split(':');
                var pairs = res.EnumerateArray().ToList();
                string? kt = ret.TryGetProperty("element", out var ke) ? ke.GetString() : null;
                string? vt = ret.TryGetProperty("value", out var ve) ? ve.GetString() : null;
                if (pairs.Count == 0) { if (got is ExcelError { } e2 && e2 == ExcelError.ExcelErrorNA) Form("an empty collection as #N/A"); else Mismatch("an empty map", "#N/A", got); return; }
                if (got is not object[,] g || g.GetLength(0) != pairs.Count || g.GetLength(1) != 2) { Mismatch("a map's shape", $"{pairs.Count}x2", got); return; }
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (!CheckScalar(parts[0], kt, pairs[i][0], g[i, 0])) return;
                    if (!CheckScalar(parts[1], vt, pairs[i][1], g[i, 1])) return;
                }

                return;
            }

            case "tuple":
            {
                var items = Items(Rest(kind).Replace("array:", "array#", StringComparison.Ordinal)).Select(s => s.Replace("array#", "array:", StringComparison.Ordinal)).ToList();
                var it = res.EnumerateArray().ToList();
                int rows = Math.Max(1, items.Select((k, j) => Head(k) == "array" ? it[j].GetArrayLength() : 1).Max());
                if (got is not object[,] g || g.GetLength(1) != items.Count || g.GetLength(0) != rows) { Mismatch("a tuple's shape", $"{rows}x{items.Count}", got); return; }
                for (int j = 0; j < items.Count; j++)
                {
                    if (Head(items[j]) == "array")
                    {
                        var col = it[j].EnumerateArray().ToList();
                        for (int i = 0; i < rows; i++)
                        {
                            bool ok = i < col.Count ? CheckScalar(Rest(items[j]), null, col[i], g[i, j]) : g[i, j] is ExcelError { } en && en == ExcelError.ExcelErrorNA;
                            if (!ok) { if (i >= col.Count) Mismatch("a tuple's padding", "#N/A", g[i, j]); return; }
                        }
                    }
                    else if (!CheckScalar(items[j], null, it[j], g[0, j]))
                    {
                        return;
                    }
                }

                return;
            }
        }

        CheckScalar(kind, ret.TryGetProperty("type", out var ty) ? ty.GetString() : null, res, got);
    }

    private void CheckList(List<JsonElement> want, object got, Func<JsonElement, object, bool> same)
    {
        if (want.Count == 0)
        {
            if (got is ExcelError { } e && e == ExcelError.ExcelErrorNA) Form("an empty collection as #N/A");
            else Mismatch("an empty collection", "#N/A", got);
            return;
        }

        if (got is not object[,] g || g.GetLength(0) != want.Count || g.GetLength(1) != 1)
        {
            Mismatch("a collection's shape", $"{want.Count}x1", got);
            return;
        }

        for (int i = 0; i < want.Count; i++)
        {
            if (!same(want[i], g[i, 0]))
            {
                Mismatch($"a collection's element {i}", want[i].ToString(), g[i, 0]);
                return;
            }
        }
    }

    private bool SameDouble(double want, object got)
    {
        if (got is double g) return BitConverter.DoubleToInt64Bits(g) == BitConverter.DoubleToInt64Bits(want) || (double.IsNaN(g) && double.IsNaN(want));
        if (!double.IsFinite(want) && got is ExcelError { } e && e == ExcelError.ExcelErrorNum)
        {
            Form("a non-finite number as #NUM!");
            return true;
        }

        return false;
    }

    private bool CheckScalar(string kind, string? type, JsonElement res, object got, bool quiet = false)
    {
        bool ok = CheckOne(kind, type, res, got, out string? what);
        if (!ok && !quiet) Mismatch(what ?? kind, res.ToString(), got);
        return ok;
    }

    private bool CheckOne(string kind, string? type, JsonElement res, object got, out string? what)
    {
        what = null;
        bool none = res.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        if (none || (kind == "date?" && res.ValueKind == JsonValueKind.Number && res.GetInt32() == 0))
        {
            if (got is ExcelError { } e && e == ExcelError.ExcelErrorNA) { Form("nothing as #N/A"); return true; }
            if (kind == "date?" && got is double z && z == 0) return true;
            what = "nothing";
            return false;
        }

        switch (kind.TrimEnd('?'))
        {
            case "double":
                what = "a double";
                return SameDouble(Hex(res), got);
            case "int32":
            case "int64":
                what = "an integer";
                return got is double d && d == res.GetDouble();
            case "bool":
                what = "a boolean";
                return got is bool b && b == res.GetBoolean();
            case "string":
                what = "a string";
                return got is string s && s == res.GetString();
            case "date":
                what = "a date";
                return got is double dd && dd == res.GetInt32();
            case "period":
            {
                // a period by the text a cell shows (a sub-day unit's, "1h", reads back only as text)
                what = "a period";
                return got is string ps && ps == NanoQL.Time.PeriodFunctions.ShortPeriod(new NanoQL.Time.Period(res[0].GetInt32(), (NanoQL.Time.TimeUnit)res[1].GetInt32()));
            }

            case "complex":
            {
                what = "a complex number";
                Complex c;
                try { c = X.Cplx(got, "r"); }
                catch (ArgumentException) { return false; }
                return SameDouble(Hex(res[0]), c.Real) && SameDouble(Hex(res[1]), c.Imaginary);
            }

            case "enum":
            {
                what = "an enum";
                string et = type!.Replace("System.Nullable`1<", string.Empty, StringComparison.Ordinal).TrimEnd('>');
                if (got is not string name) return false;
                long v = 0;
                foreach (string part in name.Split(", "))
                {
                    if (!enumValues[et].TryGetValue(part, out long pv)) return false;
                    v |= pv;
                }

                return v == res.GetInt64();
            }

            case "handle":
            case "boxed":
            case "snapshot":
            case "new":
                what = "an object";
                return CheckObj(res, got);
        }

        throw new StopException("a result kind the replayer has no check for: " + kind);
    }

    private static object Resolve(string handle)
    {
        object o = X.Obj<object>(handle, "r");
        return o is XlSnapshot s ? s.Generator : o;
    }

    private string Handle(object o) => o is string s ? s : throw new StopException("an object with no handle");

    private static string TypeKey(string name) => System.Text.RegularExpressions.Regex.Replace(name, "<[^>]*>F[0-9A-Fa-f]+__", string.Empty);

    private bool CheckObj(JsonElement res, object got)
    {
        if (got is not string h || !X.IsHandle(h)) return false;
        if (res.TryGetProperty("n", out var n))
        {
            if (res.TryGetProperty("y", out var y))
            {
                var o = Resolve(h);
                if (TypeKey(o.GetType().FullName!) != TypeKey(y.GetString()!)) return false;
            }

            Objs[n.GetInt64()] = h;
            return true;
        }

        long id = res.GetProperty("s").GetInt64();
        if (!Objs.TryGetValue(id, out var earlier) || !X.IsHandle(earlier))
        {
            Objs[id] = h;
            return true;
        }

        if (ReferenceEquals(Resolve(h), Resolve(earlier))) return true;
        Form("the same object as another handle's equal one");
        Objs[id] = h;
        return true;
    }
}
