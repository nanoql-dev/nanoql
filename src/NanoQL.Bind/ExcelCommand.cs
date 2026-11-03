// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind;

/// <summary>
/// <c>nanoql-bind excel</c>: the Excel add-in's generator (docs/excel.md).
/// <list type="bullet">
/// <item><c>update</c>: write <c>bindings/excel/functions.json</c>, nanoql's experimental build's surface.</item>
/// <item><c>check</c>: regenerate it into memory and compare its bytes with the checked-in file, and check its
/// names.</item>
/// <item><c>generate --out &lt;folder&gt;</c>: write a build's generated C#, its surface and its templates alone (the
/// Excel tests compile them).</item>
/// <item><c>build --out &lt;folder&gt;</c>: generate a build into an Excel-DNA project, build it and pack its <c>.xll</c>.</item>
/// <item><c>package --out &lt;folder&gt;</c>: build nanoql's two add-ins and write the release's zip and its symbols
/// zip; it takes <c>--runtime-source</c> alone.</item>
/// </list>
/// The others take <c>--assembly &lt;prefix&gt;=&lt;path&gt;</c> (nanoql's two by default), <c>--experimental</c> (the
/// experimental API beside the stable), <c>--list &lt;prefix&gt;=&lt;file&gt;|none</c> (nanoql's prefix takes nanoql's
/// list unless told otherwise, any other none), <c>--test-exports</c> (the bindings' test types, prefix nqlt, and
/// <c>nqlxlStats</c>), <c>--roles</c> and <c>--shipped</c> as the tool's other commands do; <c>build</c> also takes
/// <c>--runtime-source &lt;feed&gt;</c>, <c>--no-pack</c> (compile, no <c>.xll</c>) and the test-only
/// <c>--xl-budget &lt;n&gt;</c>, which only lowers the 10,000 functions Excel-DNA's loader registers.
/// </summary>
internal static class ExcelCommand
{
    /// <summary>Excel-DNA's loader's function slots: a function past them is silently missing.</summary>
    public const int Budget = 10000;

    /// <summary>Runs an <c>excel</c> command.</summary>
    /// <param name="args">The arguments after <c>excel</c>.</param>
    /// <param name="repo">The repository.</param>
    /// <param name="testTypes">Builds the bindings' test types and gives their assembly's path.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string[] args, string repo, Func<string, string> testTypes)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("excel takes update | check | generate --out <folder> | build --out <folder> | package --out <folder>");
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
        if (command == "package")
        {
            if (assemblies.Count > 0 || roles.Count > 0 || lists.Count > 0 || shipped != null || ns != null || experimental || testExports || noPack || budget != null)
            {
                throw new ArgumentException("excel package takes --out <folder> and --runtime-source <feed> alone: it packs nanoql's release");
            }

            return Package(bindings, outDir ?? throw new ArgumentException("excel package takes --out <folder>"), runtimeSource);
        }

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

    // The release: nanoql's two add-ins built and packed; the release's zip (the two .xll, LICENSE, NOTICE, the notices
    // with Excel-DNA's and .NET's added, README.md) and its symbols zip (nanoql's PDBs, their sources embedded), each
    // in the C archive's form: one folder, its entries sorted and dated 2026-01-01.
    private static int Package(string bindings, string dir, string? runtimeSource)
    {
        var sw = Stopwatch.StartNew();
        var assemblies = new List<AssemblyInput>
        {
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")),
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")),
        };
        var content = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (bool experimental in new[] { false, true })
        {
            var options = new ExcelOptions
            {
                Experimental = experimental,
                Lists = Lists(new Dictionary<string, string>(StringComparer.Ordinal), assemblies),
                Namespace = "NanoQL.Excel.Functions",
                Shipped = Path.Combine(bindings, "excel", "functions.shipped.json"),
            };
            ExcelOutput x = Generation.Run(assemblies, [Path.Combine(bindings, "roles.json")], Path.Combine(bindings, "surface.shipped.json"), options).Excel!;
            string build = Path.Combine(dir, experimental ? "experimental" : "stable");
            int rc = Build(x, build, assemblies, experimental, runtimeSource, false, Budget, sw);
            if (rc != 0)
            {
                return rc;
            }

            string xll = ExcelProject.AddInName(assemblies, experimental) + "64.xll";
            content[xll] = File.ReadAllBytes(Path.Combine(build, xll));
        }

        content["LICENSE"] = ExcelProject.Notice("LICENSE");
        content["NOTICE"] = ExcelProject.Notice("NOTICE");

        // nanoql's notices, then Excel-DNA's, whose runtime each .xll packs, and .NET's, whose host it links.
        string notices = Path.Combine(bindings, "excel", "notices");
        content["THIRD_PARTY_NOTICES.md"] = [.. ExcelProject.Notice("THIRD_PARTY_NOTICES.md"),
            .. File.ReadAllBytes(Path.Combine(notices, "Excel-DNA.txt")), .. File.ReadAllBytes(Path.Combine(notices, "dotnet.txt"))];
        content["README.md"] = File.ReadAllBytes(Path.Combine(bindings, "excel", "README.md"));
        var symbols = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (AssemblyInput a in assemblies)
        {
            string pdb = Path.ChangeExtension(a.Path, ".pdb");
            symbols[Path.GetFileName(pdb)] = File.Exists(pdb) ? File.ReadAllBytes(pdb)
                : throw new BindException($"the symbols zip lacks {pdb}: the tool's Release build carries nanoql's PDBs");
        }

        using JsonDocument surface = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(bindings, "surface.json")));
        string folder = $"nanoql-excel-{surface.RootElement.GetProperty("version").GetString()}-win-x64";
        foreach ((string name, SortedDictionary<string, byte[]> files) in new[] { (folder, content), (folder + "-symbols", symbols) })
        {
            string zip = Path.Combine(dir, name + ".zip");
            using (var z = new ZipArchive(File.Create(zip), ZipArchiveMode.Create))
            {
                foreach ((string path, byte[] bytes) in files)
                {
                    ZipArchiveEntry e = z.CreateEntry($"{name}/{path}", CompressionLevel.SmallestSize);
                    e.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using Stream s = e.Open();
                    s.Write(bytes);
                }
            }

            Console.WriteLine($"excel package: {zip}, {new FileInfo(zip).Length:N0} bytes, {files.Count} files");
        }

        Console.WriteLine($"excel package: done ({sw.Elapsed.TotalSeconds:F0} s)");
        return 0;
    }

    // Each prefix's list: nanoql's takes nanoql's unless told otherwise; any other none unless given one.
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

        // Excel-DNA names its output after the project's .dna; the add-in takes its own name, which its settings file follows.
        string packed = Path.Combine(dir, "bin", "Release", "net10.0-windows", "publish", ExcelProject.Name + "-AddIn64-packed.xll");
        string xll = Path.Combine(dir, addIn + ".xll");
        File.Copy(packed, xll, true);
        Console.WriteLine($"excel build: {xll}, {new FileInfo(xll).Length:N0} bytes, {x.Registered:N0} functions of {Budget:N0} ({sw.Elapsed.TotalSeconds:F0} s)");
        return 0;
    }

    /// <summary>
    /// The budget, which applies to packing: a build past it would register silently missing functions, so it is
    /// refused, naming its count and its largest namespaces.
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

    // A user prefix's functions the check withholds, each with the types it lacks and the remedy.
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
