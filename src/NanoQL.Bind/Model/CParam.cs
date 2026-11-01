// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>A function's parameter: its C# name, its kind, and its default when it is optional.</summary>
/// <param name="Name">The C# name (<c>self</c> for the instance).</param>
/// <param name="Kind">How it crosses.</param>
/// <param name="Optional">Whether C# lets a caller leave it out.</param>
/// <param name="Default">Its default as metadata stores it (an enum's as its underlying value), when optional.</param>
internal sealed record CParam(string Name, CKind Kind, bool Optional, object? Default)
{
    /// <summary>Gets whether the parameter is the instance, named <c>self</c>; a C# parameter of that name isn't.</summary>
    public bool IsSelf { get; init; }
}
