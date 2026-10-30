// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currency.hpp ql/currency.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL;

/// <summary>
/// A currency specification: its name, ISO 4217 codes, symbols, fractions and rounding convention.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point: each of QuantLib's currencies derives from it, and a user may add one.
/// QuantLib's empty <c>Currency()</c>, a placeholder whose members throw, is a <c>Currency?</c> null.
/// </para>
/// <para>
/// Two currencies are equal when their names and their codes are, compared ordinally, whatever their other data; the
/// hash is the name's. QuantLib's <c>operator==</c> compares names alone (<c>currency.hpp:179-182</c>), so its BGL and
/// BGN, both named "Bulgarian lev", are one currency there: the code tells them apart, a documented divergence
/// (<c>Divergences/CurrencyNameAndCodeTests.cs</c>). The equality is sealed, so every subclass compares the same
/// way.
/// </para>
/// </remarks>
public class Currency : IEquatable<Currency>
{
    /// <summary>Initializes a new instance of the <see cref="Currency"/> class.</summary>
    /// <param name="name">The currency's name, such as "U.S. dollar".</param>
    /// <param name="code">The ISO 4217 three-letter code, such as "USD".</param>
    /// <param name="numericCode">The ISO 4217 numeric code, such as 840.</param>
    /// <param name="symbol">The symbol, such as "$".</param>
    /// <param name="fractionSymbol">The fraction's symbol, such as "¢".</param>
    /// <param name="fractionsPerUnit">The number of fractions in a unit, such as 100.</param>
    /// <param name="rounding">The rounding convention.</param>
    /// <param name="triangulationCurrency">
    /// The currency used for triangulated exchange when required, or <see langword="null"/> for none.
    /// </param>
    /// <param name="minorUnitCodes">The minor units' codes, such as "GBp" and "GBX" for GBP; none when null.</param>
    /// <exception cref="ArgumentNullException">A string or the rounding is null.</exception>
    /// <exception cref="ArgumentException">A minor unit's code is null.</exception>
    public Currency(
        string name,
        string code,
        int numericCode,
        string symbol,
        string fractionSymbol,
        int fractionsPerUnit,
        Rounding rounding,
        Currency? triangulationCurrency = null,
        IEnumerable<string>? minorUnitCodes = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(fractionSymbol);
        ArgumentNullException.ThrowIfNull(rounding);
        ImmutableSortedSet<string>.Builder codes = ImmutableSortedSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (string minorUnitCode in minorUnitCodes ?? [])
        {
            // A std::string can't be null.
            codes.Add(minorUnitCode ?? throw new ArgumentException("null minor unit code", nameof(minorUnitCodes)));
        }

        Name = name;
        Code = code;
        NumericCode = numericCode;
        Symbol = symbol;
        FractionSymbol = fractionSymbol;
        FractionsPerUnit = fractionsPerUnit;
        Rounding = rounding;
        TriangulationCurrency = triangulationCurrency;
        MinorUnitCodes = codes.ToImmutable();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Currency"/> class from a preset's data, which the preset reads
    /// through <see cref="ConventionProvider"/>.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <exception cref="ArgumentNullException">The data, or one of its strings or its rounding, is null.</exception>
    protected Currency(CurrencyData data)
        : this(
            (data ?? throw new ArgumentNullException(nameof(data))).Name,
            data.Code,
            data.NumericCode,
            data.Symbol,
            data.FractionSymbol,
            data.FractionsPerUnit,
            data.Rounding,
            data.TriangulationCurrency)
    {
    }

    /// <summary>Gets the currency's name, such as "U.S. dollar".</summary>
    public string Name { get; }

    /// <summary>Gets the ISO 4217 three-letter code, such as "USD".</summary>
    public string Code { get; }

    /// <summary>Gets the ISO 4217 numeric code, such as 840.</summary>
    public int NumericCode { get; }

    /// <summary>Gets the symbol, such as "$".</summary>
    public string Symbol { get; }

    /// <summary>Gets the fraction's symbol, such as "¢".</summary>
    public string FractionSymbol { get; }

    /// <summary>Gets the number of fractions in a unit, such as 100.</summary>
    public int FractionsPerUnit { get; }

    /// <summary>Gets the rounding convention.</summary>
    public Rounding Rounding { get; }

    /// <summary>Gets the currency used for triangulated exchange when required, or <see langword="null"/> for none.</summary>
    public Currency? TriangulationCurrency { get; }

    /// <summary>Gets the minor units' codes, such as "GBp" and "GBX" for GBP, in ordinal order.</summary>
    public ImmutableSortedSet<string> MinorUnitCodes { get; }

    /// <summary>Compares two currencies by name and code; two nulls are equal.</summary>
    /// <param name="left">The first currency.</param>
    /// <param name="right">The second currency.</param>
    /// <returns><see langword="true"/> when both are null or both have the same name and code.</returns>
    public static bool operator ==(Currency? left, Currency? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Compares two currencies by name and code.</summary>
    /// <param name="left">The first currency.</param>
    /// <param name="right">The second currency.</param>
    /// <returns><see langword="true"/> unless both are null or both have the same name and code.</returns>
    public static bool operator !=(Currency? left, Currency? right) => !(left == right);

    /// <summary>Compares this currency with another by name and code, ordinally.</summary>
    /// <param name="other">The other currency.</param>
    /// <returns><see langword="true"/> when the other currency isn't null and has this currency's name and
    /// code.</returns>
    public bool Equals(Currency? other) =>
        other is not null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Code, other.Code, StringComparison.Ordinal);

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is Currency other && Equals(other);

    /// <summary>The hash of the currency's name.</summary>
    /// <returns>The hash.</returns>
    public sealed override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>The currency's code, as QuantLib's <c>operator&lt;&lt;</c> writes it.</summary>
    /// <returns>The code.</returns>
    public override string ToString() => Code;

    /// <summary>
    /// A currency as QuantLib's <c>operator&lt;&lt;</c> writes it into a message: its code, or "null currency" for
    /// QuantLib's empty currency, a null.
    /// </summary>
    /// <param name="currency">The currency, or <see langword="null"/>.</param>
    /// <returns>The text.</returns>
    internal static string Format(Currency? currency) => currency is null ? "null currency" : currency.Code;
}
