// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A conversion factor between two units of measure (<c>UnitOfMeasureConversion</c>), which a set's
/// <see cref="NanoQL.Experimental.UnitOfMeasureConversions"/> adds to QuantLib's known factors.
/// </summary>
[Experimental("NQL0001")]
public sealed record UnitOfMeasureConversionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="UnitOfMeasureConversionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public UnitOfMeasureConversionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the commodity type's name, or <see langword="null"/> to take the base's: QuantLib's null commodity type,
    /// which its known factors take, by default.
    /// </summary>
    public string? CommodityType { get; init; }

    /// <summary>Gets the source unit's name, or <see langword="null"/> to take the base's.</summary>
    public string? Source { get; init; }

    /// <summary>Gets the target unit's name, or <see langword="null"/> to take the base's.</summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets the value of one source unit in the target unit, or <see langword="null"/> to take the base's.
    /// </summary>
    public double? Factor { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal UnitOfMeasureConversionRecord Over(UnitOfMeasureConversionRecord lower) =>
        this with
        {
            CommodityType = CommodityType ?? lower.CommodityType,
            Source = Source ?? lower.Source,
            Target = Target ?? lower.Target,
            Factor = Factor ?? lower.Factor,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        CommodityType,
        Source,
        Target,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(CommodityType), CommodityType is not null),
            (nameof(Source), Source is not null),
            (nameof(Target), Target is not null),
            (nameof(Factor), Factor is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal UnitOfMeasureConversionRecord Named(string name) =>
        new(name)
        {
            CommodityType = CommodityType,
            Source = Source,
            Target = Target,
            Factor = Factor,
        };
}
