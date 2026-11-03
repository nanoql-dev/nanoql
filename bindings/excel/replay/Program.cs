// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ExcelDna.Integration;
using NanoQL.Excel;
using NanoQL.Excel.Replay;

// The Excel replayer (docs/excel.md): the vector suite's recorded trace (tools/replay's recorder) through a build's
// generated worksheet functions, outside Excel. Each line's C function is called through its worksheet function (a
// family's construction with its type arguments by name), its arguments in Excel's forms, its Excel result against
// C#'s: doubles by their bits (a non-finite one as #NUM!, which a cell shows), objects by their type, none as #N/A,
// refusals by the Excel error for their code. A fact replays up to its first call the build doesn't have or whose input
// no cell gives, as the bindings' replayers do; the objects stay across facts, as theirs do. Passes at no mismatch and,
// given a floor, at or above its calls with refusals matched. Its vectors mode replays the repro vectors cells export.
// Usage: NanoQL.Excel.Replay <trace.jsonl> <build folder> [--surface <surface.json>] [--report <facts.tsv>] [--floor
// <replay-floor.json>] NanoQL.Excel.Replay vectors <vector folder> <build folder> [--reverse] [--shared]
if (args.Length >= 3 && args[0] == "vectors")
{
    return VectorReplay.Run(args[1], args[2], args.Contains("--reverse"), args.Contains("--shared"));
}

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: NanoQL.Excel.Replay <trace.jsonl> <build folder> [--surface <surface.json>] [--report <facts.tsv>] [--floor <replay-floor.json>]");
    return 2;
}

string trace = args[0], build = args[1];
string? surfacePath = null, report = null, floorPath = null;
for (int i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--surface": surfacePath = args[++i]; break;
        case "--report": report = args[++i]; break;
        case "--floor": floorPath = args[++i]; break;
        default: Console.Error.WriteLine($"unknown argument '{args[i]}'"); return 2;
    }
}

surfacePath ??= Path.Combine(Repository(), "bindings", "surface.json");
var host = new ReplayHost();
X.Host = host;

JsonElement surface = JsonDocument.Parse(File.ReadAllText(surfacePath)).RootElement;
var fns = surface.GetProperty("functions").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
var enumNames = new Dictionary<string, Dictionary<long, string>>(StringComparer.Ordinal);
var enumValues = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
var flags = new HashSet<string>(StringComparer.Ordinal);
foreach (JsonElement e in surface.GetProperty("enums").EnumerateArray())
{
    string n = e.GetProperty("name").GetString()!;
    var byValue = new Dictionary<long, string>();
    var byName = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    foreach (JsonElement m in e.GetProperty("members").EnumerateArray())
    {
        byValue.TryAdd(m[1].GetInt64(), m[0].GetString()!);
        byName[m[0].GetString()!] = m[1].GetInt64();
    }

    enumNames[n] = byValue;
    enumValues[n] = byName;
    if (e.TryGetProperty("flags", out JsonElement fl) && fl.ValueKind == JsonValueKind.True)
    {
        flags.Add(n);
    }
}

// The build's worksheet functions by their Excel names, from its assembly, loaded by path.
Assembly functions = Assembly.LoadFrom(Path.GetFullPath(Path.Combine(build, "bin", "Release", "net10.0-windows", "NanoQL.Excel.Functions.dll")));
var methods = functions.GetExportedTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
    .Select(m => (Method: m, Attribute: m.GetCustomAttribute<ExcelFunctionAttribute>())).Where(x => x.Attribute != null)
    .ToDictionary(x => x.Attribute!.Name, x => x.Method, StringComparer.Ordinal);
var targets = new Dictionary<string, (MethodInfo Method, string Excel, string[] TypeArgs)>(StringComparer.Ordinal);
foreach (JsonElement f in JsonDocument.Parse(File.ReadAllText(Path.Combine(build, "functions.json"))).RootElement.GetProperty("functions").EnumerateArray())
{
    string excel = f.GetProperty("excel").GetString()!;
    MethodInfo m = methods[excel];
    if (f.TryGetProperty("constructions", out JsonElement cons))
    {
        foreach (JsonElement c in cons.EnumerateArray())
        {
            targets[c.GetProperty("c").GetString()!] = (m, excel, c.GetProperty("args").GetString()!.Split('/'));
        }
    }
    else
    {
        targets[f.GetProperty("c").GetString()!] = (m, excel, []);
    }
}

