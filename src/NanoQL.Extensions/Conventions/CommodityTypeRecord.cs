// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/CommoditySetTests.cs (a kind with no QuantLib presets, its builds checked by hand)

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A commodity type (<c>CommodityType</c>), which <see cref="NanoQL.Experimental.CommodityType"/> takes: its name and
/// code.
/// </summary>
[Experimental("NQL0001")]
public sealed record CommodityTypeRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CommodityTypeRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CommodityTypeRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the commodity type's name, or <see langword="null"/> to take the base's: the record's by default.
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>Gets its code, its identity, or <see langword="null"/> to take the base's.</summary>
    public string? Code { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CommodityTypeRecord Over(CommodityTypeRecord lower) =>
        this with
        {
            TypeName = TypeName ?? lower.TypeName,
            Code = Code ?? lower.Code,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(TypeName), TypeName is not null),
            (nameof(Code), Code is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CommodityTypeRecord Named(string name) =>
        new(name)
        {
            TypeName = TypeName,
            Code = Code,
        };
}
