// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NanoQL.Bind.Reading;

/// <summary>
/// The roles table, <c>bindings/roles.json</c>, written by hand: for a member that takes an engine and supplies an
/// argument of its own that names alone can place (a value, an object two of the engine's parameters can take, a
/// calibration helper's trial model), the engine parameters' names that argument goes into. A user assembly's own
/// table sits beside it and adds to it.
/// </summary>
internal sealed class RolesTable
{
    private readonly Dictionary<(string Member, int Argument), string[]> roles = [];

    /// <summary>Gets the entries, for the coverage report.</summary>
    public IEnumerable<KeyValuePair<(string Member, int Argument), string[]>> Entries =>
        roles.OrderBy(r => r.Key.Member, StringComparer.Ordinal).ThenBy(r => r.Key.Argument);

    /// <summary>Reads the tables; a path that doesn't exist adds nothing.</summary>
    /// <param name="paths">The tables' paths.</param>
    /// <returns>The roles.</returns>
    public static RolesTable Read(IEnumerable<string> paths)
    {
        var table = new RolesTable();
        foreach (string path in paths.Where(File.Exists))
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            if (doc.RootElement.GetProperty("schema").GetInt32() != 1)
            {
                throw new InvalidDataException($"{path}: roles schema 1 expected");
            }

            foreach (JsonElement e in doc.RootElement.GetProperty("roles").EnumerateArray())
            {
                string member = e.GetProperty("member").GetString()!;
                int argument = e.GetProperty("argument").GetInt32();
                string[] names = [.. e.GetProperty("parameters").EnumerateArray().Select(n => n.GetString()!)];
                if (!table.roles.TryAdd((member, argument), names))
                {
                    throw new InvalidDataException($"{path}: {member} argument {argument} has two entries");
                }
            }
        }

        return table;
    }

    /// <summary>The engine parameters' names a member's supplied argument goes into, or null.</summary>
    /// <param name="member">The member: its type's full name, a dot, its name.</param>
    /// <param name="argument">The argument's index among those the member supplies after the instrument and the context.</param>
    /// <returns>The names, or null when the table has no entry.</returns>
    public string[]? For(string member, int argument) => roles.GetValueOrDefault((member, argument));
}
