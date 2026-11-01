// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>
/// A type of the surface, for the language layers: its C name, its public base and interfaces, its kind, and how it
/// crosses (<c>handle</c>; for the working types <c>matrix</c>, <c>vector</c>, <c>snapshot</c>, <c>distribution</c> or
/// <c>withheld</c>; <c>value</c> for <c>Date</c> and <c>Period</c>; <c>boxed</c> for other structs; <c>static</c>).
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="Prefix">Its assembly's prefix.</param>
/// <param name="CName">Its C name.</param>
/// <param name="Base">Its nearest public base of the inputs, or null.</param>
/// <param name="Interfaces">The inputs' non-generic interfaces it implements, by full name.</param>
/// <param name="Shape">Its kind: <c>class</c>, <c>sealed</c>, <c>abstract</c>, <c>static</c>, <c>struct</c>, <c>interface</c>.</param>
/// <param name="Experimental">Whether it is experimental.</param>
/// <param name="Form">How it crosses.</param>
internal sealed record TypeRow(Type Type, string Prefix, string CName, string? Base, IReadOnlyList<string> Interfaces, string Shape, bool Experimental, string Form);
