// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The generator's runs the tests read, each made once: nanoql's two assemblies alone, and with this assembly's fixtures
/// under the prefix <c>fx</c>, as a user's assembly is bound beside nanoql's.
/// </summary>
internal static class Surfaces
{
    private static readonly Lazy<Generation> NanoqlRun = new(() => Run(null));
    private static readonly Lazy<Generation> FixturesRun = new(() => Run(null, fixtures: true));

    /// <summary>Gets the repository: the nearest folder above the tests holding <c>bindings/roles.json</c>.</summary>
    public static string Repository { get; } = FindRepository();

    /// <summary>Gets the run over nanoql's assemblies alone.</summary>
    public static Generation Nanoql => NanoqlRun.Value;

    /// <summary>Gets the run over nanoql's assemblies and the fixtures.</summary>
    public static Generation WithFixtures => FixturesRun.Value;

    /// <summary>A run of the generator.</summary>
    /// <param name="shipped">The last release's surface, or null for none.</param>
    /// <param name="fixtures">Whether the fixtures are bound beside nanoql.</param>
    /// <param name="roles">The roles table, or null for the repository's.</param>
    /// <returns>The run.</returns>
    public static Generation Run(string? shipped, bool fixtures = false, string? roles = null)
    {
        var inputs = new List<AssemblyInput>
        {
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")),
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")),
        };
        if (fixtures)
        {
            inputs.Add(new("fx", typeof(Surfaces).Assembly.Location));
        }

        return Generation.Run(inputs, [roles ?? Path.Combine(Repository, "bindings", "roles.json")], shipped);
    }

    /// <summary>A function's declaration in its prefix's header, without its comment.</summary>
    /// <param name="g">The run.</param>
    /// <param name="name">The function's C name.</param>
    /// <returns>The declaration's line, or null when the header has none.</returns>
    public static string? Declaration(Generation g, string name)
    {
        string prefix = name[..name.IndexOf('_', StringComparison.Ordinal)];
        string header = g.Files[prefix == "nql" ? "c/include/nanoql.h" : $"c/include/{prefix}.h"];
        return header.Split('\n').FirstOrDefault(l => l.Contains($" {name}(", StringComparison.Ordinal));
    }

    private static string FindRepository()
    {
        for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
        {
            if (File.Exists(Path.Combine(d, "bindings", "roles.json")))
            {
                return d;
            }
        }

        throw new DirectoryNotFoundException("no bindings/roles.json above the tests");
    }
}
