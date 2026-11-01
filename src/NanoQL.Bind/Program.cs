// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NanoQL.Bind;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

// nanoql-bind: the bindings' generator and native build (docs/bindings.md).
//   update                    regenerate bindings/'s nanoql.h, surface.json and coverage.md
//   check                     regenerate them into a temporary folder and compare their bytes with bindings/'s
//   build --rid <rid> --out <folder> [--test-exports]
//                             generate, publish the native host with Native AOT, and write the library and headers
//   shims --out <folder> [--test-exports]
//                             write the export shims the native host compiles (its build runs this)
//   twins --out <folder>      write the recorder's twins and their map (tools/replay)
//   replay-c --out <folder>   write the C replayer's dispatch (bindings/c/replay)
//   python --out <folder>     write the Python package's generated modules (bindings/python)
//   report                    print the surface's counts
// Each takes --assembly <prefix>=<path>... (nanoql's two by default, from beside the tool), --roles <path>... (the
// repository's bindings/roles.json by default) and --shipped <path> (bindings/surface.shipped.json when it exists).
try
{
    return Run(args);
}
catch (Exception e) when (e is BindException or ArgumentException or InvalidDataException or IOException)
{
    Console.Error.WriteLine("nanoql-bind: " + e.Message);
    return 2;
}

static int Run(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("usage: nanoql-bind update | check | build --rid <rid> --out <folder> [--test-exports] | shims --out <folder> | python --out <folder> | report");
        return 2;
    }

    string command = args[0];
    var assemblies = new List<AssemblyInput>();
    var roles = new List<string>();
    string? shipped = null, rid = null, outDir = null, host = null;
    bool testExports = false;
    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--assembly": assemblies.Add(AssemblyInput.Parse(args[++i])); break;
            case "--roles": roles.Add(Path.GetFullPath(args[++i])); break;
            case "--shipped": shipped = Path.GetFullPath(args[++i]); break;
            case "--rid": rid = args[++i]; break;
            case "--out": outDir = Path.GetFullPath(args[++i]); break;
            case "--host": host = Path.GetFullPath(args[++i]); break;
            case "--test-exports": testExports = true; break;
            default: throw new ArgumentException($"unknown argument '{args[i]}'");
        }
    }

    string repo = Repository();
    string bindings = Path.Combine(repo, "bindings");
    if (assemblies.Count == 0)
    {
        assemblies.Add(new AssemblyInput("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")));
        assemblies.Add(new AssemblyInput("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")));
    }

    if (testExports)
    {
        assemblies.Add(new AssemblyInput("nqlt", TestTypes(repo)));
    }

    if (roles.Count == 0)
    {
        roles.Add(Path.Combine(bindings, "roles.json"));
    }

    shipped ??= Path.Combine(bindings, "surface.shipped.json");
    var sw = Stopwatch.StartNew();
    Generation g = Generation.Run(assemblies, roles, shipped);
    switch (command)
    {
        case "update":
            foreach ((string path, string text) in g.Files.Where(f => !testExports || !f.Key.EndsWith("nqlt.h", StringComparison.Ordinal)))
            {
                string full = Path.Combine(bindings, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllBytes(full, System.Text.Encoding.UTF8.GetBytes(text));
                Console.WriteLine($"wrote bindings/{path} ({text.Length:N0} characters)");
            }

            return 0;
        case "check":
        {
            int same = 0;
            foreach ((string path, string text) in g.Files)
            {
                string full = Path.Combine(bindings, path);
                byte[] want = System.Text.Encoding.UTF8.GetBytes(text);
                bool ok = File.Exists(full) && File.ReadAllBytes(full).AsSpan().SequenceEqual(want);
                Console.WriteLine($"{(ok ? "same" : "DIFFERS")}: bindings/{path}");
                same += ok ? 1 : 0;
            }

            Console.WriteLine($"check: {same}/{g.Files.Count} byte-identical ({sw.Elapsed.TotalSeconds:F1} s); 'nanoql-bind update' regenerates them");
            return same == g.Files.Count ? 0 : 1;
        }

        case "report":
            Console.Write(g.Report());
            return 0;
        case "twins":
            g.WriteTwins(outDir ?? throw new ArgumentException("twins takes --out <folder>"));
            Console.WriteLine($"nanoql-bind: the twins in {outDir} ({sw.Elapsed.TotalSeconds:F1} s)");
            return 0;
        case "replay-c":
            Directory.CreateDirectory(outDir ?? throw new ArgumentException("replay-c takes --out <folder>"));
            File.WriteAllText(Path.Combine(outDir, "dispatch.c"), g.CDispatch);
            Console.WriteLine($"nanoql-bind: the C replayer's dispatch in {outDir} ({sw.Elapsed.TotalSeconds:F1} s)");
            return 0;
        case "python":
        {
            (int functions, int classes, int enums) = g.WritePython(outDir ?? throw new ArgumentException("python takes --out <package folder>"));
            Console.WriteLine($"nanoql-bind: the Python modules in {outDir}: {functions:N0} functions, {classes:N0} classes, {enums:N0} enums ({sw.Elapsed.TotalSeconds:F1} s)");
            return 0;
        }

        case "shims":
            g.WriteShims(outDir ?? throw new ArgumentException("shims takes --out <folder>"));
            Console.WriteLine($"nanoql-bind: {g.Surface.Functions.Count:N0} functions' shims in {outDir} ({sw.Elapsed.TotalSeconds:F1} s)");
            return 0;
        case "build":
            if (rid == null || outDir == null)
            {
                throw new ArgumentException("build takes --rid <rid> and --out <folder>");
            }

            return Build(g, host ?? Path.Combine(repo, "src", "NanoQL.Native", "NanoQL.Native.csproj"), rid, outDir, testExports, assemblies);
        default:
            throw new ArgumentException($"unknown command '{command}'");
    }
}

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

    throw new IOException("no bindings/roles.json in the current folder or above it: run nanoql-bind in the repository");
}

