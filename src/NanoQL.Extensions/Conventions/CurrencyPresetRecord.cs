// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A currency preset (<c>CurrencyPreset</c>): one of QuantLib's currencies by its ISO 4217 code, with the data its
/// constructor reads through the set.
/// </summary>
/// <remarks>
/// A record of a preset's code must keep QuantLib's name and numeric code, which with the code are the currency's
/// identity, read by its equality and the exchange-rate registries; its other fields configure the currency. There are
/// no currencies of a user's own.
/// </remarks>
public sealed record CurrencyPresetRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CurrencyPresetRecord"/> class.</summary>
    /// <param name="name">The currency's ISO 4217 code.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CurrencyPresetRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets QuantLib's name of the currency, or <see langword="null"/> to take the base's.</summary>
    public string? QuantLibName { get; init; }

    /// <summary>Gets the ISO 4217 numeric code, QuantLib's, or <see langword="null"/> to take the base's.</summary>
    public int? NumericCode { get; init; }

    /// <summary>Gets the symbol, or <see langword="null"/> to take the base's.</summary>
    public string? Symbol { get; init; }

    /// <summary>Gets the fraction's symbol, or <see langword="null"/> to take the base's.</summary>
    public string? FractionSymbol { get; init; }

    /// <summary>Gets the fractions in a unit, or <see langword="null"/> to take the base's.</summary>
    public int? FractionsPerUnit { get; init; }

    /// <summary>Gets the rounding convention, or <see langword="null"/> to take the base's.</summary>
    public CurrencyRounding? Rounding { get; init; }

    /// <summary>
    /// Gets the code of the currency used for triangulated exchange: an empty string for none, or
    /// <see langword="null"/> to take the base's.
    /// </summary>
    public string? TriangulationCurrency { get; init; }
}
