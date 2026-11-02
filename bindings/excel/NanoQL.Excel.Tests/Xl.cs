// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using ExcelDna.Integration;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The two tests' builds compiled into this assembly (XLD36): their worksheet functions by Excel name, or by C name, a
/// family's construction with its type arguments first; each call with Excel's values (null a missing argument, a whole
/// number a double), as a cell makes it. Every call runs on the runtime with <see cref="Host"/> standing in for Excel.
/// </summary>
internal static class Xl
{
    private static readonly Lazy<Build> ExperimentalBuild = new(() => Load("NanoQL.Excel.Generated.Experimental", "experimental"));
    private static readonly Lazy<Build> StableBuild = new(() => Load("NanoQL.Excel.Generated.Stable", "stable"));

    static Xl() => X.Host = Host;

    /// <summary>Gets the test host every call reads.</summary>
    public static TestHost Host { get; } = new();

    /// <summary>Gets the experimental tests' build, with S-bind's test types (prefix nqlt).</summary>
    public static Build Experimental => ExperimentalBuild.Value;

    /// <summary>Gets the stable tests' build.</summary>
    public static Build Stable => StableBuild.Value;

    /// <summary>Starts a test on a clean runtime: an empty store, QuantLib's conventions, a 1900 workbook, cell A1.</summary>
    public static void Reset()
    {
        X.Host = Host;
        Host.Reset();
        X.Clear();
        XlSettings.Load(Path.Combine(Path.GetTempPath(), "nanoql-excel-tests-" + Environment.ProcessId, "nanoql64.xll"), null);
    }

    /// <summary>Calls a worksheet function of the experimental tests' build by its Excel name.</summary>
    /// <param name="name">The Excel name.</param>
    /// <param name="args">Its arguments.</param>
    /// <returns>The Excel result.</returns>
    public static object Call(string name, params object?[] args) => Experimental.Call(name, args);

    /// <summary>Calls a C function through its worksheet function in the experimental tests' build.</summary>
    /// <param name="cname">The C name.</param>
    /// <param name="args">Its arguments, after a family's type arguments.</param>
    /// <returns>The Excel result.</returns>
    public static object C(string cname, params object?[] args) => Experimental.C(cname, args);

    /// <summary>A column of Excel values.</summary>
    /// <param name="v">The values.</param>
    /// <returns>The range.</returns>
    public static object[,] Col(params object[] v)
    {
        var o = new object[v.Length, 1];
        for (int i = 0; i < v.Length; i++)
        {
            o[i, 0] = Value(v[i]);
        }

        return o;
    }

    /// <summary>A row of Excel values.</summary>
    /// <param name="v">The values.</param>
    /// <returns>The range.</returns>
    public static object[,] Row(params object[] v)
    {
        var o = new object[1, v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            o[0, i] = Value(v[i]);
        }

        return o;
    }

    /// <summary>A value as Excel passes it: null a missing argument, a whole number a double.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The Excel value.</returns>
    public static object Value(object? v) => v switch
    {
        null => ExcelMissing.Value,
        int n => (double)n,
        long n => (double)n,
        _ => v,
    };

    /// <summary>A handle's text, or a test failure naming what came back.</summary>
    /// <param name="r">The result.</param>
    /// <returns>The handle.</returns>
    public static string H(object r) => r is string s && s.Contains('#', StringComparison.Ordinal) ? s : throw new Xunit.Sdk.XunitException($"expected a handle, got {Show(r)}: {Message()}");

    /// <summary>A number, or a test failure naming what came back.</summary>
    /// <param name="r">The result.</param>
    /// <returns>The number.</returns>
    public static double D(object r) => r is double d ? d : throw new Xunit.Sdk.XunitException($"expected a number, got {Show(r)}: {Message()}");

    /// <summary>How a test shows a result.</summary>
    /// <param name="o">The result.</param>
    /// <returns>Its text.</returns>
    public static string Show(object? o) => o switch
    {
        null => "null",
        ExcelError e => e.ToString(),
        object[,] m => $"a {m.GetLength(0)} by {m.GetLength(1)} range [{string.Join(", ", m.Cast<object>().Take(8).Select(Show))}]",
        _ => o.ToString() ?? "null",
    };

    /// <summary>The message the runtime keeps for the calling cell.</summary>
    /// <returns>The message.</returns>
    public static string Message() => X.ErrorAt(Host.Cell!);

    private static Build Load(string ns, string resource)
    {
        var methods = typeof(Xl).Assembly.GetTypes().Where(t => t.Namespace == ns)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<ExcelFunctionAttribute>())).Where(x => x.Attribute != null)
            .ToDictionary(x => x.Attribute!.Name, x => x.Method, StringComparer.Ordinal);
        // Each build's functions.json, where the build wrote it (the project's NanoQLExcelGen).
        string gen = typeof(Xl).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().Single(x => x.Key == "NanoQLExcelGen").Value!;
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(gen, resource, "functions.json")));
        var byC = new Dictionary<string, (string Excel, string[] TypeArgs)>(StringComparer.Ordinal);
        foreach (JsonElement f in doc.RootElement.GetProperty("functions").EnumerateArray())
        {
            string excel = f.GetProperty("excel").GetString()!;
            if (f.TryGetProperty("constructions", out JsonElement cons))
            {
                foreach (JsonElement c in cons.EnumerateArray())
                {
                    byC[c.GetProperty("c").GetString()!] = (excel, c.GetProperty("args").GetString()!.Split('/'));
                }
            }
            else
            {
                byC[f.GetProperty("c").GetString()!] = (excel, []);
            }
        }

        return new Build(methods, byC, doc.RootElement.GetProperty("functions").EnumerateArray().Select(f => f.Clone()).ToList());
    }

    /// <summary>A tests' build: its functions by name and by C name, and its surface's entries.</summary>
    /// <param name="Methods">The worksheet functions by Excel name.</param>
    /// <param name="ByC">Each C function's worksheet function and a family's type arguments.</param>
    /// <param name="Functions">The build's functions.json entries.</param>
    internal sealed record Build(Dictionary<string, MethodInfo> Methods, Dictionary<string, (string Excel, string[] TypeArgs)> ByC, List<JsonElement> Functions)
    {
        /// <summary>Calls a worksheet function by its Excel name, the arguments not given missing.</summary>
        /// <param name="name">The Excel name.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The Excel result.</returns>
        public object Call(string name, params object?[] args)
        {
            MethodInfo m = Methods.TryGetValue(name, out MethodInfo? found) ? found : throw new Xunit.Sdk.XunitException($"the build has no {name}");
            ParameterInfo[] ps = m.GetParameters();
            if (args.Length > ps.Length)
            {
                throw new Xunit.Sdk.XunitException($"{name} takes {ps.Length} arguments, given {args.Length}");
            }

            object[] xs = [.. Enumerable.Range(0, ps.Length).Select(i => i < args.Length ? Value(args[i]) : ExcelMissing.Value)];
            return m.Invoke(null, xs)!;
        }

        /// <summary>Calls a C function through its worksheet function, a family's type arguments first.</summary>
        /// <param name="cname">The C name.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The Excel result.</returns>
        public object C(string cname, params object?[] args)
        {
            (string excel, string[] typeArgs) = ByC.TryGetValue(cname, out var t) ? t : throw new Xunit.Sdk.XunitException($"the build has no {cname}");
            return Call(excel, [.. typeArgs, .. args]);
        }

        /// <summary>The Excel name of a C function.</summary>
        /// <param name="cname">The C name.</param>
        /// <returns>The Excel name.</returns>
        public string Name(string cname) => ByC[cname].Excel;
    }
}
