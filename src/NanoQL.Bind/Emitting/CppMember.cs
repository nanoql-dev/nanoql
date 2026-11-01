// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// A function's C++ member: its class, its name, its form (<c>new</c>, <c>instance</c>, <c>static</c>, <c>index</c>),
/// its parameters' names and types after <c>self</c>, and the first parameter that takes its default.
/// </summary>
internal sealed class CppMember(CFunction function, string cls, string name, string style, bool value, List<string> names, List<string> types)
{
    /// <summary>Gets the function.</summary>
    public CFunction Function { get; } = function;

    /// <summary>Gets the class's name.</summary>
    public string Class { get; } = cls;

    /// <summary>Gets or sets the member's name: the class's for a constructor.</summary>
    public string Name { get; set; } = name;

    /// <summary>Gets or sets the form.</summary>
    public string Style { get; set; } = style;

    /// <summary>Gets whether the class is a value (<c>Date</c>, <c>Period</c>).</summary>
    public bool Value { get; } = value;

    /// <summary>Gets the parameters' names.</summary>
    public List<string> Names { get; } = names;

    /// <summary>Gets the parameters' types.</summary>
    public List<string> Types { get; } = types;

    /// <summary>Gets or sets the first parameter that takes its default; the count when none does.</summary>
    public int DefaultsFrom { get; set; }

    /// <summary>Gets or sets whether a constructor became a static <c>create</c>, its signature another constructor's.</summary>
    public bool Create { get; set; }
}
