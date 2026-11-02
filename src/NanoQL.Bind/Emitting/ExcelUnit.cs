// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// One worksheet function of a build: a C function, or a family of a generic type's constructions folded into one, its
/// type arguments taken by name (XLD29).
/// </summary>
/// <param name="Name">The Excel name.</param>
/// <param name="Function">The function, or the family's representative, whose kinds every construction shares.</param>
/// <param name="Family">The family's constructions this build holds, by C name; null for a plain function.</param>
/// <param name="Refused">The family's experimental constructions the stable build refuses, naming the experimental build.</param>
/// <param name="Experimental">Whether it is experimental: a family only when every construction is.</param>
internal sealed record ExcelUnit(string Name, CFunction Function, IReadOnlyList<CFunction>? Family, IReadOnlyList<CFunction> Refused, bool Experimental)
{
    /// <summary>Gets the C functions it holds.</summary>
    public IEnumerable<CFunction> Functions => Family ?? [Function];
}
