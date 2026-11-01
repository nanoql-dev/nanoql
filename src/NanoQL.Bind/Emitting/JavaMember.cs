// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// A function's Java member: the class it is written in (the type's own, or its <c>&lt;Class&gt;Experimental</c> for a
/// stable class's experimental member), its name and form (<c>new</c>, <c>instance</c>, <c>static</c>, <c>self</c> for a
/// static taking the instance first), its parameters' names and Java types after <c>self</c>, and the first parameter a
/// telescoped overload leaves at its default.
/// </summary>
/// <param name="function">The function.</param>
/// <param name="type">The function's type.</param>
/// <param name="holder">The fully qualified class the member is written in.</param>
/// <param name="name">The member's name.</param>
/// <param name="style">The form.</param>
/// <param name="names">The parameters' names.</param>
/// <param name="types">The parameters' Java types.</param>
internal sealed class JavaMember(CFunction function, TypeRow type, string holder, string name, string style, List<string> names, List<string> types)
{
    /// <summary>Gets the function.</summary>
    public CFunction Function { get; } = function;

    /// <summary>Gets the function's type.</summary>
    public TypeRow Type { get; } = type;

    /// <summary>Gets or sets the fully qualified class the member is written in.</summary>
    public string Holder { get; set; } = holder;

    /// <summary>Gets or sets the member's name: the class's for a constructor.</summary>
    public string Name { get; set; } = name;

    /// <summary>Gets or sets the form.</summary>
    public string Style { get; set; } = style;

    /// <summary>Gets the parameters' names.</summary>
    public List<string> Names { get; } = names;

    /// <summary>Gets the parameters' Java types.</summary>
    public List<string> Types { get; } = types;

    /// <summary>Gets or sets the first parameter a telescoped overload leaves at its default; the count when none does.</summary>
    public int DefaultsFrom { get; set; }
}
