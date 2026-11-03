// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// A cell's calls as a vector, §3.2's envelope with the Excel layer's departures: each call its Excel function and the
/// Excel values it was given (a range with its shape and its empty cells, a handle argument a reference to the call
/// that made it, an object inside a returned array its parent call's item), the provider, the calls' date system, the
/// settings' conventions file and every file a call read, embedded; the expected output the cell's own outcome. The
/// refusals: a cell whose value isn't its last call's outcome, an Excel error argument, an object inside a returned map
/// or tuple, a tree whose calls mix date systems, and a tree reporting a read file's path.
/// </summary>
internal sealed class XlVectorWriter
{
    // Readable text: JSON's escapes for quotes, backslashes and control characters only (a file's markup stays as written).
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Dictionary<string, string> ids = new(StringComparer.Ordinal);
    private readonly List<string> calls = [];
    private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> shifts = [];

    /// <summary>The vector's lines for a cell's calls.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="value">The cell's value as Excel shows it.</param>
    /// <returns>The lines.</returns>
    public static List<string> Of(ExcelReference cell, object value)
    {
        string fn;
        object[] args;
        object outcome;
        int shift;
        bool recorded = X.TryCellCall(cell, out XlCellCall call);

        // A spilled cell is its own call (a looped call's anchor shows its first element), checked against the value.
        if (recorded && call.Outcome is object[,] { Length: > 1 } spilled)
        {
            if (!Same(value, spilled[0, 0]))
            {
                throw new ArgumentException("the cell's value isn't its nanoql call's result: export a cell that holds the call alone");
            }

            (fn, args, outcome, shift) = (call.Fn, call.Args, call.Outcome, call.Shift);
        }
        else if (value is string h && X.TryEntry(h, out XlEntry en) && en.Args != null)
        {
            (fn, args, outcome, shift) = (en.Function, en.Args, h, en.Shift);
        }
        else
        {
            if (!recorded)
            {
                throw new ArgumentException("no nanoql call recorded for that cell");
            }

            object shown = value is object[,] sp ? sp[0, 0] : value;
            object mine = call.Outcome is object[,] om ? om[0, 0] : call.Outcome;
            bool same = mine switch
            {
                XlFailure f => shown is ExcelError e && e == f.Code,
                XlPassed => throw new ArgumentException("an argument of the cell's nanoql call is an Excel error: export the cell that gave it"),
                _ => Same(shown, mine),
            };
            if (!same)
            {
                throw new ArgumentException("the cell's value isn't its nanoql call's result: export a cell that holds the call alone");
            }

            (fn, args, outcome, shift) = (call.Fn, call.Args, call.Outcome, call.Shift);
        }

        return new XlVectorWriter().Render(fn, args, outcome, shift);
    }

    private static bool Same(object shown, object mine) => mine switch
    {
        ExcelError ee => shown is ExcelError se && se == ee,
        double d => shown is double dv && BitConverter.DoubleToInt64Bits(d) == BitConverter.DoubleToInt64Bits(dv),
        _ => Equals(shown, mine),
    };

    private static string Str(string s) => JsonSerializer.Serialize(s, Relaxed);

