// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// Writes <c>surface.json</c>, the surface's language-neutral description every back end reads, and the Excel back end
/// may (docs/bindings.md, "surface.json"): its schema version, the assemblies, the types, the enums, every function
/// with its C name, kind, parameters with their kinds and defaults, result and member key, its C parameters, the
/// presets by name and the engine input sets. One row a line, so a change shows as lines in a diff.
/// </summary>
internal static class SurfaceWriter
{
    /// <summary>The schema's version, raised on any change of meaning.</summary>
    public const int Schema = 1;

    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

    /// <summary>The surface as JSON text.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The text, LF line endings.</returns>
    public static string Write(Surface surface)
    {
        var sb = new StringBuilder();
        sb.Append("{\"schema\": ").Append(Schema).Append(",\n \"version\": \"").Append(Versions.Text).Append("\", \"abi\": ").Append(Versions.Abi).Append(",\n");
        Section(sb, "assemblies", surface.Assemblies, (w, a) =>
        {
            w.WriteString("prefix", a.Prefix);
            w.WriteString("name", a.Assembly);
        });
        Section(sb, "types", surface.Types, (w, t) =>
        {
            w.WriteString("name", t.Type.FullName);
            w.WriteString("prefix", t.Prefix);
            w.WriteString("cname", t.CName);
            w.WriteString("base", t.Base);
            w.WriteStartArray("interfaces");
            foreach (string i in t.Interfaces)
            {
                w.WriteStringValue(i);
            }

            w.WriteEndArray();
            w.WriteString("shape", t.Shape);
            w.WriteString("form", t.Form);
            w.WriteBoolean("experimental", t.Experimental);
        });
        Section(sb, "enums", surface.Enums, (w, e) =>
        {
            w.WriteString("name", e.Type.FullName);
            w.WriteString("prefix", e.Prefix);
            w.WriteString("cname", e.CName);
            w.WriteBoolean("experimental", e.Experimental);
            w.WriteBoolean("flags", e.Flags);
            w.WriteStartArray("members");
            foreach ((string name, long value) in e.Members)
            {
                w.WriteStartArray();
                w.WriteStringValue(name);
                w.WriteNumberValue(value);
                w.WriteEndArray();
            }

            w.WriteEndArray();
        });
        Section(sb, "functions", surface.Functions, Function);
        Section(sb, "presets", surface.Presets, (w, p) =>
        {
            w.WriteString("dictionary", p.Factory.Dictionary);
            w.WriteString("name", p.Factory.Name);
            w.WriteString("member", p.Construction.Constructor.MemberKey());
            w.WriteString("lookup", p.Lookup?.CName);
            w.WriteStartArray("args");
            foreach (PresetArgument a in p.Construction.Arguments)
            {
                w.WriteStartObject();
                if (a.IsConstant)
                {
                    w.WritePropertyName("const");
                    Value(w, a.Constant);
                }
                else if (a.Parameter >= 0)
                {
                    w.WriteNumber("param", a.Parameter);
                }
                else
                {
                    w.WriteBoolean("computed", true);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
        });
        Section(sb, "engines", surface.EngineSets, (w, s) =>
        {
            w.WriteString("id", s.Id);
            w.WriteString("engine", s.Engine.FullName);
            w.WriteStartArray("inputs");
            foreach (System.Reflection.ParameterInfo p in s.Inputs)
            {
                w.WriteStartObject();
                w.WriteString("name", p.Name);
                w.WriteString("type", p.ParameterType.KeyName());
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("calculations");
            foreach (System.Reflection.MethodInfo c in s.Calculations)
            {
                w.WriteStringValue(c.MemberKey());
            }

            w.WriteEndArray();
        }, last: true);
        sb.Append("}\n");
        return sb.ToString();
    }

    private static void Function(Utf8JsonWriter w, CFunction f)
    {
        w.WriteString("name", f.CName);
        w.WriteString("kind", f.Kind);
        w.WriteString("type", f.Type.FullName);
        w.WriteString("member", f.Member?.Name ?? f.BaseName);
        w.WriteString("key", f.Key);
        w.WriteBoolean("static", f.Static);
        w.WriteBoolean("experimental", f.Experimental);
        if (f.Deprecated != null)
        {
            w.WriteString("deprecated", f.Deprecated);
        }

        w.WriteBoolean("overloaded", f.Overloaded);
        if (f.InputSet != null)
        {
            w.WriteString("set", f.InputSet.Id);
        }

        w.WriteStartArray("params");
        foreach (CParam p in f.Params)
        {
            w.WriteStartObject();
            w.WriteString("name", p.Name);
            Kind(w, p.Kind);
            if (p.Optional)
            {
                w.WriteBoolean("optional", true);
                w.WritePropertyName("default");
                Value(w, p.Default);
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        if (f.Return != null)
        {
            w.WriteStartObject("returns");
            Kind(w, f.Return);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("returns");
        }

        w.WriteStartArray("c");
        foreach (CSlot s in CForms.All(f))
        {
            w.WriteStartArray();
            w.WriteStringValue(s.CType);
            w.WriteStringValue(s.Name);
            w.WriteEndArray();
        }

        w.WriteEndArray();
    }

    private static void Kind(Utf8JsonWriter w, CKind k)
    {
        w.WriteString("kind", k.Text);
        w.WriteString("type", k.Type.KeyName());
        if (k.Element != null)
        {
            w.WriteString("element", k.Element.Type.KeyName());
        }

        if (k.Value != null)
        {
            w.WriteString("value", k.Value.Type.KeyName());
        }

        IReadOnlyList<string> itemNames = k.ItemNames.Count > 0 ? k.ItemNames : k.Element?.ItemNames ?? [];
        if (itemNames.Count > 0)
        {
            w.WriteStartArray("items");
            foreach (string n in itemNames)
            {
                w.WriteStringValue(n);
            }

            w.WriteEndArray();
        }

        if (k.Engine != null)
        {
            w.WriteNumber("adapter", k.Engine.Index);
        }
    }

    private static void Value(Utf8JsonWriter w, object? v)
    {
        switch (v)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case string s: w.WriteStringValue(s); break;
            case double d when double.IsFinite(d): w.WriteRawValue(d.ToString("R", CultureInfo.InvariantCulture)); break;
            case double d: w.WriteStringValue(d.ToString(CultureInfo.InvariantCulture)); break;
            case float fl: w.WriteRawValue(((double)fl).ToString("R", CultureInfo.InvariantCulture)); break;
            default: w.WriteRawValue(Convert.ToString(v, CultureInfo.InvariantCulture)!); break;
        }
    }

    private static void Section<T>(StringBuilder sb, string name, IReadOnlyList<T> rows, Action<Utf8JsonWriter, T> write, bool last = false)
    {
        sb.Append(" \"").Append(name).Append("\": [");
        for (int i = 0; i < rows.Count; i++)
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, Options))
            {
                w.WriteStartObject();
                write(w, rows[i]);
                w.WriteEndObject();
            }

            sb.Append(i == 0 ? "\n  " : ",\n  ").Append(Encoding.UTF8.GetString(ms.ToArray()));
        }

        sb.Append(rows.Count > 0 ? "\n ]" : "]").Append(last ? "\n" : ",\n");
    }
}
