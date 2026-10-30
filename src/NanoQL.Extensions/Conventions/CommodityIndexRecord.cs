// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/CommoditySetTests.cs (a kind with no QuantLib presets, its builds checked by hand)

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A commodity index's conventions (<c>CommodityIndex</c>), which <see cref="NanoQL.Experimental.CommodityIndex"/>
/// takes; its forward curve and exchange contracts are market and contract data, which no record holds.
/// </summary>
[Experimental("NQL0001")]
public sealed record CommodityIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CommodityIndexRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CommodityIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the index's name, or <see langword="null"/> to take the base's: the record's by default.</summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// Gets the commodity type's name, or <see langword="null"/> to take the base's: QuantLib's null commodity type by
    /// default.
    /// </summary>
    public string? CommodityType { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the unit of measure's name, or <see langword="null"/> to take the base's.</summary>
    public string? UnitOfMeasure { get; init; }

    /// <summary>Gets the fixing calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the quantity of a lot, or <see langword="null"/> to take the base's.</summary>
    public double? LotQuantity { get; init; }

    /// <summary>
    /// Gets the nearby offset into the exchange contracts, or <see langword="null"/> to take the base's.
    /// </summary>
    public int? NearbyOffset { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CommodityIndexRecord Over(CommodityIndexRecord lower) =>
        this with
        {
            IndexName = IndexName ?? lower.IndexName,
            CommodityType = CommodityType ?? lower.CommodityType,
            Currency = Currency ?? lower.Currency,
            UnitOfMeasure = UnitOfMeasure ?? lower.UnitOfMeasure,
            Calendar = Calendar ?? lower.Calendar,
            LotQuantity = LotQuantity ?? lower.LotQuantity,
            NearbyOffset = NearbyOffset ?? lower.NearbyOffset,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        CommodityType,
        Currency,
        UnitOfMeasure,
        Calendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(IndexName), IndexName is not null),
            (nameof(CommodityType), CommodityType is not null),
            (nameof(Currency), Currency is not null),
            (nameof(UnitOfMeasure), UnitOfMeasure is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(LotQuantity), LotQuantity is not null),
            (nameof(NearbyOffset), NearbyOffset is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CommodityIndexRecord Named(string name) =>
        new(name)
        {
            IndexName = IndexName,
            CommodityType = CommodityType,
            Currency = Currency,
            UnitOfMeasure = UnitOfMeasure,
            Calendar = Calendar,
            LotQuantity = LotQuantity,
            NearbyOffset = NearbyOffset,
        };
}
