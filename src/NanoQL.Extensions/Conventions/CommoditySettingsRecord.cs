// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// The commodity settings (<c>CommoditySettings</c>), which <see cref="NanoQL.Experimental.CommoditySettings"/> takes;
/// named <c>CommoditySettings</c>, QuantLib's defaults.
/// </summary>
[Experimental("NQL0001")]
public sealed record CommoditySettingsRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CommoditySettingsRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CommoditySettingsRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the settings' currency's code, or <see langword="null"/> to take the base's: QuantLib's USD by default.
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Gets the settings' unit of measure's name, or <see langword="null"/> to take the base's: QuantLib's barrels by
    /// default.
    /// </summary>
    public string? UnitOfMeasure { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CommoditySettingsRecord Over(CommoditySettingsRecord lower) =>
        this with
        {
            Currency = Currency ?? lower.Currency,
            UnitOfMeasure = UnitOfMeasure ?? lower.UnitOfMeasure,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Currency,
        UnitOfMeasure,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Currency), Currency is not null),
            (nameof(UnitOfMeasure), UnitOfMeasure is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CommoditySettingsRecord Named(string name) =>
        new(name)
        {
            Currency = Currency,
            UnitOfMeasure = UnitOfMeasure,
        };
}
