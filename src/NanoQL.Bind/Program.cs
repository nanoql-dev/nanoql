// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind;

// nanoql-bind: the bindings' generator and native build (docs/bindings.md).
//   update                    regenerate bindings/'s nanoql.h, surface.json and coverage.md
//   check                     regenerate them into a temporary folder and compare their bytes with bindings/'s
//   freeze                    write a release's shipped files, bindings/surface.shipped.json and
//                             bindings/excel/functions.shipped.json, once check and excel check are green
//                             (FreezeCommand)
//   build --rid <rid> --out <folder> [--test-exports]
//                             generate, publish the native host with Native AOT, and write the library and headers
//   shims --out <folder> [--test-exports]
//                             write the export shims the native host compiles (its build runs this)
//   headers --out <folder> [--test-exports]
//                             write the C headers alone: nanoql.h, and a user prefix's (nqlt.h for the test exports)
//   twins --out <folder>      write the recorder's twins and their map (tools/replay)
//   replay-c --out <folder>   write the C replayer's dispatch (bindings/c/replay)
//   python --out <folder>     write the Python package's generated modules (bindings/python)
//   cpp --out <folder>        write the C++ headers, nanoql.hpp and a user prefix's own (bindings/cpp)
//   replay-cpp --out <folder> write the C++ replayer's dispatch (bindings/cpp/replay)
//   java --out <folder> [--prefix <prefix>]...
//                             write the Java layer's sources over jextract's (bindings/java): every prefix's, or the
//                             named ones alone (nql for nanoql's jar, a user's for a package beside it)
//   replay-java --out <folder> write the Java replayer's dispatch (bindings/java/src/test)
//   report                    print the surface's counts
//   excel update | check | generate | build | package
//                             the Excel add-in's surface, functions and builds (ExcelCommand, docs/excel.md)
// Each takes --assembly <prefix>=<path>... (nanoql's two by default, from beside the tool), --roles <path>... (the
// repository's bindings/roles.json by default) and --shipped <path> (bindings/surface.shipped.json when it exists).
// Outside the repository (no bindings/roles.json in the current folder or above it) the tool reads the defaults it
// carries beside it, defaults/bindings/, laid out as the repository's; build writes the native host's project into its
// output and publishes it there against the nanoql assemblies the generation reads, the given ones or the tool's own
// (LibraryDir), with the C headers alone; and the commands that read or write the repository's own files, or write the
// C++ and Java layers, which the repository builds and tests but doesn't release, refuse.
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args, Directory.GetCurrentDirectory(), Exec);
        }
        catch (Exception e) when (e is BindException or ArgumentException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine("nanoql-bind: " + e.Message);
            return 2;
        }
    }

    /// <summary>Runs a command.</summary>
    /// <param name="args">The command and its arguments.</param>
    /// <param name="cwd">The folder it runs in, which finds the repository.</param>
    /// <param name="exec">Runs a process: the file and its arguments, returning its exit code.</param>
    /// <returns>The exit code.</returns>
    internal static int Run(string[] args, string cwd, Func<string, IReadOnlyList<string>, int> exec)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: nanoql-bind update | check | freeze | build --rid <rid> --out <folder> [--test-exports] | shims --out <folder> | python --out <folder> | report");
            return 2;
        }

        string command = args[0];
        string? repo = Repository(cwd);
        if (command == "excel")
        {
            return ExcelCommand.Run(args[1..], repo, Defaults(), TestTypes);
        }

        var assemblies = new List<AssemblyInput>();
        var roles = new List<string>();
        string? shipped = null, rid = null, outDir = null, host = null;
        bool testExports = false;
        var prefixes = new List<string>();
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
                case "--prefix": prefixes.Add(args[++i]); break;
                default: throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }

        if (repo == null)
        {
            RefuseOutside(command, testExports);
        }

        if (command == "freeze")
        {
            if (args.Length > 1)
            {
                throw new ArgumentException("freeze takes no argument: it records nanoql's release from the repository's files");
            }

            return FreezeCommand.Run(repo!);
        }

        string bindings = Path.Combine(repo ?? Defaults(), "bindings");
        if (assemblies.Count == 0)
        {
            assemblies.Add(new AssemblyInput("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")));
            assemblies.Add(new AssemblyInput("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")));
        }

        if (testExports)
        {
            assemblies.Add(new AssemblyInput("nqlt", TestTypes(repo!)));
        }

        // Outside the repository the host is compiled against the nanoql assemblies the generation reads.
        string? libraryDir = repo == null && command == "build" ? LibraryDir(assemblies) : null;
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
                int same = Check(g, bindings);
                Console.WriteLine($"check: {same}/{g.Files.Count} byte-identical ({sw.Elapsed.TotalSeconds:F1} s); 'nanoql-bind update' regenerates them");
                return same == g.Files.Count ? 0 : 1;
            }

            case "report":
                Console.Write(g.Report());
                return 0;
            case "headers":
                Directory.CreateDirectory(outDir ?? throw new ArgumentException("headers takes --out <folder>"));
                foreach ((string path, string text) in g.Files.Where(f => f.Key.StartsWith("c/include/", StringComparison.Ordinal)))
                {
                    File.WriteAllBytes(Path.Combine(outDir, Path.GetFileName(path)), System.Text.Encoding.UTF8.GetBytes(text));
                    Console.WriteLine($"nanoql-bind: {Path.GetFileName(path)} in {outDir}");
                }

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
            case "cpp":
            {
                Directory.CreateDirectory(outDir ?? throw new ArgumentException("cpp takes --out <folder>"));
                foreach ((string name, string text) in g.CppHeaders())
                {
                    File.WriteAllBytes(Path.Combine(outDir, name), System.Text.Encoding.UTF8.GetBytes(text));
                    Console.WriteLine($"nanoql-bind: {name} in {outDir} ({text.Count(c => c == '\n'):N0} lines)");
                }

                return 0;
            }

            case "java":
            {
                int files = g.WriteJava(outDir ?? throw new ArgumentException("java takes --out <folder>"), prefixes.Count > 0 ? prefixes : null);
                Console.WriteLine($"nanoql-bind: the Java layer in {outDir}: {files:N0} files ({sw.Elapsed.TotalSeconds:F1} s)");
                return 0;
            }

            case "replay-java":
                foreach ((string name, string text) in g.JavaDispatch())
                {
                    string path = Path.Combine(outDir ?? throw new ArgumentException("replay-java takes --out <folder>"), name);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(text));
                }

                Console.WriteLine($"nanoql-bind: the Java replayer's dispatch in {outDir} ({sw.Elapsed.TotalSeconds:F1} s)");
                return 0;
            case "replay-cpp":
                Directory.CreateDirectory(outDir ?? throw new ArgumentException("replay-cpp takes --out <folder>"));
                foreach ((string name, string text) in g.CppDispatch())
                {
                    File.WriteAllBytes(Path.Combine(outDir, name), System.Text.Encoding.UTF8.GetBytes(text));
                }

                Console.WriteLine($"nanoql-bind: the C++ replayer's dispatch in {outDir} ({sw.Elapsed.TotalSeconds:F1} s)");
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

                host ??= repo != null ? Path.Combine(repo, "src", "NanoQL.Native", "NanoQL.Native.csproj") : WriteHost(outDir);
                return Build(g, host, rid, outDir, testExports, assemblies, libraryDir, exec);
            default:
                throw new ArgumentException($"unknown command '{command}'");
        }
    }

    /// <summary>The checked-in files a generation reproduces byte for byte, each printed.</summary>
    /// <param name="g">The generation.</param>
    /// <param name="bindings">The repository's <c>bindings/</c> folder.</param>
    /// <returns>The number byte-identical, of <c>g.Files.Count</c>.</returns>
    internal static int Check(Generation g, string bindings)
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

        return same;
    }

    /// <summary>The defaults the tool carries beside it, laid out as the repository's: <c>defaults/bindings/</c>
    /// (the roles and the released names) and <c>defaults/host/</c> (the native host's project and sources).</summary>
    /// <returns>The folder.</returns>
    internal static string Defaults() => Path.Combine(AppContext.BaseDirectory, "defaults");

    /// <summary>
    /// Outside the repository, the folder of nanoql's two assemblies the native host is compiled against: those the
    /// generation reads, so the library's shims and its nanoql are one build's. Given with <c>--assembly nql=</c>, both
    /// from one folder (a build output holding them); with none given, the tool's own copies beside it.
    /// </summary>
    /// <param name="assemblies">The assemblies the generation reads.</param>
    /// <returns>The folder, with its trailing separator.</returns>
    internal static string LibraryDir(List<AssemblyInput> assemblies)
    {
        var given = assemblies.Where(a => Path.GetFileName(a.Path) is "NanoQL.dll" or "NanoQL.Extensions.dll").ToList();
        var names = given.Select(a => Path.GetFileName(a.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var folders = given.Select(a => Path.GetDirectoryName(Path.GetFullPath(a.Path))!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names != 2 || folders.Count != 1)
        {
            throw new ArgumentException("outside the repository, build compiles the library against the nanoql assemblies it reads: give NanoQL.dll and "
                + "NanoQL.Extensions.dll with --assembly nql=, both from one folder (your build's output, which holds them), or no --assembly at all, for nanoql's own library");
        }

        return Path.TrimEndingDirectorySeparator(folders[0]) + Path.DirectorySeparatorChar;
    }

    // Outside the repository: the commands on the repository's own files, and the layers it builds and tests without
    // releasing them, stay in it.
    private static void RefuseOutside(string command, bool testExports)
    {
        if (command is "update" or "check" or "freeze" || testExports)
        {
            throw new IOException($"'{command}{(testExports ? " --test-exports" : string.Empty)}' works on the repository's own files: run nanoql-bind in the repository");
        }

        if (command is "cpp" or "java" or "replay-cpp" or "replay-java")
        {
            throw new IOException($"'{command}' writes the {(command.EndsWith("cpp", StringComparison.Ordinal) ? "C++" : "Java")} layer, which the repository builds and tests but doesn't release: run nanoql-bind in the repository");
        }
    }

    // The repository: the nearest folder at or above the given one holding bindings/roles.json, or null outside one.
    private static string? Repository(string cwd)
    {
        for (string? d = cwd; d != null; d = Path.GetDirectoryName(d))
        {
            if (File.Exists(Path.Combine(d, "bindings", "roles.json")))
            {
                return d;
            }
        }

        return null;
    }

    // Outside the repository, the native host's project the tool carries, written into the build's output and published
    // there, standalone: no build settings of the folders above it reach it, as an Excel build's project.
    private static string WriteHost(string outDir)
    {
        string from = Path.Combine(Defaults(), "host"), to = Path.Combine(outDir, "host");
        Directory.CreateDirectory(to);
        foreach (string f in Directory.EnumerateFiles(from))
        {
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        }

        File.WriteAllText(Path.Combine(to, "Directory.Build.props"), "<Project />\n");
        File.WriteAllText(Path.Combine(to, "Directory.Build.targets"), "<Project />\n");
        return Path.Combine(to, "NanoQL.Native.csproj");
    }

    // The test build's own assembly: echo functions per kind, a deprecated member, and a user's generator.
    private static string TestTypes(string repo)
    {
        string project = Path.Combine(repo, "bindings", "NanoQL.Bind.TestTypes", "NanoQL.Bind.TestTypes.csproj");
        string dll = Path.Combine(repo, "bindings", "NanoQL.Bind.TestTypes", "bin", "Release", "net10.0", "NanoQL.Bind.TestTypes.dll");

        // Built every time, incrementally: a library built before a change to the test types would bind the old ones.
        Exec("dotnet", ["build", project, "-c", "Release", "-nologo", "-v:q"]);
        return dll;
    }

    // Outside the repository (a library folder given), the host references nanoql's two assemblies there, and include/
    // holds the C headers alone.
    private static int Build(Generation g, string host, string rid, string outDir, bool testExports, List<AssemblyInput> assemblies, string? libraryDir, Func<string, IReadOnlyList<string>, int> exec)
    {
        bool outside = libraryDir != null;
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

        // The C++ headers beside the C ones, in the repository.
        if (!outside)
        {
            foreach ((string name, string text) in g.CppHeaders())
            {
                File.WriteAllBytes(Path.Combine(outDir, "include", name), System.Text.Encoding.UTF8.GetBytes(text));
            }
        }

        // Assemblies beyond nanoql's two are referenced by path; nanoql's are the host's project references, or outside
        // the repository those in the library folder.
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

        if (outside)
        {
            argv.Add("-p:NanoQLLibraryDir=" + libraryDir);
        }

        int rc = exec("dotnet", argv);
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

    private static int Exec(string file, IReadOnlyList<string> argv)
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
