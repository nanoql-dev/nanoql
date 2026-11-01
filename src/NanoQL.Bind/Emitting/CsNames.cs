// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>C# source names for the generated shims: types fully qualified with <c>global::</c>, and literals.</summary>
internal static class CsNames
{
    /// <summary>A type's C# name.</summary>
    /// <param name="t">The type.</param>
    /// <returns>Its name.</returns>
    public static string Of(Type t)
    {
        switch (t.FullName)
        {
            case "System.Double": return "double";
            case "System.Int32": return "int";
            case "System.Int64": return "long";
            case "System.Boolean": return "bool";
            case "System.String": return "string";
            case "System.Object": return "object";
            case "System.Void": return "void";
            case "System.Byte": return "byte";
        }

        if (t.IsByRef)
        {
            return Of(t.GetElementType()!);
        }

        if (t.IsArray)
        {
            return Of(t.GetElementType()!) + "[]";
        }

        if (t.IsConstructionOf("System.Nullable`1"))
        {
            return Of(t.GetGenericArguments()[0]) + "?";
        }

        string name = t.IsNested ? Of(t.DeclaringType!) + "." + t.Name : "global::" + (t.Namespace != null ? t.Namespace + "." : string.Empty) + t.Name;
        if (t.IsGenericType)
        {
            name = name[..name.LastIndexOf('`')] + "<" + string.Join(", ", t.GetGenericArguments().Select(Of)) + ">";
        }

        return name;
    }

    /// <summary>A parameter's default as a C# literal of its type.</summary>
    /// <param name="t">The parameter's type.</param>
    /// <param name="value">The default as metadata stores it.</param>
    /// <returns>The literal.</returns>
    public static string Literal(Type t, object? value)
    {
        Type u = t.IsConstructionOf("System.Nullable`1") ? t.GetGenericArguments()[0] : t;
        if (value is null or DBNull)
        {
            return u.IsValueType && u == t ? "default(" + Of(t) + ")" : "null";
        }

        if (u.IsEnum)
        {
            return "(" + Of(u) + ")(" + Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + ")";
        }

        return value switch
        {
            bool b => b ? "true" : "false",
            double d => Double(d),
            float f => Double(f),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            string s => System.Text.Json.JsonSerializer.Serialize(s),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };
    }

    /// <summary>A double as an exact C# literal.</summary>
    /// <param name="d">The value.</param>
    /// <returns>The literal.</returns>
    public static string Double(double d) =>
        double.IsNaN(d) ? "double.NaN"
        : double.IsPositiveInfinity(d) ? "double.PositiveInfinity"
        : double.IsNegativeInfinity(d) ? "double.NegativeInfinity"
        : d.ToString("R", CultureInfo.InvariantCulture) + "d";
}
