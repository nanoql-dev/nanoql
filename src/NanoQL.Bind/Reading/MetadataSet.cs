// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.InteropServices;

namespace NanoQL.Bind.Reading;

/// <summary>
/// The assemblies the bindings cross, read through a <see cref="MetadataLoadContext"/>: their metadata is inspected and
/// nothing in them runs. Types from the context are compared by name, never with <c>typeof</c>, which names the tool's
/// own runtime's types.
/// </summary>
internal sealed class MetadataSet : IDisposable
{
    private readonly MetadataLoadContext context;
    private readonly HashSet<Assembly> ours;
    private readonly Dictionary<Assembly, string> prefixes;

    private MetadataSet(MetadataLoadContext context, IReadOnlyList<(string Prefix, Assembly Assembly)> assemblies)
    {
        this.context = context;
        Assemblies = assemblies;
        ours = [.. assemblies.Select(a => a.Assembly)];
        prefixes = assemblies.ToDictionary(a => a.Assembly, a => a.Prefix);
        Core = assemblies.Select(a => a.Assembly).Single(a => a.GetName().Name == "NanoQL");
    }

    /// <summary>Gets the inputs, in the order given, with their prefixes.</summary>
    public IReadOnlyList<(string Prefix, Assembly Assembly)> Assemblies { get; }

    /// <summary>Gets nanoql's core assembly, which every set holds.</summary>
    public Assembly Core { get; }

    /// <summary>Opens the inputs, resolving their references from their folders and the running runtime's.</summary>
    /// <param name="inputs">The assemblies and their prefixes; nanoql's core among them.</param>
    /// <returns>The set.</returns>
    public static MetadataSet Open(IReadOnlyList<AssemblyInput> inputs)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dll in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
        {
            paths.TryAdd(Path.GetFileName(dll), dll);
        }

        foreach (string dir in inputs.Select(i => Path.GetDirectoryName(i.Path)!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
            {
                paths.TryAdd(Path.GetFileName(dll), dll);
            }
        }

        foreach (AssemblyInput input in inputs)
        {
            paths[Path.GetFileName(input.Path)] = input.Path;
        }

        var context = new MetadataLoadContext(new PathAssemblyResolver(paths.Values));
        var loaded = inputs.Select(i => (i.Prefix, context.LoadFromAssemblyPath(i.Path))).ToList();
        if (loaded.Count(a => a.Item2.GetName().Name == "NanoQL") != 1)
        {
            throw new ArgumentException("the inputs hold nanoql's core assembly, NanoQL.dll, once", nameof(inputs));
        }

        return new MetadataSet(context, loaded);
    }

    /// <summary>Whether a type is declared in one of the inputs.</summary>
    /// <param name="t">The type.</param>
    /// <returns>True for the inputs' own types.</returns>
    public bool Ours(Type t) => ours.Contains(t.Assembly);

    /// <summary>The prefix of the input that declares a type.</summary>
    /// <param name="t">One of the inputs' types.</param>
    /// <returns>Its prefix.</returns>
    public string PrefixOf(Type t) => prefixes[t.Assembly];

    /// <summary>A core type by its full name.</summary>
    /// <param name="fullName">The name.</param>
    /// <returns>The type.</returns>
    public Type CoreType(string fullName) =>
        Core.GetType(fullName) ?? throw new InvalidOperationException($"nanoql's core has no type {fullName}");

    /// <summary>A type of the runtime's core library by its full name.</summary>
    /// <param name="fullName">The name.</param>
    /// <returns>The type.</returns>
    public Type SystemType(string fullName) =>
        context.CoreAssembly!.GetType(fullName) ?? throw new InvalidOperationException($"no system type {fullName}");

    /// <inheritdoc/>
    public void Dispose() => context.Dispose();
}
