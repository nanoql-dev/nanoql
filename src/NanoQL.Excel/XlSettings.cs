// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using NanoQL.Extensions.Conventions;

namespace NanoQL.Excel;

/// <summary>
/// The add-in's settings file (XLR9): <c>&lt;xll name&gt;.settings.json</c> beside the <c>.xll</c>, so each build reads
/// its own (XLD28), with the keys <c>"conventions"</c> (a path, relative to the file's folder) and
/// <c>"recordCalls"</c> (XLR14); <c>NANOQL_CONVENTIONS</c> overrides <c>"conventions"</c>, a relative path then
/// resolving against the <c>.xll</c>'s folder. The named file loads as <see cref="ConventionProvider.Default"/>. A failed
/// load (a missing or malformed file, an unknown key, a key in another case, a wrong type) never falls back to
/// QuantLib's conventions: Default becomes the refusing provider, its every lookup refusing with the load's error.
/// </summary>
internal static class XlSettings
{
    /// <summary>Gets the failed load's error, or null when the conventions loaded or none were named.</summary>
    public static string? LoadError { get; private set; }

    /// <summary>Gets the loaded conventions file's bytes, which a repro export embeds, or null.</summary>
    public static byte[]? ConventionsBytes { get; private set; }

    /// <summary>Loads the settings of the add-in at a path, setting the default conventions and the provider's identity.</summary>
    /// <param name="xll">The <c>.xll</c>'s path.</param>
    /// <param name="environment">The value of <c>NANOQL_CONVENTIONS</c>, or null.</param>
    public static void Load(string xll, string? environment)
    {
        string dir = Path.GetDirectoryName(xll)!;
        string file = Path.Combine(dir, Path.GetFileNameWithoutExtension(xll) + ".settings.json");
        string? path = null, from = null;
        bool record = true;
        LoadError = null;
        ConventionsBytes = null;
        try
        {
            if (File.Exists(file))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("the settings file isn't a JSON object");
                }

                foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                {
                    switch (prop.Name)
                    {
                        case "conventions" when prop.Value.ValueKind == JsonValueKind.String:
                            path = prop.Value.GetString();
                            from = dir;
                            break;
                        case "recordCalls" when prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                            record = prop.Value.GetBoolean();
                            break;
                        case "conventions" or "recordCalls":
                            throw new InvalidDataException($"the setting \"{prop.Name}\" has a value of the wrong type ({prop.Value.ValueKind})");
                        default:
                            throw new InvalidDataException($"the settings file has a key the add-in doesn't know: \"{prop.Name}\" (it knows \"conventions\" and \"recordCalls\")");
                    }
                }
            }

            X.RecordCalls = record;
            if (!string.IsNullOrEmpty(environment))
            {
                path = environment;
                from = dir;
            }

            if (path == null)
            {
                ConventionProvider.Default = ConventionProvider.QuantLib;
                X.ProviderId = "QuantLib";
                return;
            }

            path = Path.GetFullPath(path, from!);
            byte[] bytes = File.ReadAllBytes(path);
            ConventionSnapshot snapshot = ConventionsXml.Read(new MemoryStream(bytes, false), path);
            ConventionProvider.Default = ConventionSet.QuantLib.With(snapshot);
            X.ProviderId = "file:" + Convert.ToHexString(SHA256.HashData(bytes), 0, 8);
            ConventionsBytes = bytes;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LoadError = $"the add-in's conventions didn't load ({path ?? file}): {e.GetType().Name}: {e.Message}";
            ConventionProvider.Default = new RefusingProvider(LoadError);
            X.ProviderId = "failed";
        }
    }
}
