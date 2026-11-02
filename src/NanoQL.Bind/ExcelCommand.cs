// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind;

/// <summary>
/// <c>nanoql-bind excel</c>: the Excel add-in's generator (docs/excel.md).
/// <list type="bullet">
/// <item><c>update</c>: write <c>bindings/excel/functions.json</c>, nanoql's experimental build's surface (XLD22).</item>
/// <item><c>check</c>: regenerate it into memory and compare its bytes with the checked-in file (X1), and check its names.</item>
/// <item><c>generate --out &lt;folder&gt;</c>: write a build's generated C#, its surface and its templates alone (the
/// Excel tests compile them).</item>
/// <item><c>build --out &lt;folder&gt;</c>: generate a build into an Excel-DNA project, build it and pack its <c>.xll</c>.</item>
/// </list>
/// Each takes <c>--assembly &lt;prefix&gt;=&lt;path&gt;</c> (nanoql's two by default), <c>--experimental</c> (the
/// experimental API beside the stable), <c>--list &lt;prefix&gt;=&lt;file&gt;|none</c> (nanoql's prefix takes nanoql's list
/// unless told otherwise, any other none, XLD35), <c>--test-exports</c> (S-bind's test types, prefix nqlt, and
/// <c>nqlxlStats</c>), <c>--roles</c> and <c>--shipped</c> as the tool's other commands do; <c>build</c> also takes
/// <c>--runtime-source &lt;feed&gt;</c>, <c>--no-pack</c> (compile, no <c>.xll</c>) and the test-only
/// <c>--xl-budget &lt;n&gt;</c>, which only lowers the 10,000 functions Excel-DNA's loader registers (XLR5).
/// </summary>
internal static class ExcelCommand
{
    /// <summary>Excel-DNA's loader's function slots: a function past them is silently missing (XLP2).</summary>
    public const int Budget = 10000;

