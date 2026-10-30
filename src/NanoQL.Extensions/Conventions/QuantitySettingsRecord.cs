// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NanoQL.Experimental;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// The quantity settings (<c>QuantitySettings</c>), which <see cref="NanoQL.Experimental.QuantitySettings"/> takes;
/// named <c>QuantitySettings</c>, QuantLib's defaults.
/// </summary>
[Experimental("NQL0001")]
public sealed record QuantitySettingsRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="QuantitySettingsRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public QuantitySettingsRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets how quantities in different units combine, or <see langword="null"/> to take the base's: no conversion by
    /// default.
    /// </summary>
    public QuantityConversionType? ConversionType { get; init; }

    /// <summary>
    /// Gets the unit a base-unit conversion converts to, or <see langword="null"/> to take the base's: none by default.
    /// </summary>
    public string? BaseUnitOfMeasure { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal QuantitySettingsRecord Over(QuantitySettingsRecord lower) =>
        this with
        {
            ConversionType = ConversionType ?? lower.ConversionType,
            BaseUnitOfMeasure = BaseUnitOfMeasure ?? lower.BaseUnitOfMeasure,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        BaseUnitOfMeasure,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(ConversionType), ConversionType is not null),
            (nameof(BaseUnitOfMeasure), BaseUnitOfMeasure is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal QuantitySettingsRecord Named(string name) =>
        new(name)
        {
            ConversionType = ConversionType,
            BaseUnitOfMeasure = BaseUnitOfMeasure,
        };
}
