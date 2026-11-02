// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDna.Integration;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The repro renderers (XLR14): every committed vector rendered as C# from the experimental tests' build's templates and
/// adapters, compiled against nanoql alone in a scratch project and run, each program's outcome compared with its cell's
/// as a grid, cell by cell (XLP66, XLP71); and the forms the renders take for a looped call, a name, a range where one
/// value goes and the template's mask (XLR15, XLR16, B7-1), in C# and in Python.
/// </summary>
[Collection(TestSettings.Name)]
public sealed partial class RenderTests
{
    // The current convention set, which a name where a day counter or a calendar goes reads (XLR16).
    private const string Set = "(global::NanoQL.ConventionProvider.Current as global::NanoQL.Extensions.Conventions.ConventionSet ?? global::NanoQL.Extensions.Conventions.ConventionSet.QuantLib)";

    private static readonly string Gen = typeof(RenderTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x => x.Key == "NanoQLExcelGen").Value!;

    /// <summary>Gets the experimental tests' build's repro templates.</summary>
    internal static string Templates { get; } = File.ReadAllText(Path.Combine(Gen, "experimental", "xl-templates.json"));

    /// <summary>Gets the experimental tests' build's engine adapters' source.</summary>
    internal static string Adapters { get; } = File.ReadAllText(Path.Combine(Gen, "experimental", "gen", "XlAdapters.g.cs"));

    /// <summary>Every committed vector's C# compiles against nanoql alone and runs to its cell's outcome.</summary>
    [Fact]
    public void EveryCommittedVectorRunsAsCSharpToItsCellsOutcome()
    {
        var vectors = Vectors.All();
        Assert.Equal(55, vectors.Count);
        var failed = RunAll(vectors, "committed");
        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }

    /// <summary>
    /// A value the add-in's conversions refuse renders as the cell's refusal, alone (the C# and the Python call fail as the
    /// cell's did) and as a looped element (that element fails in its place): a number where text goes, text where a
    /// boolean goes, a fraction where a whole number goes (32 and 64 bits, and a draws' count), a time of day where a date
    /// goes, an empty cell inside a vector's range, text in a matrix, complex text that doesn't parse.
    /// </summary>
    [Fact]
    public void AValueTheAddInRefusesFailsInTheRepro()
    {
        var cases = new List<(string Name, string Text)>
        {
            Refused("number-as-text", "nqlASXIsASXcode", 3.0, true),
            Refused("text-as-boolean", "nqlASXIsASXcode", "H5", "TRUE"),
            Refused("fraction-as-int32", "nqlSobolRsg", 2.5, 42.0),
            Refused("fraction-as-int64", "nqlMersenneTwisterUniformRng", 42.5),
            Refused("time-as-date", "nqlDayCounterYearFraction", "Actual360", 46328.5, 46509.0),
            Refused("inner-empty-in-vector", "nqlCholeskyDecompositionFunctionsCholeskySolveFor", Square(2, 0, 1, 1), Xl.Col(1.0, ExcelEmpty.Value, 3.0)),
            Refused("text-in-matrix", "nqlCholeskyDecompositionFunctionsCholeskyDecomposition", Square(4, "x", 1, 3), false),
            Refused("fraction-as-count", () => Xl.Call("nqlMersenneTwisterUniformRngDraws", Made("nqlMersenneTwisterUniformRng", 42.0), 2.5)),
            Refused("text-as-complex", "nqlExpm1FunctionsExpm1", "0.5+i0.25"),
            Refused("looped", "nqlASXIsASXcode", Xl.Col("H5", 3.0), true),
            Refused("looped-text-in-matrix", "nqlQRDecompositionFunctionsQRDecomposition", Square(4, "x", 1, 3), Xl.Col(true, false)),
            Refused("looped-fraction-as-count", () => Xl.Call("nqlMersenneTwisterUniformRngDraws", Made("nqlMersenneTwisterUniformRng", 42.0), Xl.Col(2.0, 2.5))),
        };
        Assert.Equal(12, cases.Count);
        foreach ((string name, string text) in cases)
        {
            bool looped = name.StartsWith("looped", StringComparison.Ordinal);
            Assert.Contains(looped ? "{\"error\":15}" : "\"expected\": { \"throws\": true }", text, StringComparison.Ordinal);
            string cs = string.Join("\n", XlCSharp.Render(text, Templates, Adapters));
            string py = string.Join("\n", XlPython.Render(text, Templates));
            Assert.Contains(looped ? "Elem(() => throw new global::System.ArgumentException(" : "= Refused(", cs, StringComparison.Ordinal);
            Assert.Contains(looped ? "_elem(lambda: _raise(" : "= _raise(", py, StringComparison.Ordinal);
        }

        var failed = RunAll(cases, "refused");
        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }

