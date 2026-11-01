// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Bind.Reading;

/// <summary>Tests on a metadata context's types and members, by name.</summary>
internal static class TypeTests
{
    /// <summary>Whether a type has a full name.</summary>
    /// <param name="t">The type.</param>
    /// <param name="fullName">The name.</param>
    /// <returns>True when it does.</returns>
    public static bool Is(this Type t, string fullName) => t.FullName == fullName;

    /// <summary>Whether a type is a closed construction of a generic definition.</summary>
    /// <param name="t">The type.</param>
    /// <param name="definition">The definition's full name, with its arity (<c>System.Nullable`1</c>).</param>
    /// <returns>True when it is.</returns>
    public static bool IsConstructionOf(this Type t, string definition) =>
        t.IsGenericType && !t.IsGenericTypeDefinition && t.GetGenericTypeDefinition().FullName == definition;

    /// <summary>A nullable value type's underlying type, or null.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The underlying type.</returns>
    public static Type? NullableOf(this Type t) => t.IsConstructionOf("System.Nullable`1") ? t.GetGenericArguments()[0] : null;

    /// <summary>Whether a member carries an attribute, by the attribute's full name.</summary>
    /// <param name="m">The member.</param>
    /// <param name="attribute">The attribute's full name.</param>
    /// <returns>True when it does.</returns>
    public static bool Has(this MemberInfo m, string attribute) =>
        m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == attribute);

    /// <summary>An attribute's data on a member, by the attribute's full name.</summary>
    /// <param name="m">The member.</param>
    /// <param name="attribute">The attribute's full name.</param>
    /// <returns>Its data, or null.</returns>
    public static CustomAttributeData? Attribute(this MemberInfo m, string attribute) =>
        m.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == attribute);

    /// <summary>Whether a type is a delegate.</summary>
    /// <param name="t">The type.</param>
    /// <returns>True for a delegate type.</returns>
    public static bool IsDelegate(this Type t)
    {
        for (Type? b = t.BaseType; b != null; b = b.BaseType)
        {
            if (b.FullName == "System.MulticastDelegate")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a type is a trait interface: an interface with static abstract or static virtual members, its own or
    /// inherited, which a C ABI can't call.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <returns>True for a trait interface.</returns>
    public static bool IsTrait(this Type t) =>
        t.IsInterface && t.GetInterfaces().Append(t).Any(i => i.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.IsAbstract || m.IsVirtual));

    /// <summary>
    /// A type's name as metadata keys write it: generic arguments expanded, a type parameter as metadata numbers it
    /// (<c>!0</c> for a type's, <c>!!0</c> for a method's), as a call site's signature reads.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <returns>The name.</returns>
    public static string KeyName(this Type t) =>
        t.IsGenericParameter ? (t.DeclaringMethod != null ? "!!" : "!") + t.GenericParameterPosition
        : t.IsGenericType && !t.IsGenericTypeDefinition
            ? t.GetGenericTypeDefinition().FullName + "<" + string.Join(",", t.GetGenericArguments().Select(KeyName)) + ">"
            : t.IsByRef ? KeyName(t.GetElementType()!) + "&"
            : t.IsArray ? KeyName(t.GetElementType()!) + "[]"
            : t.FullName ?? t.Name;

    /// <summary>
    /// A member's identity as a caller's metadata references it: declaring type, name and parameter types. The recorder
    /// and the coverage report key members by it.
    /// </summary>
    /// <param name="m">The member.</param>
    /// <returns>The key.</returns>
    public static string MemberKey(this MemberInfo m) => m switch
    {
        MethodBase mb => mb.DeclaringType!.KeyName() + "::" + mb.Name + "(" + string.Join(",", mb.GetParameters().Select(p => p.ParameterType.KeyName())) + ")",
        PropertyInfo pi => pi.DeclaringType!.KeyName() + "::" + pi.Name,
        FieldInfo fi => fi.DeclaringType!.KeyName() + "::" + fi.Name,
        Type t => t.KeyName(),
        _ => m.Name,
    };
}
