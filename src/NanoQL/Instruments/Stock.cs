// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/stock.hpp ql/instruments/stock.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A stock: an instrument worth its quote.</summary>
/// <remarks>Immutable; it never expires. It has no engine: <see cref="Calculate"/> computes its value.</remarks>
public sealed class Stock : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="Stock"/> class.</summary>
    /// <param name="quote">The stock's quoted value.</param>
    public Stock(double quote)
    {
        Quote = quote;
    }

    /// <summary>Gets the stock's quoted value.</summary>
    public double Quote { get; }

    /// <summary>Whether the instrument has expired: a stock never does.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return false;
    }

    /// <summary>Prices the stock: QuantLib's <c>performCalculations</c>, its NPV the quote.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public InstrumentResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return FetchResults(new InstrumentResults { NPV = Quote });
    }
}
