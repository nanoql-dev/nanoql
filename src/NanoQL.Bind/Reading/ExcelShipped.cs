// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NanoQL.Bind.Reading;

/// <summary>
/// The last release's Excel names, <c>bindings/excel/functions.shipped.json</c> (XLR1's freezing): each released name by
/// its function's C name, or by its family's key, so a construction added later joins its family under the released
/// name (XLD29); and the retired names, a removed function's, which no later function takes. Absent before 2.0.
/// </summary>
internal sealed class ExcelShipped
{
    private ExcelShipped()
    {
    }

    /// <summary>Gets each released name by its function's identity: <c>c:</c> and its C name, or <c>family:</c> and its key.</summary>
    public Dictionary<string, string> Names { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets every name the file holds, released or retired, none of which a new function takes.</summary>
    public HashSet<string> Reserved { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the file, or none when it is absent.</summary>
    /// <param name="path">The file's path, or null.</param>
    /// <returns>The names, empty before the first release.</returns>
    public static ExcelShipped Read(string? path)
    {
        var s = new ExcelShipped();
        if (path == null || !File.Exists(path))
        {
            return s;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement f in doc.RootElement.GetProperty("functions").EnumerateArray())
        {
            string excel = f.GetProperty("excel").GetString()!;
            s.Reserved.Add(excel);
            if (f.TryGetProperty("key", out JsonElement key))
            {
                s.Names["family:" + key.GetString()] = excel;
            }
            else if (f.TryGetProperty("c", out JsonElement c))
            {
                s.Names["c:" + c.GetString()] = excel;
            }
        }

        if (doc.RootElement.TryGetProperty("retired", out JsonElement retired))
        {
            foreach (JsonElement r in retired.EnumerateArray())
            {
                s.Reserved.Add(r.GetString()!);
            }
        }

        return s;
    }
}
