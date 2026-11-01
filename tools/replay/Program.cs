// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

// The vector suite's recorder (docs/bindings.md, "The vector suite through each binding").
//   record --tests <NanoQL.Tests' build folder> --twins <nanoql-bind twins folder> --out <folder> [--class <name>...]
//          [--keep-reds]
// compiles the twins with runtime/, copies the test build into <out>/run with its assembly rewritten (each call site
// into nanoql that has a twin calls it; each vector fact starts a fact in the trace), runs the vector classes one at a
// time, and writes the trace to <out>/trace.jsonl with a summary. A vector red stops it, unless --keep-reds: off the
// reference platform a vector red is reported, and the trace, C#'s own results, is kept.
try
{
    return Run(args);
}
catch (Exception e) when (e is ArgumentException or IOException or InvalidOperationException)
{
    Console.Error.WriteLine("replay: " + e.Message);
    return 2;
}

static int Run(string[] args)
{
    string? tests = null, twins = null, outDir = null;
    bool keepReds = false;
    var classes = new List<string>();
    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--tests": tests = Path.GetFullPath(args[++i]); break;
            case "--twins": twins = Path.GetFullPath(args[++i]); break;
            case "--out": outDir = Path.GetFullPath(args[++i]); break;
            case "--class": classes.Add(args[++i]); break;
            case "--keep-reds": keepReds = true; break;
            default: throw new ArgumentException($"unknown argument '{args[i]}'");
        }
    }

    if (args.Length == 0 || args[0] != "record" || tests == null || twins == null || outDir == null)
    {
        Console.Error.WriteLine("usage: replay record --tests <folder> --twins <folder> --out <folder> [--class <name>...] [--keep-reds]");
        return 2;
    }

    var sw = Stopwatch.StartNew();
    Directory.CreateDirectory(outDir);
    string twinsDll = CompileTwins(tests, twins, Path.Combine(outDir, "twins"));
    Console.WriteLine($"twins compiled ({sw.Elapsed.TotalSeconds:F0} s)");

    string run = Path.Combine(outDir, "run");
    if (Directory.Exists(run))
    {
        Directory.Delete(run, true);
    }

    Copy(tests, run);
    File.Copy(twinsDll, Path.Combine(run, Path.GetFileName(twinsDll)), true);
    File.Delete(Path.Combine(run, "NanoQL.Tests.deps.json"));
    (int sites, int groups, int facts, List<string> vectorClasses) = Rewrite(Path.Combine(tests, "NanoQL.Tests.dll"), twinsDll, Path.Combine(twins, "twins.json"), Path.Combine(run, "NanoQL.Tests.dll"));
    Console.WriteLine($"rewritten: {sites:N0} call sites and {groups:N0} method groups into twins; {facts:N0} vector facts marked ({sw.Elapsed.TotalSeconds:F0} s)");

    string trace = Path.Combine(outDir, "trace.jsonl");
    var psi = new ProcessStartInfo(Path.Combine(run, OperatingSystem.IsWindows() ? "NanoQL.Tests.exe" : "NanoQL.Tests")) { UseShellExecute = false, WorkingDirectory = run };
    psi.Environment["NANOQL_TRACE"] = trace;
    psi.ArgumentList.Add("-parallelMode");
    psi.ArgumentList.Add("none");
    // The vector namespaces whole, by class wildcards, and a class at a time where a namespace holds other classes
    // too: a command line naming each of the suite's classes would pass Windows' limit.
    foreach ((string filter, string value) in classes.Count > 0 ? classes.Select(c => ("-class", c)) : Filters(vectorClasses))
    {
        psi.ArgumentList.Add(filter);
        psi.ArgumentList.Add(value);
    }

    using (Process p = Process.Start(psi)!)
    {
        p.WaitForExit();
        Console.WriteLine($"the vector classes ran, exit code {p.ExitCode} ({sw.Elapsed.TotalSeconds:F0} s)");
        // Exit code 1 is failed tests; any other, a crashed host or a bad command line, leaves no trace to keep.
        if (p.ExitCode != 0 && !(keepReds && p.ExitCode == 1))
        {
            return p.ExitCode;
        }
    }

    long calls = 0, factLines = 0;
    foreach (string line in File.ReadLines(trace))
    {
        if (line.StartsWith("{\"fact\"", StringComparison.Ordinal))
        {
            factLines++;
        }
        else
        {
            calls++;
        }
    }

    Console.WriteLine($"trace: {calls:N0} calls in {factLines:N0} fact runs, {new FileInfo(trace).Length:N0} bytes ({sw.Elapsed.TotalSeconds:F0} s)");
    return 0;
}