    /// <summary>
    /// A looped call is its loop, each element guarded as Excel shows a failed element (XLR15): a date's loop, and an
    /// element no date holds failing in its place; in C# and in Python.
    /// </summary>
    [Fact]
    public void ALoopedCallIsItsLoopEachElementGuarded()
    {
        string cs = CSharp("r7-loop-values");
        Assert.Contains("var v3 = new object[10, 1];", cs, StringComparison.Ordinal);
        Assert.Contains("v3[9, 0] = Elem(() => (object)(((global::NanoQL.TermStructures.YieldTermStructure)v2).Discount(new global::NanoQL.Time.Date(46628), false)));", cs, StringComparison.Ordinal);
        Assert.Contains("[_elem(lambda: v2.discount(_d(46628)))]", Python("r7-loop-values"), StringComparison.Ordinal);
        Assert.Contains("v3[1, 0] = Elem(() => throw new global::System.ArgumentException(\"date: expected a date (a whole-number serial), got text \\\"x\\\"\"));", CSharp("r7-loop-element"), StringComparison.Ordinal);
        Assert.Contains("_elem(lambda: _raise('date: expected a date (a whole-number serial), got text \"x\"'))", Python("r7-loop-element"), StringComparison.Ordinal);
    }

    /// <summary>A name where a day counter or a calendar goes is the convention set's lookup (XLR16), alone or in an array of calendars (B7-1).</summary>
    [Fact]
    public void ANameIsTheConventionSetsLookup()
    {
        Assert.Contains($"new global::NanoQL.TermStructures.FlatForward(new global::NanoQL.Time.Date(46328), 0.03, {Set}.DayCounter(\"Actual365Fixed\")", CSharp("r7-names-curve"), StringComparison.Ordinal);
        Assert.Contains("nanoql.FlatForward(_d(46328), 0.03, nanoql.ConventionSet.day_counter(None, 'Actual365Fixed'))", Python("r7-names-curve"), StringComparison.Ordinal);
        Assert.Contains($"new global::NanoQL.Time.Calendar[] {{ {Set}.Calendar(\"TARGET\"), {Set}.Calendar(\"UnitedStates.Settlement\") }}", CSharp("r8-z-namesarray"), StringComparison.Ordinal);
        Assert.Contains("nanoql.JointCalendar([nanoql.ConventionSet.calendar(None, 'TARGET'), nanoql.ConventionSet.calendar(None, 'UnitedStates.Settlement')])", Python("r8-z-namesarray"), StringComparison.Ordinal);
    }

    /// <summary>A range where one value goes, which the template's mask doesn't loop, is the add-in's refusal, never its first cell (B7-1).</summary>
    [Fact]
    public void ARangeWhereOneValueGoesIsTheAddInsRefusal()
    {
        string cs = CSharp("r8-y-rangeret");
        Assert.Contains("var v2 = Refused(\"nqlCalendarBusinessDayList: from: a range where one value goes, which the add-in does not loop\");", cs, StringComparison.Ordinal);
        Assert.DoesNotContain("BusinessDayList(", cs, StringComparison.Ordinal);
        Assert.Contains("v2 = _raise('nqlCalendarBusinessDayList: from: a range where one value goes, which the add-in does not loop')", Python("r8-y-rangeret"), StringComparison.Ordinal);
    }

    /// <summary>The template's mask is followed (B7-1): a bind looped over booleans and draws looped over counts, each element its own.</summary>
    [Fact]
    public void TheTemplatesMaskIsFollowed()
    {
        string bind = CSharp("r8-w-bindbool");
        Assert.Contains("v3[0, 0] = Elem(() => (object)(new BoundEngine(\"NanoQL.PricingEngines.DiscountingSwapEngine#0\", null, new global::System.Collections.Generic.Dictionary<string, object> { [\"discountCurve\"] = v2, [\"includeSettlementDateFlows\"] = true })));", bind, StringComparison.Ordinal);
        Assert.Contains("[\"includeSettlementDateFlows\"] = false", bind, StringComparison.Ordinal);
        Assert.Contains("[_elem(lambda: nanoql.DiscountingSwapEngine.bind(discount_curve=v2, include_settlement_date_flows=False))]", Python("r8-w-bindbool"), StringComparison.Ordinal);
        string draws = CSharp("r8-x-drawsloop");
        Assert.Contains("v2[0, 0] = Elem(() => (object)(Draws.Of((global::NanoQL.Numerics.IRng)(object)v1, 5)));", draws, StringComparison.Ordinal);
        Assert.Contains("v2[1, 0] = Elem(() => (object)(Draws.Of((global::NanoQL.Numerics.IRng)(object)v1, 3)));", draws, StringComparison.Ordinal);
    }

