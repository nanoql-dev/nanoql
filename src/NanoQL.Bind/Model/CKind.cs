// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>
/// How a C# parameter or result crosses the C ABI (docs/bindings.md, "Kinds"). Scalars: <c>double</c>, <c>int32</c>,
/// <c>int64</c>, <c>bool</c>, <c>string</c>, <c>date</c> (the serial, 0 none, for <c>Date</c> and <c>Date?</c>),
/// <c>period</c>, <c>enum</c>, <c>complex</c>; nullable values with a <c>?</c>; objects: <c>handle</c>, <c>boxed</c> (a
/// nanoql struct behind a handle), <c>snapshot</c> (a generator), <c>engine</c> (a bound engine); the working types'
/// forms: <c>matrix</c>, <c>vector</c>, <c>distribution</c>; collections: <c>array</c>, <c>span</c> (written in place),
/// <c>jagged</c>, <c>map</c> (keys and values), <c>tuple</c> (a parameter or a result per element), an <c>array</c> of
/// tuples (an array per element), <c>tuple?</c> for a returned collection's nullable tuple.
/// </summary>
/// <param name="Name">The kind's name.</param>
/// <param name="Type">The C# type it crosses.</param>
internal sealed record CKind(string Name, Type Type)
{
    /// <summary>Gets a collection's element kind, or a map's key kind.</summary>
    public CKind? Element { get; init; }

    /// <summary>Gets a map's value kind.</summary>
    public CKind? Value { get; init; }

    /// <summary>Gets a tuple's element kinds.</summary>
    public IReadOnlyList<CKind> Items { get; init; } = [];

    /// <summary>Gets a tuple's element names (its declared names, or <c>item1</c>...).</summary>
    public IReadOnlyList<string> ItemNames { get; init; } = [];

    /// <summary>Gets an <c>engine</c> parameter's delegate shape.</summary>
    public EngineParameter? Engine { get; init; }

    /// <summary>Gets the kind as <c>surface.json</c> writes it: <c>array:double</c>, <c>map:date:double</c>, <c>tuple:double,double</c>.</summary>
    public string Text => Name switch
    {
        "array" or "span" or "jagged" => Name + ":" + Element!.Text,
        "map" => "map:" + Element!.Text + ":" + Value!.Text,
        "tuple" => "tuple:" + string.Join(",", Items.Select(i => i.Text)),
        "tuple?" => "tuple?:" + string.Join(",", Items.Select(i => i.Text)),
        _ => Name,
    };

    /// <summary>Gets whether the kind is a nullable value (<c>double?</c> and the like).</summary>
    public bool IsNullableValue => Name.EndsWith('?') && Name != "boxed?";
}
