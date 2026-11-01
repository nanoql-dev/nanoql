// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Emitting;

/// <summary>A function's C# member as a reader names it, with its parameters' types and defaults, for the header's comments.</summary>
internal static class Describe
{
    /// <summary>The member's description: <c>Calendar.Advance(Date date, int n, ...) -> Date</c>.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The description.</returns>
    public static string Of(CFunction f)
    {
        string member = f.Kind switch
        {
            "new" => "new " + TypeNamer.Simple(f.Type),
            "draws" => TypeNamer.Simple(f.Type) + " snapshot: Draws",
            "bind" or "bindon" => TypeNamer.Simple(f.Type) + "." + f.BaseName,
            _ => TypeNamer.Simple(f.Type) + "." + (f.Member?.Name ?? f.BaseName),
        };
        string ps = string.Join(", ", f.Params.Where(p => !p.IsSelf).Select(p => Type(p.Kind.Type) + " " + p.Name + (p.Optional ? " = " + Default(p) : string.Empty)));
        return $"{member}({ps})" + (f.Return != null && f.Kind != "new" ? " -> " + Type(f.Return.Type) : string.Empty);
    }

    /// <summary>A type's short C# name: simple names, generic arguments kept.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The name.</returns>
    public static string Type(Type t) => t.FullName switch
    {
        "System.Double" => "double",
        "System.Int32" => "int",
        "System.Int64" => "long",
        "System.Boolean" => "bool",
        "System.String" => "string",
        "System.Object" => "object",
        _ when t.NullableOf() is { } u => Type(u) + "?",
        _ when t.IsArray => Type(t.GetElementType()!) + "[]",
        _ when t.IsGenericType => TypeNamer.Simple(t) + "<" + string.Join(", ", t.GetGenericArguments().Select(Type)) + ">",
        _ => TypeNamer.Simple(t),
    };

    /// <summary>An optional parameter's default as a reader writes it: an enum's member name, a number, <c>null</c>.</summary>
    /// <param name="p">The parameter.</param>
    /// <returns>The default.</returns>
    public static string Default(CParam p)
    {
        Type t = p.Kind.Type.NullableOf() ?? p.Kind.Type;
        if (p.Default == null)
        {
            return "null";
        }

        if (t.IsEnum)
        {
            long v = Convert.ToInt64(p.Default, CultureInfo.InvariantCulture);
            string? name = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(fi => Convert.ToInt64(fi.GetRawConstantValue(), CultureInfo.InvariantCulture) == v)?.Name;
            return name ?? v.ToString(CultureInfo.InvariantCulture);
        }

        return p.Default switch
        {
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            string s => "\"" + s + "\"",
            _ => Convert.ToString(p.Default, CultureInfo.InvariantCulture)!,
        };
    }
}