    /// <summary>Runs an <c>excel</c> command.</summary>
    /// <param name="args">The arguments after <c>excel</c>.</param>
    /// <param name="repo">The repository.</param>
    /// <param name="testTypes">Builds S-bind's test types and gives their assembly's path.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string[] args, string repo, Func<string, string> testTypes)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("excel takes update | check | generate --out <folder> | build --out <folder>");
        }

        string command = args[0];
        var assemblies = new List<AssemblyInput>();
        var roles = new List<string>();
        var lists = new Dictionary<string, string>(StringComparer.Ordinal);
        string? shipped = null, outDir = null, runtimeSource = null, ns = null;
        bool experimental = false, testExports = false, noPack = false;
        int? budget = null;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--assembly": assemblies.Add(AssemblyInput.Parse(args[++i])); break;
                case "--roles": roles.Add(Path.GetFullPath(args[++i])); break;
                case "--shipped": shipped = Path.GetFullPath(args[++i]); break;
                case "--out": outDir = Path.GetFullPath(args[++i]); break;
                case "--experimental": experimental = true; break;
                case "--test-exports": testExports = true; break;
                case "--no-pack": noPack = true; break;
                case "--runtime-source": runtimeSource = Path.GetFullPath(args[++i]); break;
                case "--namespace": ns = args[++i]; break;
                case "--xl-budget": budget = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--list":
                {
                    string text = args[++i];
                    int eq = text.IndexOf('=', StringComparison.Ordinal);
                    if (eq <= 0)
                    {
                        throw new ArgumentException($"a list is given as <prefix>=<file> or <prefix>=none, not '{text}'");
                    }

                    lists[text[..eq]] = text[(eq + 1)..];
                    break;
                }

                default: throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }

        if (budget > Budget)
        {
            throw new ArgumentException($"--xl-budget {budget} would raise the ceiling: Excel-DNA's loader registers {Budget:N0} functions, and --xl-budget only lowers it");
        }

        string bindings = Path.Combine(repo, "bindings");
        if (assemblies.Count == 0)
        {
            assemblies.Add(new AssemblyInput("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")));
            assemblies.Add(new AssemblyInput("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")));
        }

        if (testExports)
        {
            assemblies.Add(new AssemblyInput("nqlt", testTypes(repo)));
        }

        if (roles.Count == 0)
        {
            roles.Add(Path.Combine(bindings, "roles.json"));
        }

        if (command is "update" or "check")
        {
            // nanoql's experimental build, nanoql's list: the surface functions.json holds.
            experimental = true;
            lists.Clear();
        }

        var options = new ExcelOptions
        {
            Experimental = experimental,
            Lists = Lists(lists, assemblies),
            TestBuild = testExports,
            Namespace = ns ?? "NanoQL.Excel.Functions",
            Shipped = Path.Combine(bindings, "excel", "functions.shipped.json"),
        };
        var sw = Stopwatch.StartNew();
        Generation g = Generation.Run(assemblies, roles, shipped ?? Path.Combine(bindings, "surface.shipped.json"), options);
        ExcelOutput x = g.Excel!;
        string checkedIn = Path.Combine(bindings, "excel", "functions.json");
        switch (command)
        {
            case "update":
                File.WriteAllBytes(checkedIn, Encoding.UTF8.GetBytes(x.Manifest));
                Console.WriteLine($"wrote bindings/excel/functions.json: {x.Summary}");
                return 0;
            case "check":
                return Check(x, checkedIn, sw);
            case "generate":
                Write(outDir ?? throw new ArgumentException("excel generate takes --out <folder>"), x);
                Console.WriteLine($"excel generate: {x.Summary} ({sw.Elapsed.TotalSeconds:F1} s)");
                PrintUserWithheld(x);
                return 0;
            case "build":
                return Build(x, outDir ?? throw new ArgumentException("excel build takes --out <folder>"), assemblies, experimental, runtimeSource, noPack, budget ?? Budget, sw);
            default:
                throw new ArgumentException($"unknown excel command '{command}'");
        }
    }

    // Each prefix's list: nanoql's takes nanoql's unless told otherwise; any other none unless given one (XLD35).
    private static Dictionary<string, ExcelList?> Lists(Dictionary<string, string> given, List<AssemblyInput> assemblies)
    {
        var lists = new Dictionary<string, ExcelList?>(StringComparer.Ordinal);
        foreach (string prefix in assemblies.Select(a => a.Prefix).Distinct())
        {
            lists[prefix] = given.TryGetValue(prefix, out string? file)
                ? file == "none" ? null : ExcelList.Read(Path.GetFullPath(file))
                : prefix == "nql" ? ExcelList.Nanoql() : null;
        }

        foreach (string prefix in given.Keys.Where(p => !lists.ContainsKey(p)))
        {
            throw new ArgumentException($"--list {prefix}: no assembly is bound under the prefix {prefix}");
        }

        return lists;
    }

    // X1: the checked-in surface byte for byte, and its names distinct regardless of case, none a cell reference, none
    // the add-in's own, none over 255 characters.
    private static int Check(ExcelOutput x, string checkedIn, Stopwatch sw)
    {
        byte[] want = Encoding.UTF8.GetBytes(x.Manifest);
        bool same = File.Exists(checkedIn) && File.ReadAllBytes(checkedIn).AsSpan().SequenceEqual(want);
        var names = x.Selection.Units.Select(u => u.Name).ToList();
        int distinct = names.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int cells = names.Count(ExcelNames.IsCellReference);
        int own = names.Count(n => ExcelNames.Own.Contains(n, StringComparer.OrdinalIgnoreCase));
        int longer = names.Count(n => n.Length > 255);
        Console.WriteLine($"{(same ? "same" : "DIFFERS")}: bindings/excel/functions.json");
        Console.WriteLine($"names: distinct regardless of case {distinct}/{names.Count}; cell references {cells}; the add-in's own {own}; over 255 characters {longer}");
        bool ok = same && distinct == names.Count && cells == 0 && own == 0 && longer == 0;
        Console.WriteLine($"excel check: {(ok ? 1 : 0)}/1 ({sw.Elapsed.TotalSeconds:F1} s); 'nanoql-bind excel update' regenerates it");
        return ok ? 0 : 1;
    }

    private static void Write(string dir, ExcelOutput x)
    {
        string gen = Path.Combine(dir, "gen");
        Directory.CreateDirectory(gen);
        foreach (string old in Directory.EnumerateFiles(gen, "*.g.cs"))
        {
            File.Delete(old);
        }

        foreach ((string name, string text) in x.Files)
        {
            File.WriteAllText(Path.Combine(gen, name), text);
        }

        File.WriteAllBytes(Path.Combine(dir, "functions.json"), Encoding.UTF8.GetBytes(x.Manifest));
        File.WriteAllBytes(Path.Combine(dir, "xl-templates.json"), Encoding.UTF8.GetBytes(x.Templates));
    }

    private static int Build(ExcelOutput x, string dir, List<AssemblyInput> assemblies, bool experimental, string? runtimeSource, bool noPack, int budget, Stopwatch sw)
    {
        if (!noPack)
        {
            CheckBudget(x, budget);
        }

        Directory.CreateDirectory(dir);
        Write(dir, x);
        ExcelProject.Write(dir, x.Files, assemblies, experimental, runtimeSource);
        ExcelProject.WriteNotices(dir);
        Console.WriteLine($"excel build: {x.Summary}");
        PrintUserWithheld(x);
        var argv = new List<string> { "build", Path.Combine(dir, ExcelProject.Name + ".csproj"), "-c", "Release", "-nologo", "-v:q" };
        if (noPack)
        {
            argv.Add("-p:RunExcelDnaBuild=false");
            argv.Add("-p:RunExcelDnaPack=false");
        }

        int rc = Exec("dotnet", argv);
        if (rc != 0)
        {
            return rc;
        }

        string addIn = ExcelProject.AddInName(assemblies, experimental) + "64";
        if (noPack)
        {
            Console.WriteLine($"excel build: compiled into {dir}, no .xll (--no-pack), {x.Registered:N0} functions registered were it packed ({sw.Elapsed.TotalSeconds:F0} s)");
            return 0;
        }

        // Excel-DNA names its output after the project's .dna; the add-in takes its own name (XLD1), which its settings file follows.
        string packed = Path.Combine(dir, "bin", "Release", "net10.0-windows", "publish", ExcelProject.Name + "-AddIn64-packed.xll");
        string xll = Path.Combine(dir, addIn + ".xll");
        File.Copy(packed, xll, true);
        Console.WriteLine($"excel build: {xll}, {new FileInfo(xll).Length:N0} bytes, {x.Registered:N0} functions of {Budget:N0} ({sw.Elapsed.TotalSeconds:F0} s)");
        return 0;
    }

    /// <summary>
    /// The budget, which applies to packing: a build past it would register silently missing functions (XLR5, XLD16),
    /// so it is refused, naming its count and its largest namespaces.
    /// </summary>
    /// <param name="x">The build.</param>
    /// <param name="budget">The ceiling: Excel-DNA's 10,000, or the test-only --xl-budget's lower one.</param>
    internal static void CheckBudget(ExcelOutput x, int budget)
    {
        if (x.Registered > budget)
        {
            string largest = string.Join(", ", x.Selection.Units.GroupBy(u => u.Function.Type.Namespace ?? string.Empty)
                .OrderByDescending(g => g.Count()).Take(5).Select(g => $"{g.Key} {g.Count():N0}"));
            throw new BindException($"the build registers {x.Registered:N0} functions, the generated and the add-in's own, past the {budget:N0} Excel-DNA's loader takes; the largest namespaces: {largest}. Narrow the list (--list), or build the stable API alone");
        }
    }

    // A user prefix's functions the check withholds, each with the types it lacks and the remedy (XLD35, F11-3).
    private static void PrintUserWithheld(ExcelOutput x)
    {
        foreach (string line in UserWithheld(x))
        {
            Console.WriteLine(line);
        }
    }

    /// <summary>The lines the tool prints for a user prefix's withheld functions: each with the types it lacks and the remedy.</summary>
    /// <param name="x">The build.</param>
    /// <returns>The lines.</returns>
    internal static IEnumerable<string> UserWithheld(ExcelOutput x)
        => x.Selection.Withheld.Where(w => w.Function.Prefix != "nql").Select(w => $"excel: {w.Name} ({w.Function.CName}) withheld: no kept function makes {string.Join(", ", w.Lacks)}; "
            + "where nanoql's list leaves the type out, widen it: --list nql=<file> with the type added (docs/excel.md)");

    private static int Exec(string file, IEnumerable<string> argv)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false };
        foreach (string a in argv)
        {
            psi.ArgumentList.Add(a);
        }

        using Process p = Process.Start(psi)!;
        p.WaitForExit();
        return p.ExitCode;
    }
}
