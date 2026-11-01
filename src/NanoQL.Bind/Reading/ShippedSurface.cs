// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NanoQL.Bind.Reading;

/// <summary>
/// The last release's surface, <c>bindings/surface.shipped.json</c>, which each release writes and which is absent
/// before the first: every released function's C name by its member key, and every released type's C name, so a
/// released name never moves (docs/bindings.md, "Names").
/// </summary>
internal sealed class ShippedSurface
{
    private readonly Dictionary<string, string> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> types = new(StringComparer.Ordinal);

    /// <summary>Gets an empty surface: nothing released.</summary>
    public static ShippedSurface None { get; } = new();

    /// <summary>Gets the released functions' C names.</summary>
    public IReadOnlyCollection<string> FunctionNames => functions.Values;

    /// <summary>Gets the released types' C names.</summary>
    public IReadOnlyCollection<string> TypeNames => types.Values;

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
                shipped.functions[k] = f.GetProperty("name").GetString()!;
            }
        }

        foreach (JsonElement t in doc.RootElement.GetProperty("types").EnumerateArray())
        {
            shipped.types[t.GetProperty("name").GetString()!] = t.GetProperty("cname").GetString()!;
        }

        return shipped;
    }

    /// <summary>A released function's C name, or null.</summary>
    /// <param name="key">The function's key in <c>surface.json</c>.</param>
    /// <returns>The name, or null for a member not released.</returns>
    public string? Function(string key) => functions.GetValueOrDefault(key);

    /// <summary>A released type's C name, or null.</summary>
    /// <param name="fullName">The type's full name.</param>
    /// <returns>The name, or null for a type not released.</returns>
    public string? Type(string fullName) => types.GetValueOrDefault(fullName);
}
