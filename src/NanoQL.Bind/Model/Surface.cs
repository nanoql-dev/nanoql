// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>
/// The C surface of a set of assemblies: what crosses (types, enums, functions, presets by name, engine input sets and
/// the pairs they form with the members that take an engine) and what doesn't, with why. The emitters write it out.
/// </summary>
internal sealed class Surface
{
    /// <summary>Gets the inputs' prefixes and assembly names, in the order given.</summary>
    public List<(string Prefix, string Assembly)> Assemblies { get; } = [];

    /// <summary>Gets the types, ordered by full name.</summary>
    public List<TypeRow> Types { get; } = [];

    /// <summary>Gets the enums, ordered by full name.</summary>
    public List<EnumRow> Enums { get; } = [];

    /// <summary>Gets the functions, ordered by C name.</summary>
    public List<CFunction> Functions { get; } = [];

    /// <summary>Gets the members and types that don't cross.</summary>
    public List<SkipRow> Skips { get; } = [];

    /// <summary>Gets the withheld preset constructors with their lookups by name.</summary>
    public List<PresetRow> Presets { get; } = [];

    /// <summary>Gets the engine input sets.</summary>
    public List<EngineInputSet> EngineSets { get; } = [];

    /// <summary>Gets the engine parameters, by adapter index.</summary>
    public List<EngineParameter> EngineParameters { get; } = [];

    /// <summary>Gets every member–engine pair, formed or not.</summary>
    public List<EnginePair> EnginePairs { get; } = [];

    /// <summary>Gets the roles table's entries, for the coverage report.</summary>
    public List<(string Member, int Argument, string[] Parameters)> Roles { get; } = [];

    /// <summary>Gets the functions' <c>Matrix</c> and <c>Vector</c> parameters their members write into.</summary>
    public List<(CFunction Function, CParam Parameter)> Written { get; } = [];

    /// <summary>
    /// Gets the parameters only another prefix's override writes (a user's, where nanoql's member reads), with that
    /// prefix: they cross as the member's own do, the override's write lost through them.
    /// </summary>
    public List<(CFunction Function, CParam Parameter, string Prefix)> WrittenElsewhere { get; } = [];
}
