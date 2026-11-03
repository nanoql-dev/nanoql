// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/futuresconvadjustmentquote.hpp ql/quotes/futuresconvadjustmentquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Time;

namespace NanoQL.Quotes;

/// <summary>
/// The convexity adjustment of an Ibor futures contract under Hull-White: QuantLib's <c>FuturesConvAdjustmentQuote</c>.
/// </summary>
/// <remarks>
/// Immutable: its value is Hull-White's convexity bias (<see cref="HullWhite.ConvexityBias"/>) at the context's
/// evaluation date, computed once at construction (§2.2's quote row). QuantLib's quote recomputes when the evaluation
/// date or an input changes; here a new date or input is a new quote. The futures' start and the index's maturity are
/// year fractions of the index's day counter from the evaluation date. QuantLib's <c>isValid</c> isn't ported: a value
/// is always valid.
/// </remarks>
public sealed class FuturesConvAdjustmentQuote
{
    /// <summary>Initializes a new instance of the <see cref="FuturesConvAdjustmentQuote"/> class.</summary>
    /// <param name="index">The futures' Ibor index.</param>
    /// <param name="futuresDate">The futures' date, the underlying deposit's start.</param>
    /// <param name="futuresQuote">The futures' price.</param>
    /// <param name="volatility">The Hull-White volatility.</param>
    /// <param name="meanReversion">The Hull-White mean-reversion speed.</param>
    /// <param name="context">The evaluation context, whose evaluation date the bias is computed at.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="futuresDate"/> is <c>default(Date)</c>; or the price, a
    /// time, the volatility or the speed is negative, or the index's maturity is before the futures' date.</exception>
    public FuturesConvAdjustmentQuote(
        IborIndex index,
        Date futuresDate,
        double futuresQuote,
        double volatility,
        double meanReversion,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(futuresDate, nameof(futuresDate));
        DayCounter dc = index.DayCounter;
        ImmDate = futuresDate;
        Date indexMaturityDate = index.MaturityDate(futuresDate);
        FuturesValue = futuresQuote;
        Volatility = volatility;
        MeanReversion = meanReversion;

        Date settlementDate = context.EvaluationDate;
        double startTime = dc.YearFraction(settlementDate, futuresDate);
        double indexMaturity = dc.YearFraction(settlementDate, indexMaturityDate);
        Value = HullWhite.ConvexityBias(futuresQuote, startTime, indexMaturity, volatility, meanReversion);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesConvAdjustmentQuote"/> class on an IMM code, its date the
    /// first IMM date of the code on or after the evaluation date.
    /// </summary>
    /// <param name="index">The futures' Ibor index.</param>
    /// <param name="immCode">The futures' IMM code.</param>
    /// <param name="futuresQuote">The futures' price.</param>
    /// <param name="volatility">The Hull-White volatility.</param>
    /// <param name="meanReversion">The Hull-White mean-reversion speed.</param>
    /// <param name="context">The evaluation context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/>, <paramref name="immCode"/> or
    /// <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The code isn't an IMM code; or the price, a time, the volatility or the
    /// speed is negative.</exception>
    public FuturesConvAdjustmentQuote(
        IborIndex index,
        string immCode,
        double futuresQuote,
        double volatility,
        double meanReversion,
        EvaluationContext context)
        : this(index, IMM.Date(immCode, context), futuresQuote, volatility, meanReversion, context)
    {
    }

    /// <summary>Gets the value: the convexity bias.</summary>
    public double Value { get; }

    /// <summary>Gets the futures' price.</summary>
    public double FuturesValue { get; }

    /// <summary>Gets the Hull-White volatility.</summary>
    public double Volatility { get; }

    /// <summary>Gets the Hull-White mean-reversion speed.</summary>
    public double MeanReversion { get; }

    /// <summary>Gets the futures' date.</summary>
    public Date ImmDate { get; }
}