    private List<string> Render(string rootFn, object[] rootArgs, object outcome, int rootShift)
    {
        shifts.Add(rootShift);
        string root = outcome is string rh && X.TryEntry(rh, out XlEntry re) && re.Args != null ? Visit(rootFn, rootArgs, rh) : Visit(rootFn, rootArgs, null);

        // A spilled root's objects carry their read files (a looped reader), as a visited handle's do.
        if (outcome is object[,] spilledRoot)
        {
            foreach (object x in spilledRoot)
            {
                if (x is string eh && X.TryEntry(eh, out XlEntry ee))
                {
                    AddFiles(ee);
                    shifts.Add(ee.Shift);
                }
            }
        }

        if (shifts.Count > 1)
        {
            throw new ArgumentException("the cell's calls were made in workbooks of both date systems (1900 and 1904): export from one workbook's calls");
        }

        // A call reporting a read file's path gives the path the cell read, which a replay, reading the embedded file
        // from a temporary path, can't reproduce; the generated assembly names the reporters.
        if (calls.Any(c => X.PathReporters.Any(n => c.Contains("\"fn\":\"" + n + "\"", StringComparison.Ordinal))))
        {
            throw new ArgumentException("the cell reports a read file's path, which a replay can't reproduce: export a cell that uses the conventions");
        }

        // A handle on a tree that reads a file is expected by its type, since its key holds the path's text. A text is
        // a handle only where the store holds it: text holding "#" stays text.
        bool readsFiles = files.Count > 0;
        string Out(object x) => x switch
        {
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            string s when X.TryEntry(s, out _) => "{\"handle\":" + Str(readsFiles ? s[..s.IndexOf('#', StringComparison.Ordinal)] : s) + "}",
            string s => Str(s),
            ExcelError e => $"{{\"error\":{(int)e}}}",
            _ => "{\"empty\":true}",
        };

        string expected;
        if (outcome is object[,] spilled && spilled.Length > 1)
        {
            var cells = new List<string>();
            foreach (object x in spilled)
            {
                cells.Add(Out(x));
            }

            expected = $"{{ \"value\": {{\"rows\":{spilled.GetLength(0)},\"cols\":{spilled.GetLength(1)},\"cells\":[{string.Join(",", cells)}]}} }}";
        }
        else
        {
            object top = outcome is object[,] tm ? tm[0, 0] : outcome;
            expected = top switch
            {
                XlFailure => "{ \"throws\": true }",
                double d => "{ \"value\": " + d.ToString("R", CultureInfo.InvariantCulture) + " }",
                bool b => "{ \"value\": " + (b ? "true" : "false") + " }",
                string s when X.TryEntry(s, out _) => "{ \"handle\": " + Str(readsFiles ? s[..s.IndexOf('#', StringComparison.Ordinal)] : s) + " }",
                string s => "{ \"value\": " + Str(s) + " }",

                // No value (#N/A) is null; another error Excel shows, a non-finite number's #NUM!, is that error.
                ExcelError xe when xe != ExcelError.ExcelErrorNA => "{ \"value\": {\"error\":" + ((int)xe).ToString(CultureInfo.InvariantCulture) + "} }",
                _ => "{ \"value\": null }",
            };
        }

        int system = shifts.Single() == 1462 ? 1904 : 1900;
        var doc = new List<string>
        {
            "{ \"upstream\": null, \"generator\": \"nanoql-excel " + X.VersionText + "\", \"seed\": null,",
            "  \"cases\": [ { \"name\": " + Str(root) + ",",
            "    \"inputs\": { \"provider\": " + Str(X.ProviderId) + ", \"dateSystem\": " + system.ToString(CultureInfo.InvariantCulture) + ",",
        };
        if (XlSettings.ConventionsBytes is { } conventions)
        {
            doc.Add("      \"conventions\": " + Str(Encoding.UTF8.GetString(conventions)) + ",");
        }

        if (files.Count > 0)
        {
            doc.Add("      \"files\": { " + string.Join(", ", files.Select(kv => Str(kv.Key) + ": " + Str(Encoding.UTF8.GetString(kv.Value)))) + " },");
        }

        doc.Add("      \"calls\": [");
        doc.AddRange(calls.Select((c, i) => "        " + c + (i < calls.Count - 1 ? "," : string.Empty)));
        doc.Add("      ], \"result\": " + Str(root) + " },");
        doc.Add("    \"expected\": " + expected + " } ] }");
        return doc;
    }

    // A call, visited once per handle: its arguments first, so the calls are in the order a replay makes them.
    private string Visit(string fn, object[] args, string? handle)
    {
        if (handle != null && ids.TryGetValue(handle, out string? known))
        {
            return known;
        }

        var parts = args.Select(Value).ToList();
        string id = "v" + (calls.Count + 1).ToString(CultureInfo.InvariantCulture);
        if (handle != null)
        {
            ids[handle] = id;
            if (X.TryEntry(handle, out XlEntry en))
            {
                shifts.Add(en.Shift);
                AddFiles(en);
            }
        }

        calls.Add($"{{\"id\":\"{id}\",\"fn\":\"{fn}\",\"args\":[{string.Join(",", parts)}]}}");
        return id;
    }

    // An object inside a returned array is its parent call's item, the parent visited once.
    private string VisitParent(XlEntry element)
    {
        string synthetic = "(call)" + element.ParentKey;
        if (ids.TryGetValue(synthetic, out string? known))
        {
            return known;
        }

        shifts.Add(element.Shift);
        var parts = element.ParentArgs!.Select(Value).ToList();
        string id = "v" + (calls.Count + 1).ToString(CultureInfo.InvariantCulture);
        ids[synthetic] = id;
        calls.Add($"{{\"id\":\"{id}\",\"fn\":\"{element.ParentFn}\",\"args\":[{string.Join(",", parts)}]}}");
        return id;
    }

    private void AddFiles(XlEntry en)
    {
        foreach (KeyValuePair<string, byte[]> kv in en.Files ?? [])
        {
            files[kv.Key] = kv.Value;
        }
    }

    private string Value(object? a)
    {
        switch (a)
        {
            case string s when X.TryEntry(s, out XlEntry el) && el.Item >= 0 && el.ParentFn != null:
                return "{\"ref\":\"" + VisitParent(el) + "\",\"item\":" + el.Item.ToString(CultureInfo.InvariantCulture) + "}";
            case string s when X.TryEntry(s, out XlEntry en) && en.Args == null:
                throw new ArgumentException("an object inside a returned map or tuple has no export form yet: export the cell that made it");
            case string s when X.TryEntry(s, out XlEntry en):
                return "{\"ref\":\"" + Visit(en.Function, en.Args!, s) + "\"}";
            case object[,] m:
                var cells = new List<string>();
                foreach (object x in m)
                {
                    cells.Add(Value(x));
                }

                return $"{{\"rows\":{m.GetLength(0)},\"cols\":{m.GetLength(1)},\"cells\":[{string.Join(",", cells)}]}}";
            case ExcelMissing:
                return "null";
            case ExcelEmpty:
                return "{\"empty\":true}";
            case ExcelError e:
                return $"{{\"error\":{(int)e}}}";
            case double d:
                return d.ToString("R", CultureInfo.InvariantCulture);
            case bool b:
                return b ? "true" : "false";
            case string s:
                return Str(s);
            default:
                return "null";
        }
    }
}
