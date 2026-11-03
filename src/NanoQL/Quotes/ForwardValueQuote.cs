// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/forwardvaluequote.hpp ql/quotes/forwardvaluequote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Quotes;

/// <summary>The forward value of an index: its fixing at a date, QuantLib's <c>ForwardValueQuote</c>.</summary>
/// <remarks>
/// Immutable: its value is the index's fixing under the context, computed once at construction (§2.2's quote row).
/// QuantLib's quote reads the index whenever it is asked; here a new curve or evaluation date is a new quote.
/// QuantLib's <c>isValid</c> isn't ported: a fixing the index can't give is refused at construction.
/// </remarks>
public sealed class ForwardValueQuote
{
    /// <summary>Initializes a new instance of the <see cref="ForwardValueQuote"/> class.</summary>
    /// <param name="index">The index.</param>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is <c>default(Date)</c> or isn't a valid
    /// fixing date.</exception>
    /// <exception cref="NanoQLException">The index can't give the fixing.</exception>
    public ForwardValueQuote(FinancialIndex index, Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        Value = index.Fixing(fixingDate, context);
    }

    /// <summary>Gets the value: the index's fixing at the date.</summary>
    public double Value { get; }
}
