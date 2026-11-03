// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind;

/// <summary>
/// One run of the generator: the inputs read, the surface selected, and its outputs rendered: the headers,
/// <c>surface.json</c> and <c>coverage.md</c>, which <c>bindings/</c> checks in, and the shims, which the native host
/// compiles. The outputs are rendered while the inputs' metadata is open, since the writers read it.
/// </summary>
internal sealed class Generation
{
    private readonly Dictionary<string, string> shims = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> twins = new(StringComparer.Ordinal);

    private Generation(Surface surface, Dictionary<string, string> files)
    {
        Surface = surface;
        Files = files;
    }

    /// <summary>Gets the surface.</summary>
    public Surface Surface { get; }

    /// <summary>Gets the C replayer's generated dispatch.</summary>
    public string CDispatch { get; private init; } = string.Empty;

    /// <summary>Gets the Excel build's outputs, when the run made one.</summary>
    public ExcelOutput? Excel { get; private init; }

    /// <summary>Gets the checked-in outputs by their path under <c>bindings/</c>.</summary>
    public IReadOnlyDictionary<string, string> Files { get; }

    /// <summary>Reads the inputs, selects their surface and renders its outputs.</summary>
    /// <param name="inputs">The assemblies and their prefixes.</param>
    /// <param name="roles">The roles tables: <c>bindings/roles.json</c> and a user assembly's own.</param>
    /// <param name="shipped">The last release's <c>surface.shipped.json</c>, absent before the first.</param>
    /// <param name="excel">An Excel build's options, or null for none.</param>
    /// <returns>The run.</returns>
    public static Generation Run(IReadOnlyList<AssemblyInput> inputs, IEnumerable<string> roles, string? shipped, ExcelOptions? excel = null)
    {
        using MetadataSet set = MetadataSet.Open(inputs);
        ExceptionDocs docs = ExceptionDocs.Read(set);
        Surface surface = Selector.Select(set, RolesTable.Read(roles), ShippedSurface.Read(shipped), docs);
        FindWritten(set, surface, docs);
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["c/include/nanoql.h"] = HeaderWriter.Write(surface, "nql"),
            ["surface.json"] = SurfaceWriter.Write(surface),
            ["coverage.md"] = CoverageWriter.Write(surface),
        };
        foreach (string prefix in surface.Assemblies.Select(a => a.Prefix).Distinct().Where(p => p != "nql"))
        {
            files[$"c/include/{prefix}.h"] = HeaderWriter.Write(surface, prefix);
        }

        var g = new Generation(surface, files) { CDispatch = new CReplayWriter(surface).Write(), Excel = excel == null ? null : BuildExcel(surface, excel) };
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-" + Guid.NewGuid().ToString("N"));
        try
        {
            new ShimWriter(surface).Write(dir);
            foreach (string f in Directory.EnumerateFiles(dir))
            {
                g.shims[Path.GetFileName(f)] = File.ReadAllText(f);
            }

            string twinDir = Path.Combine(dir, "twins");
            new TwinWriter(surface).Write(twinDir);
            foreach (string f in Directory.EnumerateFiles(twinDir))
            {
                g.twins[Path.GetFileName(f)] = File.ReadAllText(f);
            }
        }
        finally
        {
            // Best effort: another process (a file scanner) may still hold a file just written; the folder is temporary.
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        return g;
    }

    // An Excel build (docs/excel.md): its functions selected and written, its surface and counts rendered.
    private static ExcelOutput BuildExcel(Surface surface, ExcelOptions options)
    {
        ExcelSelection selection = ExcelSelection.Select(surface, options, ExcelShipped.Read(options.Shipped));
        var writer = new ExcelWriter(surface, selection, options);
        Dictionary<string, string> files = writer.Write();
        string templates = writer.Templates();
        int own = ExcelManifest.Own(options.TestBuild).Count;
        int experimental = selection.Units.Count(u => u.Experimental);
        string skips = string.Join("; ", selection.Skips.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Value:N0} {kv.Key}"));
        string summary = $"{selection.Units.Count:N0} functions ({experimental:N0} experimental), {selection.Units.Count + own:N0} with the add-in's own {own}; "
            + $"{selection.Withheld.Count:N0} withheld by the completeness check{(selection.StableWithheld > 0 ? $" ({selection.StableWithheld:N0} by the stable build's)" : string.Empty)}; "
            + $"{selection.Inherited:N0} inherited, {selection.Extensions:N0} extension methods; "
            + $"{selection.Units.Count(u => u.Family != null):N0} families of {selection.Units.Where(u => u.Family != null).Sum(u => u.Family!.Count):N0} constructions; "
            + $"help: {writer.Documented:N0} documented ({writer.Inherited:N0} through inheritdoc), {writer.Cut:N0} cut to {ExcelDocs.Limit}; "
            + $"looping {writer.Looping:N0}; nullable left out {writer.NullableLeftOut:N0}; withheld by Excel's filter: {skips}";
        return new ExcelOutput(selection, files, templates, ExcelManifest.Write(selection, options), summary) { Registered = selection.Units.Count + own };
    }

    /// <summary>Writes the shims into a folder, replacing its <c>.g.cs</c> files.</summary>
    /// <param name="dir">The folder.</param>
    public void WriteShims(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.EnumerateFiles(dir, "*.g.cs"))
        {
            File.Delete(old);
        }

        foreach ((string name, string text) in shims)
        {
            File.WriteAllText(Path.Combine(dir, name), text);
        }
    }