    // A call made on the test host and its cell exported: a refused value's vector, its outcome the cell's.
    private static (string Name, string Text) Refused(string name, string fn, params object?[] args) => Refused(name, () =>
    {
        Xl.Host.Cell = TestHost.At(0, 0);
        return Xl.Call(fn, args);
    });

    // The calls made, a handle argument's in a cell of its own before the root's, and the root's cell exported.
    private static (string Name, string Text) Refused(string name, Func<object> calls)
    {
        Xl.Reset();
        Xl.Host.Cell = TestHost.At(9, 9);
        object r = calls();
        if (r is object[,] { Length: > 1 } m)
        {
            Xl.Host.Spill(0, 0, m);
        }
        else
        {
            Xl.Host.Show(0, 0, r is object[,] one ? one[0, 0] : r);
        }

        return (name, Vectors.Export(TestHost.At(0, 0)));
    }

    // A handle made in a cell of its own, which the root's call then takes.
    private static object Made(string fn, params object?[] args)
    {
        object h = Xl.Call(fn, args);
        Xl.Host.Cell = TestHost.At(0, 0);
        return h;
    }

    // A 2 × 2 range by rows.
    private static object[,] Square(object a, object b, object c, object d) => new object[,] { { Xl.Value(a), Xl.Value(b) }, { Xl.Value(c), Xl.Value(d) } };

