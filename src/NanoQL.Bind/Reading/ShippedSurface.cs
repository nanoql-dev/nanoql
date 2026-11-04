// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

namespace NanoQL.Bind.Reading;

/// <summary>
/// The last release's surface, <c>bindings/surface.shipped.json</c>, which each release writes and which is absent
/// before the first: every released function's C name by the type it crosses under and its member key, since an
/// inherited member crosses under each type that inherits it, and every released type's C name, an enum's among them,
/// so a released name never moves (docs/bindings.md, "Names"); and the retired names, those an earlier release shipped
/// and the surface has since dropped, which no later function or type takes. Type names are read without their
/// assemblies' version, culture and public key token, which a closed generic's arguments carry and a release or a
/// runtime moves. A type's name is held within its prefix, as its functions' are: a released <c>nql_Calendar</c> leaves
/// a user's type of another prefix its own <c>Calendar</c>.
/// </summary>
internal sealed class ShippedSurface
{
    private readonly Dictionary<string, string> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> types = new(StringComparer.Ordinal);
    private readonly HashSet<string> typeNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> retiredFunctions = new(StringComparer.Ordinal);
    private readonly HashSet<string> retiredTypes = new(StringComparer.Ordinal);

    /// <summary>Gets an empty surface: nothing released.</summary>
    public static ShippedSurface None { get; } = new();

    /// <summary>Gets the released functions' C names, the retired ones among them.</summary>
    public IReadOnlyCollection<string> FunctionNames => [.. functions.Values, .. retiredFunctions];

    /// <summary>
    /// Gets the released types' C names, each after its prefix (<c>nql_Calendar</c>), the retired ones among them.
    /// </summary>
    public IReadOnlyCollection<string> TypeNames => [.. typeNames, .. retiredTypes];

    /// <summary>Reads a released surface; a missing file is <see cref="None"/>.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The surface.</returns>
    public static ShippedSurface Read(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return None;
        }

        var shipped = new ShippedSurface();
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        foreach (JsonElement f in doc.RootElement.GetProperty("functions").EnumerateArray())
        {
            if (f.TryGetProperty("key", out JsonElement key) && key.GetString() is { } k)
            {
                shipped.functions[Unversioned(f.GetProperty("type").GetString()!) + "|" + k] = f.GetProperty("name").GetString()!;
            }
        }

        // An enum is a type to the names, its C name a typedef a user's code compiles against.
        foreach (JsonElement t in doc.RootElement.GetProperty("types").EnumerateArray().Concat(doc.RootElement.GetProperty("enums").EnumerateArray()))
        {
            string cname = t.GetProperty("cname").GetString()!;
            shipped.types[Unversioned(t.GetProperty("name").GetString()!)] = cname;
            shipped.typeNames.Add(t.GetProperty("prefix").GetString() + "_" + cname);
        }

        if (doc.RootElement.TryGetProperty("retired", out JsonElement retired))
        {
            shipped.retiredFunctions.UnionWith(retired.GetProperty("functions").EnumerateArray().Select(n => n.GetString()!));
            shipped.retiredTypes.UnionWith(retired.GetProperty("types").EnumerateArray().Select(n => n.GetString()!));
        }

        return shipped;
    }

    /// <summary>A released function's C name, or null.</summary>
    /// <param name="type">The full name of the type the function crosses under.</param>
    /// <param name="key">The function's key in <c>surface.json</c>.</param>
    /// <returns>The name, or null for a member not released under that type.</returns>
    public string? Function(string type, string key) => functions.GetValueOrDefault(Unversioned(type) + "|" + key);

    /// <summary>A released type's C name, or null.</summary>
    /// <param name="fullName">The type's full name.</param>
    /// <returns>The name, or null for a type not released.</returns>
    public string? Type(string fullName) => types.GetValueOrDefault(Unversioned(fullName));

    // A type's full name without the version, culture and public key token its generic arguments' assembly-qualified
    // names carry.
    private static string Unversioned(string name) =>
        Regex.Replace(name, @", (?:Version|Culture|PublicKeyToken)=[^,\]]*", string.Empty, RegexOptions.CultureInvariant);
}
