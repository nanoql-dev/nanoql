// SPDX-FileCopyrightText: Copyright (C) 2010 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/deltavolquote.hpp ql/quotes/deltavolquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Quotes;

/// <summary>
/// A volatility quoted against an FX delta, or at the money: the volatility, its delta and delta convention, its
/// maturity, and its at-the-money convention.
/// </summary>
/// <remarks>
/// Immutable; its volatility is a value where QuantLib's is a quote handle, so it has no validity of its own
/// (QuantLib's <c>isValid</c> reports the handle's). An at-the-money quote has no delta, which QuantLib's constructor
/// leaves uninitialized: its <see cref="Delta"/> is <see langword="null"/>.
/// </remarks>
public sealed class DeltaVolQuote
{
    /// <summary>Initializes a new instance of the <see cref="DeltaVolQuote"/> class: a volatility at a delta.</summary>
    /// <param name="delta">The delta.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="maturity">The maturity, a time.</param>
    /// <param name="deltaType">The delta convention.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaType"/> isn't a member of its
    /// type.</exception>
    public DeltaVolQuote(double delta, double vol, double maturity, DeltaVolQuoteDeltaType deltaType)
    {
        EnumArguments.Check(deltaType, nameof(deltaType));
        Delta = delta;
        Value = vol;
        Maturity = maturity;
        DeltaType = deltaType;
        AtmType = DeltaVolQuoteAtmType.AtmNull;
    }

    /// <summary>Initializes a new instance of the <see cref="DeltaVolQuote"/> class: an at-the-money
    /// volatility.</summary>
    /// <param name="vol">The volatility.</param>
    /// <param name="deltaType">The delta convention.</param>
    /// <param name="maturity">The maturity, a time.</param>
    /// <param name="atmType">The at-the-money convention.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaType"/> or <paramref name="atmType"/> isn't
    /// a member of its type.</exception>
    public DeltaVolQuote(double vol, DeltaVolQuoteDeltaType deltaType, double maturity, DeltaVolQuoteAtmType atmType)
    {
        EnumArguments.Check(deltaType, nameof(deltaType));
        EnumArguments.Check(atmType, nameof(atmType));
        Value = vol;
        DeltaType = deltaType;
        Maturity = maturity;
        AtmType = atmType;
    }

    /// <summary>Gets the volatility.</summary>
    public double Value { get; }

    /// <summary>Gets the delta; <see langword="null"/> for an at-the-money quote.</summary>
    public double? Delta { get; }

    /// <summary>Gets the maturity, a time.</summary>
    public double Maturity { get; }

    /// <summary>Gets the at-the-money convention; <see cref="DeltaVolQuoteAtmType.AtmNull"/> for a delta
    /// quote.</summary>
    public DeltaVolQuoteAtmType AtmType { get; }

    /// <summary>Gets the delta convention.</summary>
    public DeltaVolQuoteDeltaType DeltaType { get; }
}