    // The vectors' C# compiled together against nanoql alone in a scratch project and run; each outcome against its cell's.
    private static List<string> RunAll(List<(string Name, string Text)> vectors, string label)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"nanoql-excel-render-{label}-{Environment.ProcessId}");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }

        Directory.CreateDirectory(dir);
        var runner = new StringBuilder("public static class Runner\n{\n    public static void Main()\n    {\n");
        for (int i = 0; i < vectors.Count; i++)
        {
            File.WriteAllLines(Path.Combine(dir, $"C{i}.cs"), XlCSharp.Render(vectors[i].Text, Templates, Adapters, $"C{i}"));
            File.WriteAllLines(Path.Combine(dir, vectors[i].Name + ".py"), XlPython.Render(vectors[i].Text, Templates));
            runner.Append(CultureInfo.InvariantCulture, $"        Case(\"{vectors[i].Name}\", () => C{i}.Repro.Run());\n");
        }

        runner.Append("    }\n\n    static void Case(string name, System.Func<string> run)\n    {\n        string r;\n")
            .Append("        try { r = run(); } catch (System.Exception e) { r = \"{\\\"throws\\\": \\\"\" + e.GetType().Name + \"\\\"}\"; }\n")
            .Append("        System.Console.WriteLine(\"CASE \" + name + \" \" + r);\n    }\n}\n");
        File.WriteAllText(Path.Combine(dir, "Runner.cs"), runner.ToString());
        File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />\n");
        File.WriteAllText(Path.Combine(dir, "Directory.Build.targets"), "<Project />\n");
        File.WriteAllText(Path.Combine(dir, "repros.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <StartupObject>Runner</StartupObject>
                <Nullable>disable</Nullable>
                <ImplicitUsings>disable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="NanoQL"><HintPath>{Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")}</HintPath></Reference>
                <Reference Include="NanoQL.Extensions"><HintPath>{Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")}</HintPath></Reference>
              </ItemGroup>
            </Project>

            """);
        (int build, string log) = Dotnet(dir, "build", "repros.csproj", "-c", "Release", "-nologo", "-v:q", "-o", Path.Combine(dir, "out"));
        Assert.True(build == 0, "the rendered C# doesn't compile:\n" + log);
        (int run, string output) = Dotnet(dir, Path.Combine(dir, "out", "repros.dll"));
        Assert.True(run == 0, output);
        var got = output.Split('\n').Where(l => l.StartsWith("CASE ", StringComparison.Ordinal))
            .ToDictionary(l => l.Split(' ')[1], l => l[(l.IndexOf(' ', 5) + 1)..].Trim(), StringComparer.Ordinal);
        var failed = new List<string>();
        foreach ((string name, string text) in vectors)
        {
            using JsonDocument v = JsonDocument.Parse(text);
            JsonElement expected = v.RootElement.GetProperty("cases")[0].GetProperty("expected");
            if (!got.TryGetValue(name, out string? g) || !Outcome(expected, g))
            {
                failed.Add($"{name}: got {g ?? "nothing"}, expected {expected.GetRawText()}");
            }
        }

        return failed;
    }

    private static string CSharp(string name) => string.Join("\n", XlCSharp.Render(File.ReadAllText(Path.Combine(Vectors.Folder, name + ".json")), Templates, Adapters));

    private static string Python(string name) => string.Join("\n", XlPython.Render(File.ReadAllText(Path.Combine(Vectors.Folder, name + ".json")), Templates));

    // The C# repro's outcome against the cell's (the check C0 ran on the planner's renders): a grid of cells, each a
    // double's bits, a text, a boolean, an object's type or an error's code; a Complex by its parts against the cell's text.
    private static bool Outcome(JsonElement expected, string got)
    {
        using JsonDocument d = JsonDocument.Parse(got);
        JsonElement g = d.RootElement;
        if (expected.TryGetProperty("throws", out _))
        {
            return g.TryGetProperty("throws", out _);
        }

        if (!g.TryGetProperty("rows", out _))
        {
            return false;
        }

        var cells = g.GetProperty("cells").EnumerateArray().ToList();
        if (expected.TryGetProperty("handle", out JsonElement h))
        {
            return cells.Count == 1 && Cell(h, cells[0], handle: true);
        }

        JsonElement want = expected.GetProperty("value");
        if (want.ValueKind == JsonValueKind.Object && want.TryGetProperty("rows", out JsonElement rows))
        {
            var wantCells = want.GetProperty("cells").EnumerateArray().ToList();
            return g.GetProperty("rows").GetInt32() == rows.GetInt32() && g.GetProperty("cols").GetInt32() == want.GetProperty("cols").GetInt32()
                && wantCells.Count == cells.Count && wantCells.Zip(cells).All(p => Cell(p.First, p.Second, handle: false));
        }

        return cells.Count == 1 && Cell(want, cells[0], handle: false);
    }

    private static bool Cell(JsonElement e, JsonElement g, bool handle)
    {
        static string Bits(double x) => BitConverter.DoubleToInt64Bits(x).ToString("X16", CultureInfo.InvariantCulture);
        bool Has(string name, out JsonElement x) => g.TryGetProperty(name, out x);
        switch (e.ValueKind)
        {
            case JsonValueKind.Null:
                return Has("n", out _);
            case JsonValueKind.True or JsonValueKind.False:
                return Has("b", out JsonElement b) && b.GetBoolean() == e.GetBoolean();
            case JsonValueKind.Number:
                return Has("d", out JsonElement dd) && dd.GetString() == Bits(e.GetDouble());
            case JsonValueKind.String when handle:
                return Has("h", out JsonElement th) && Type(e.GetString()!, th.GetString()!);
            case JsonValueKind.String:
                if (Has("c", out JsonElement c))
                {
                    Match m = ComplexText().Match(e.GetString()!);
                    return m.Success && c[0].GetString() == Bits(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                        && c[1].GetString() == Bits(double.Parse(m.Groups[2].Value + m.Groups[3].Value, CultureInfo.InvariantCulture));
                }

                return Has("s", out JsonElement s) && s.GetString() == e.GetString();
            case JsonValueKind.Object when e.TryGetProperty("empty", out _):
                return Has("n", out _);
            case JsonValueKind.Object when e.TryGetProperty("handle", out JsonElement eh):
                return Has("h", out JsonElement gh) && Type(eh.GetString()!, gh.GetString()!);

            // A looped call's element Excel shows as an error: #N/A where it has no value, a throw where it failed.
            case JsonValueKind.Object when e.TryGetProperty("error", out JsonElement er):
                return (Has("e", out JsonElement ge) && ge.GetInt32() == er.GetInt32()) || (er.GetInt32() == 42 && Has("n", out _)) || (er.GetInt32() != 42 && Has("t", out _));
            default:
                return false;
        }
    }

    // A handle's type: the add-in's draws are the repro's DrawsResult.
    private static bool Type(string handle, string type)
    {
        string want = handle.Split('#')[0];
        return want == type || (want == "XlDraws" && type == "DrawsResult");
    }

    private static (int Code, string Output) Dotnet(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }

        // The test platform's MSBuild settings aren't the scratch build's.
        foreach (string key in psi.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || k.StartsWith("MSBUILD", StringComparison.Ordinal)).ToList())
        {
            psi.Environment.Remove(key);
        }

        using Process p = Process.Start(psi)!;
        Task<string> err = p.StandardError.ReadToEndAsync();
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output + err.Result);
    }

    [GeneratedRegex("^([+-]?[0-9.]+(?:[eE][+-]?[0-9]+)?)([+-])([0-9.]+(?:[eE][+-]?[0-9]+)?)i$")]
    private static partial Regex ComplexText();
}
