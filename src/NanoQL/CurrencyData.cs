// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>
/// A currency preset's data, which <see cref="ConventionProvider.ResolveCurrency"/> returns: QuantLib's
/// <c>Currency::Data</c> without its minor unit codes, which no preset sets.
/// </summary>
/// <remarks>
/// The ISO code, the numeric code and the name are the currency's identity, which its equality and the exchange-rate
/// registries read, so a preset refuses data that changes them; the other fields are configuration.
/// </remarks>
/// <param name="Name">The currency's name.</param>
/// <param name="Code">Its ISO 4217 three-letter code, the preset's identity.</param>
/// <param name="NumericCode">Its ISO 4217 numeric code.</param>
/// <param name="Symbol">Its symbol.</param>
/// <param name="FractionSymbol">Its fraction symbol.</param>
/// <param name="FractionsPerUnit">The number of fractionary parts in a unit.</param>
/// <param name="Rounding">The rounding convention.</param>
/// <param name="TriangulationCurrency">The currency used for triangulated exchange, or <see langword="null"/>.</param>
public sealed record CurrencyData(
    string Name,
    string Code,
    int NumericCode,
    string Symbol,
    string FractionSymbol,
    int FractionsPerUnit,
    Rounding Rounding,
    Currency? TriangulationCurrency = null);