Console.WriteLine($"Excel build {Path.GetFullPath(build)}: {methods.Count:N0} worksheet functions, {targets.Count:N0} C functions reached through them");
var replay = new TraceReplay(enumNames, enumValues, flags, host.Cell);
var facts = new List<Fact>();
var factsByName = new Dictionary<string, Fact>(StringComparer.Ordinal);
var stopsBy = new Dictionary<string, long>(StringComparer.Ordinal);
var missing = new Dictionary<string, long>(StringComparer.Ordinal);
Fact? current = null;
bool whole = false, stopped = false;
long calls = 0, replayed = 0, line = 0, failures = 0;
int showStops = int.Parse(Environment.GetEnvironmentVariable("NANOQL_REPLAY_STOPS") ?? "0", CultureInfo.InvariantCulture);
var sw = Stopwatch.StartNew();
foreach (string text in File.ReadLines(trace))
{
    line++;
    if (text.StartsWith("{\"fact\"", StringComparison.Ordinal))
    {
        if (current != null && whole)
        {
            current.Whole++;
        }

        string name = JsonDocument.Parse(text).RootElement.GetProperty("fact").GetString()!;
        if (!factsByName.TryGetValue(name, out current))
        {
            current = new Fact(name);
            factsByName[name] = current;
            facts.Add(current);
        }

        current.Runs++;
        whole = true;
        stopped = false;
        continue;
    }

    calls++;
    if (current != null)
    {
        current.Calls++;
    }

    if (stopped)
    {
        continue;
    }

    using JsonDocument doc = JsonDocument.Parse(text);
    JsonElement r = doc.RootElement;
    string cname = r.GetProperty("f").GetString()!;
    string? why = null;
    if (!targets.TryGetValue(cname, out var target))
    {
        why = "a function the Excel build doesn't have";
        missing[cname] = missing.GetValueOrDefault(cname) + 1;
    }
    else
    {
        try
        {
            replay.Line = line;
            replay.FactName = current?.Name ?? string.Empty;
            replay.Fn = target.Excel + " (" + cname + ")";
            replay.Call(fns[cname], target.Method, target.TypeArgs, r);
        }
        catch (StopException e)
        {
            why = e.Message;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // The replayer's own failure on a call: reported, its fact stopped, never a silent pass.
            why = "the replayer failed: " + e.GetType().Name;
            if (failures++ < 10)
            {
                Console.WriteLine($"REPLAYER line {line} {cname}: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    if (why != null)
    {
        stopsBy[why] = stopsBy.GetValueOrDefault(why) + 1;
        if (showStops > 0 && whole)
        {
            showStops--;
            Console.WriteLine($"STOP line {line} {current?.Name}: {cname}: {why}");
        }

        stopped = true;
        whole = false;
        continue;
    }

    replayed++;
    if (current != null)
    {
        current.Replayed++;
    }
}

if (current != null && whole)
{
    current.Whole++;
}

if (report != null)
{
    using var w = new StreamWriter(report, false, new UTF8Encoding(false)) { NewLine = "\n" };
    w.WriteLine("fact\truns\twhole\tcalls\treplayed");
    foreach (Fact x in facts)
    {
        w.WriteLine($"{x.Name}\t{x.Runs}\t{x.Whole}\t{x.Calls}\t{x.Replayed}");
    }
}

long wholeFacts = facts.Count(x => x.Whole == x.Runs);
Console.WriteLine($"Excel replay: facts replayed whole {wholeFacts:N0} of {facts.Count:N0}; calls replayed {replayed:N0} of {calls:N0}; refusals matched {replay.Refusals:N0}; "
    + $"mismatches {replay.Mismatches:N0}; results through Excel's own forms {replay.FormMatches:N0} ({string.Join(", ", replay.Forms.Select(kv => $"{kv.Key} {kv.Value:N0}"))}); {sw.Elapsed.TotalSeconds:F0} s");
foreach (var kv in replay.MismatchBy.OrderByDescending(k => k.Value))
{
    Console.WriteLine($"  mismatches: {kv.Value:N0} x {kv.Key}");
}

foreach (var kv in stopsBy.OrderByDescending(k => k.Value))
{
    Console.WriteLine($"  facts stopped at {kv.Key}: {kv.Value:N0}");
}

foreach (var kv in missing.OrderByDescending(k => k.Value).Take(10))
{
    Console.WriteLine($"  not in the build: {kv.Key} (stopped {kv.Value:N0} fact runs)");
}

Console.WriteLine($"replayer failures {failures}; peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20):N0} MB");
bool below = false;
if (floorPath != null)
{
    long floor = JsonDocument.Parse(File.ReadAllText(floorPath)).RootElement.GetProperty("calls").GetInt64();
    below = replayed < floor || replay.Refusals == 0;
    Console.WriteLine($"floor: calls replayed {replayed:N0} (floor {floor:N0}); refusals matched {replay.Refusals:N0}; {(below ? "BELOW IT" : "at or above it")}");
}

return replay.Mismatches == 0 && failures == 0 && !below ? 0 : 1;

// The repository: the nearest folder above the current one holding bindings/roles.json.
static string Repository()
{
    for (string? d = Directory.GetCurrentDirectory(); d != null; d = Path.GetDirectoryName(d))
    {
        if (File.Exists(Path.Combine(d, "bindings", "roles.json")))
        {
            return d;
        }
    }

    throw new IOException("no bindings/roles.json in the current folder or above it: give --surface");
}