    // The Matrix and Vector parameters each function's member writes into, from IL; each crosses in and out: vector&
    // and matrix&, double* with its counts, the shim copying the C# array back after the call.
    private static void FindWritten(MetadataSet set, Surface surface, ExceptionDocs docs)
    {
        using var writes = new ParameterWrites(set, set.CoreType("NanoQL.Numerics.Matrix"), set.CoreType("NanoQL.Numerics.Vector"), docs);
        foreach (CFunction f in surface.Functions.Where(f => f.Member is MethodBase))
        {
            var m = (MethodBase)f.Member!;
            ParameterInfo[] ps = m.GetParameters();
            foreach (CParam p in f.Params.Where(p => !p.IsSelf && p.Kind.Name is "matrix" or "vector").ToList())
            {
                int i = Array.FindIndex(ps, q => q.Name == p.Name);
                if (i >= 0 && writes.Writes(m, i))
                {
                    CParam inOut = p with { Kind = p.Kind with { Name = p.Kind.Name + "&" } };
                    f.Params[f.Params.IndexOf(p)] = inOut;
                    surface.Written.Add((f, inOut));
                }
                else if (i >= 0 && writes.WrittenElsewhere(m, i) is { } prefix)
                {
                    surface.WrittenElsewhere.Add((f, p, prefix));
                }
            }
        }
    }

    /// <summary>Writes the recorder's twins and their map into a folder.</summary>
    /// <param name="dir">The folder.</param>
    public void WriteTwins(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach ((string name, string text) in twins)
        {
            File.WriteAllText(Path.Combine(dir, name), text);
        }
    }

    /// <summary>The C++ headers: <c>nanoql.hpp</c>, and a user prefix's own.</summary>
    /// <returns>Each header's text by its file name.</returns>
    public Dictionary<string, string> CppHeaders()
    {
        using Stream s = typeof(Generation).Assembly.GetManifestResourceStream("NanoQL.Bind.runtime.hpp")
            ?? throw new InvalidOperationException("the C++ runtime isn't embedded in the tool");
        using var reader = new StreamReader(s);
        return new CppWriter(Surface).Write(reader.ReadToEnd());
    }

    /// <summary>Writes the Java layer's sources (Java, docs/bindings.md) into a folder, a directory per package.</summary>
    /// <param name="dir">The sources' root.</param>
    /// <param name="prefixes">The prefixes whose classes to write, every one when null.</param>
    /// <returns>The files written.</returns>
    public int WriteJava(string dir, IReadOnlyCollection<string>? prefixes = null) => new JavaWriter(Surface).Write(dir, prefixes);

    /// <summary>The C++ replayer's dispatch files.</summary>
    /// <returns>Each file's text by its name.</returns>
    public Dictionary<string, string> CppDispatch() => new CppReplayWriter(new CppWriter(Surface)).Write();

    /// <summary>The Java replayer's dispatch classes.</summary>
    /// <returns>Each file's text by its path under the test sources' root.</returns>
    public Dictionary<string, string> JavaDispatch() => new JavaReplayWriter(new JavaWriter(Surface)).Write();

    /// <summary>Writes the Python package's generated modules into its folder.</summary>
    /// <param name="package">The package's folder (<c>nanoql/</c>).</param>
    /// <returns>The functions, classes and enums written.</returns>
    public (int Functions, int Classes, int Enums) WritePython(string package) => new PythonWriter(Surface).Write(package);

    /// <summary>The coverage report's counts.</summary>
    /// <returns>The counts section.</returns>
    public string Report()
    {
        string coverage = Files["coverage.md"];
        int at = coverage.IndexOf("## Counts", StringComparison.Ordinal);
        int end = coverage.IndexOf("## What doesn't cross", StringComparison.Ordinal);
        return coverage[at..end];
    }
}
