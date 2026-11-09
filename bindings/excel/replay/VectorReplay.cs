// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ExcelDna.Integration;
using NanoQL.Extensions.Conventions;

namespace NanoQL.Excel.Replay;

/// <summary>
/// The repro vectors' replay: each vector exported from a cell, replayed through the build's worksheet functions on its
/// own, as the cell ran them: a fresh store, the provider's identity and conventions as recorded, its date system, its
/// files in temporary paths removed after, the process's state restored. Each number is compared under the tolerance,
/// and its bits counted; a failed call's vector expects an Excel error, a handle its whole text, or its type where the
/// calls read a file.
/// </summary>
internal sealed class VectorReplay
{
    private const double Relative = 1e-10;

    private readonly Dictionary<string, MethodInfo> methods;
    private readonly ReplayHost host;
    private int numbers;
    private int identical;

    private VectorReplay(Dictionary<string, MethodInfo> methods, ReplayHost host)
    {
        this.methods = methods;
        this.host = host;
    }

    /// <summary>Replays a folder's vectors through a build's functions.</summary>
    /// <param name="folder">The vectors' folder.</param>
    /// <param name="build">The build's folder, an <c>excel build</c>'s output.</param>
    /// <param name="reverse">Whether to replay them in reverse order.</param>
    /// <param name="shared">Whether to leave the state shared between vectors (the isolation's deliberate red).</param>
    /// <returns>The exit code: 0 when every vector replays to its outcome.</returns>
    public static int Run(string folder, string build, bool reverse, bool shared)
    {
        var host = new ReplayHost();
        X.Host = host;
        Assembly functions = Assembly.LoadFrom(Path.GetFullPath(Path.Combine(build, "bin", "Release", "net10.0-windows", "NanoQL.Excel.Functions.dll")));
        var methods = functions.GetExportedTypes().Concat([typeof(OwnFunctions)])
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<ExcelFunctionAttribute>())).Where(x => x.Attribute != null)
            .ToDictionary(x => x.Attribute!.Name, x => x.Method, StringComparer.Ordinal);
        var replay = new VectorReplay(methods, host);
        string[] files = [.. Directory.GetFiles(folder, "*.json").OrderBy(p => p, StringComparer.Ordinal)];
        if (reverse)
        {
            Array.Reverse(files);
        }

        int failed = 0;
        foreach (string file in files)
        {
            (bool ok, string detail) = replay.One(file, !shared);
            failed += ok ? 0 : 1;
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {Path.GetFileName(file)}: {detail}");
        }

        Console.WriteLine($"Excel vectors{(reverse ? ", in reverse" : string.Empty)}{(shared ? ", the state shared" : string.Empty)}: {files.Length - failed} of {files.Length} replayed to their outcome; "
            + $"numbers within the tolerance {replay.numbers}, bit for bit {replay.identical} of {replay.numbers}");
        Console.WriteLine(ReplayHost.ShownThreadSafeLine());
        return failed == 0 && ReplayHost.ShownThreadSafe.Count == 0 ? 0 : 1;
    }

    private static bool Within(double expected, double actual)
        => Math.Abs(expected - actual) <= Relative * Math.Max(Math.Max(Math.Abs(expected), Math.Abs(actual)), 1.0);

    // A handle's whole text is its key, so equal texts mean every input replayed; a type alone where a file was read.
    private static bool Handle(string got, string want) => want.Contains('#', StringComparison.Ordinal) ? got == want : got.StartsWith(want + "#", StringComparison.Ordinal);

    private (bool Ok, string Detail) One(string file, bool isolate)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
        JsonElement c = doc.RootElement.GetProperty("cases")[0];
        JsonElement inputs = c.GetProperty("inputs");
        ConventionProvider savedDefault = ConventionProvider.Default;
        string savedId = X.ProviderId;
        int savedShift = host.Shift;
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (isolate)
            {
                X.Clear();
                X.ProviderId = inputs.GetProperty("provider").GetString()!;
            }

            host.Shift = inputs.GetProperty("dateSystem").GetInt32() == 1904 ? 1462 : 0;
            if (inputs.TryGetProperty("conventions", out JsonElement conventions))
            {
                ConventionProvider.Default = ConventionSet.QuantLib.With(ConventionsXml.Read(new MemoryStream(Encoding.UTF8.GetBytes(conventions.GetString()!)), "embedded"));
            }

            if (inputs.TryGetProperty("files", out JsonElement fs))
            {
                foreach (JsonProperty f in fs.EnumerateObject())
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "nqlxl-replay-" + Guid.NewGuid().ToString("N") + ".xml");
                    File.WriteAllText(tmp, f.Value.GetString()!);
                    paths[f.Name] = tmp;
                }
            }

            var made = new Dictionary<string, object>(StringComparer.Ordinal);
            object Arg(JsonElement e) => e.ValueKind switch
            {
                JsonValueKind.Null => ExcelMissing.Value,
                JsonValueKind.Number => e.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => paths.TryGetValue(e.GetString()!, out string? tp) ? tp : e.GetString()!,

                // An object inside a returned array: its parent call's spill at the item's row.
                JsonValueKind.Object when e.TryGetProperty("item", out JsonElement it) => made[e.GetProperty("ref").GetString()!] is object[,] sm
                    ? (it.GetInt32() < sm.GetLength(0) ? sm[it.GetInt32(), 0] : ExcelError.ExcelErrorRef) : ExcelError.ExcelErrorRef,
                JsonValueKind.Object when e.TryGetProperty("ref", out JsonElement rf) => made[rf.GetString()!],
                JsonValueKind.Object when e.TryGetProperty("empty", out _) => ExcelEmpty.Value,
                JsonValueKind.Object when e.TryGetProperty("error", out JsonElement er) => (ExcelError)er.GetInt32(),
                JsonValueKind.Object when e.TryGetProperty("rows", out JsonElement rows) => Range(e, rows.GetInt32(), e.GetProperty("cols").GetInt32()),
                _ => throw new InvalidDataException("an argument the replayer doesn't read: " + e),
            };
            object Range(JsonElement e, int rows, int cols)
            {
                var o = new object[rows, cols];
                var cells = e.GetProperty("cells").EnumerateArray().ToList();
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        o[i, j] = Arg(cells[(i * cols) + j]);
                    }
                }

                return o;
            }

            object? last = null;
            int n = 0;
            foreach (JsonElement call in inputs.GetProperty("calls").EnumerateArray())
            {
                string fn = call.GetProperty("fn").GetString()!;
                MethodInfo m = methods.TryGetValue(fn, out MethodInfo? found) ? found : throw new InvalidDataException($"the build has no {fn}");
                object[] given = [.. call.GetProperty("args").EnumerateArray().Select(Arg)];
                object[] all = [.. given.Concat(Enumerable.Repeat<object>(ExcelMissing.Value, Math.Max(0, m.GetParameters().Length - given.Length)))];
                last = ReplayHost.Invoke(m, all);
                made[call.GetProperty("id").GetString()!] = last!;
                n++;
            }

            JsonElement exp = c.GetProperty("expected");
            object? top = last is object[,] lm ? lm[0, 0] : last;
            bool ok = exp.TryGetProperty("value", out JsonElement wv) && wv.ValueKind == JsonValueKind.Object && wv.TryGetProperty("rows", out JsonElement wr)
                ? last is object[,] gm && gm.GetLength(0) == wr.GetInt32() && gm.GetLength(1) == wv.GetProperty("cols").GetInt32()
                  && wv.GetProperty("cells").EnumerateArray().Select((w, k) => Same(gm[k / gm.GetLength(1), k % gm.GetLength(1)], w)).ToList().All(b => b)
                : exp.TryGetProperty("throws", out _) ? top is ExcelError
                : exp.TryGetProperty("handle", out JsonElement ht) ? top is string hs && Handle(hs, ht.GetString()!)
                : Same(top, exp.GetProperty("value"));
            string where = $"{n.ToString(CultureInfo.InvariantCulture)} calls, date system {inputs.GetProperty("dateSystem")}, conventions {(inputs.TryGetProperty("conventions", out _) ? "embedded" : "QuantLib's")}, files {paths.Count.ToString(CultureInfo.InvariantCulture)}";
            return (ok, ok ? where : $"{where}: got {Show(last)}, expected {exp.GetRawText()}");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return (false, $"the replayer failed: {(e is TargetInvocationException { InnerException: { } i } ? i : e).GetType().Name}: {e.Message}");
        }
        finally
        {
            foreach (string p in paths.Values)
            {
                File.Delete(p);
            }

            if (isolate)
            {
                ConventionProvider.Default = savedDefault;
                X.ProviderId = savedId;
                host.Shift = savedShift;
            }
        }
    }

    private static string Show(object? o) => o is object[,] m ? $"a {m.GetLength(0)}x{m.GetLength(1)} spill starting {m[0, 0]}" : $"{o}";

    private bool Same(object? got, JsonElement want)
    {
        switch (want.ValueKind)
        {
            case JsonValueKind.Number:
                if (got is not double gd)
                {
                    return false;
                }

                numbers++;
                identical += BitConverter.DoubleToInt64Bits(gd) == BitConverter.DoubleToInt64Bits(want.GetDouble()) ? 1 : 0;
                return Within(want.GetDouble(), gd);
            case JsonValueKind.True or JsonValueKind.False:
                return got is bool gb && gb == want.GetBoolean();
            case JsonValueKind.String:
                return got is string gs && gs == want.GetString();
            case JsonValueKind.Object when want.TryGetProperty("handle", out JsonElement wh):
                return got is string hs && Handle(hs, wh.GetString()!);
            case JsonValueKind.Object when want.TryGetProperty("error", out JsonElement we):
                return got is ExcelError ge && (int)ge == we.GetInt32();
            case JsonValueKind.Object when want.TryGetProperty("empty", out _):
                return got is null or ExcelEmpty;

            // No value, which Excel shows as #N/A.
            case JsonValueKind.Null:
                return got is null || (got is ExcelError gn && gn == ExcelError.ExcelErrorNA);
            default:
                return false;
        }
    }
}