// The twins' assembly: nanoql-bind's Twins.g.cs and runtime/, against the test build's nanoql assemblies.
static string CompileTwins(string tests, string twins, string dir)
{
    Directory.CreateDirectory(dir);
    File.Copy(Path.Combine(twins, "Twins.g.cs"), Path.Combine(dir, "Twins.g.cs"), true);
    foreach (string f in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "runtime"), "*.cs"))
    {
        File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
    }

    File.WriteAllText(Path.Combine(dir, "NanoQL.Replay.Twins.csproj"), $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <NoWarn>$(NoWarn);NQL0001;CS0618;CS1591</NoWarn>
            <AssemblyName>NanoQL.Replay.Twins</AssemblyName>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="{Path.Combine(tests, "NanoQL.dll")}" />
            <Reference Include="{Path.Combine(tests, "NanoQL.Extensions.dll")}" />
          </ItemGroup>
        </Project>
        """);
    var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = dir };
    foreach (string a in new[] { "build", "-c", "Release", "-nologo", "-v:q" })
    {
        psi.ArgumentList.Add(a);
    }

    using Process p = Process.Start(psi)!;
    p.WaitForExit();
    if (p.ExitCode != 0)
    {
        throw new InvalidOperationException("the twins didn't compile");
    }

    return Path.Combine(dir, "bin", "Release", "net10.0", "NanoQL.Replay.Twins.dll");
}

static void Copy(string from, string to)
{
    foreach (string d in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
    {
        Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
    }

    Directory.CreateDirectory(to);
    foreach (string f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
    {
        File.Copy(f, Path.Combine(to, Path.GetRelativePath(from, f)), true);
    }
}

// Rewrites the test assembly: a call site into nanoql whose member has a twin calls the twin, a method group of one
// makes a delegate over the twin, and each vector fact's method starts with its fact's line.
static (int Sites, int Groups, int Facts, List<string> Classes) Rewrite(string input, string twinsDll, string mapFile, string output)
{
    var map = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(mapFile))!;
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.GetDirectoryName(input)!);
    resolver.AddSearchDirectory(Path.GetDirectoryName(twinsDll)!);
    using var module = ModuleDefinition.ReadModule(input, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
    using var twinModule = ModuleDefinition.ReadModule(twinsDll, new ReaderParameters { AssemblyResolver = resolver });
    TypeDefinition twins = twinModule.GetType("NanoQL.Replay.Twins");
    var twinMethods = twins.Methods.ToDictionary(m => m.Name);
    MethodReference fact = module.ImportReference(twinModule.GetType("NanoQL.Replay.Rec").Methods.Single(m => m.Name == "Fact"));
    int sites = 0, groups = 0, facts = 0;
    var classes = new List<string>();
    IEnumerable<TypeDefinition> All(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(All));
    foreach (TypeDefinition type in module.Types.SelectMany(All))
    {
        bool vector = IsVectorClass(type);
        bool marked = false;
        foreach (MethodDefinition method in type.Methods.Where(m => m.HasBody))
        {
            ILProcessor il = method.Body.GetILProcessor();
            foreach (Instruction ins in method.Body.Instructions.ToList())
            {
                // nanoql's members, and the reflection calls that may build a preset.
                if (ins.Operand is not MethodReference mr
                    || (mr.DeclaringType.Scope.Name is not ("NanoQL" or "NanoQL.Extensions") && mr.DeclaringType.FullName is not ("System.Activator" or "System.Reflection.ConstructorInfo")))
                {
                    continue;
                }

                if (!map.TryGetValue(Key(mr), out string[]? twin))
                {
                    continue;
                }

                string site = twin[1];
                bool rewrite = ins.OpCode.Code switch
                {
                    Code.Newobj => site == "newobj",
                    Code.Call => site == "static" || site == "byref" || (site == "instance" && !Resolve(mr)?.IsVirtual == true),
                    Code.Callvirt => site == "instance" && ins.Previous?.OpCode.Code != Code.Constrained,
                    Code.Ldftn => site == "static",
                    Code.Ldvirtftn => site == "instance",
                    _ => false,
                };
                if (!rewrite)
                {
                    continue;
                }

                MethodReference target = module.ImportReference(twinMethods[twin[0]]);
                if (ins.OpCode.Code == Code.Ldvirtftn)
                {
                    // dup; ldvirtftn M becomes dup; pop; ldftn twin: the delegate stays over the instance, now the
                    // static twin's first argument.
                    il.InsertBefore(ins, il.Create(OpCodes.Pop));
                    ins.OpCode = OpCodes.Ldftn;
                    groups++;
                }
                else if (ins.OpCode.Code == Code.Ldftn)
                {
                    groups++;
                }
                else
                {
                    ins.OpCode = OpCodes.Call;
                    sites++;
                }

                ins.Operand = target;
            }

            if (vector && method.CustomAttributes.Any(a => a.AttributeType.Name is "FactAttribute" or "TheoryAttribute"))
            {
                Instruction first = method.Body.Instructions[0];
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, type.FullName + "." + method.Name));
                il.InsertBefore(first, il.Create(OpCodes.Call, fact));
                facts++;
                marked = true;
            }
        }

        if (marked)
        {
            classes.Add(type.FullName);
        }
    }

    module.Write(output);
    return (sites, groups, facts, classes);
}

static IEnumerable<(string Filter, string Value)> Filters(List<string> vectorClasses)
{
    foreach (IGrouping<string, string> ns in vectorClasses.GroupBy(c => c[..c.LastIndexOf('.')]))
    {
        if (ns.Key.StartsWith("NanoQL.Tests.Vectors", StringComparison.Ordinal))
        {
            // The runner ANDs filters of different kinds, so the namespaces go as class wildcards, which it ORs.
            yield return ("-class", ns.Key + ".*");
        }
        else
        {
            foreach (string c in ns)
            {
                yield return ("-class", c);
            }
        }
    }
}

static bool IsVectorClass(TypeDefinition t) =>
    (t.Namespace ?? string.Empty).StartsWith("NanoQL.Tests.Vectors", StringComparison.Ordinal)
    || (t.Namespace == "NanoQL.Tests.QLNet" && t.Name.EndsWith("VectorTests", StringComparison.Ordinal));

static MethodDefinition? Resolve(MethodReference m)
{
    try
    {
        return m.Resolve();
    }
    catch (AssemblyResolutionException)
    {
        return null;
    }
}

// A member's key as nanoql-bind writes it: declaring type, name and parameter types, generic arguments expanded, a
// closed generic method's arguments after it.
static string Key(MethodReference m)
{
    MethodReference e = m is GenericInstanceMethod gm ? gm.ElementMethod : m;
    string k = Name(m.DeclaringType) + "::" + m.Name + "(" + string.Join(",", e.Parameters.Select(p => Name(Subst(p.ParameterType, m.DeclaringType)))) + ")";
    return m is GenericInstanceMethod g ? k + "<" + string.Join(",", g.GenericArguments.Select(Name)) + ">" : k;
}

static string Name(TypeReference t) => t switch
{
    GenericInstanceType g => g.ElementType.FullName.Replace('/', '+') + "<" + string.Join(",", g.GenericArguments.Select(Name)) + ">",
    ByReferenceType br => Name(br.ElementType) + "&",
    ArrayType at => Name(at.ElementType) + "[]",
    _ => t.FullName.Replace('/', '+'),
};

static TypeReference Subst(TypeReference t, TypeReference declaring)
{
    if (t is GenericParameter gp && gp.Type == GenericParameterType.Type && declaring is GenericInstanceType git && gp.Position < git.GenericArguments.Count)
    {
        return git.GenericArguments[gp.Position];
    }

    if (t is GenericInstanceType g)
    {
        var n = new GenericInstanceType(g.ElementType);
        foreach (TypeReference a in g.GenericArguments)
        {
            n.GenericArguments.Add(Subst(a, declaring));
        }

        return n;
    }

    return t switch
    {
        ArrayType at => new ArrayType(Subst(at.ElementType, declaring)),
        ByReferenceType br => new ByReferenceType(Subst(br.ElementType, declaring)),
        _ => t,
    };
}