// The test build's own assembly: echo functions per kind, a deprecated member, and a user's generator.
static string TestTypes(string repo)
{
    string project = Path.Combine(repo, "bindings", "NanoQL.Bind.TestTypes", "NanoQL.Bind.TestTypes.csproj");
    string dll = Path.Combine(repo, "bindings", "NanoQL.Bind.TestTypes", "bin", "Release", "net10.0", "NanoQL.Bind.TestTypes.dll");

    // Built every time, incrementally: a library built before a change to the test types would bind the old ones.
    Exec("dotnet", ["build", project, "-c", "Release", "-nologo", "-v:q"]);
    return dll;
}

static int Build(Generation g, string host, string rid, string outDir, bool testExports, List<AssemblyInput> assemblies)
{
    var sw = Stopwatch.StartNew();
    string hostDir = Path.GetDirectoryName(host)!;
    string shims = Path.Combine(hostDir, "obj", "nanoql-bind", rid + (testExports ? "-test" : string.Empty));
    g.WriteShims(shims);
    foreach ((string path, string text) in g.Files)
    {
        string full = Path.Combine(outDir, path.StartsWith("c/include/", StringComparison.Ordinal) ? "include/" + Path.GetFileName(path) : path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, System.Text.Encoding.UTF8.GetBytes(text));
    }

    // Assemblies beyond nanoql's two are referenced by path; nanoql's are the host's project references.
    string extra = string.Join(";", assemblies.Where(a => Path.GetFileName(a.Path) is not ("NanoQL.dll" or "NanoQL.Extensions.dll")).Select(a => a.Path));
    string publish = Path.Combine(outDir, "publish");
    var argv = new List<string>
    {
        "publish", host, "-c", "Release", "-r", rid, "-o", publish, "-nologo", "-p:NanoQLShimsDir=" + shims,
        "-p:NanoQLTestExports=" + (testExports ? "true" : "false"), "-p:NanoQLGenerateShims=false",
    };
    if (extra.Length > 0)
    {
        argv.Add("-p:NanoQLAssemblies=" + extra.Replace(";", "%3B", StringComparison.Ordinal));
    }

    int rc = Exec("dotnet", argv);
    if (rc != 0)
    {
        return rc;
    }

    string lib = Path.Combine(outDir, "lib");
    Directory.CreateDirectory(lib);
    foreach (string f in Directory.EnumerateFiles(publish).Where(f => Path.GetFileName(f).StartsWith("nanoql.", StringComparison.Ordinal) || Path.GetFileName(f).StartsWith("libnanoql.", StringComparison.Ordinal)))
    {
        File.Copy(f, Path.Combine(lib, Path.GetFileName(f)), true);
    }

    Console.WriteLine($"built {rid}{(testExports ? " (test build)" : string.Empty)} into {outDir} in {sw.Elapsed.TotalSeconds:F0} s: {g.Surface.Functions.Count:N0} functions");
    return 0;
}

static int Exec(string file, IEnumerable<string> argv)
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
