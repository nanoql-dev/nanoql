// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/lastfixingquote.hpp ql/quotes/lastfixingquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Quotes;

/// <summary>The last fixing an index has available: QuantLib's <c>LastFixingQuote</c>.</summary>
/// <remarks>
/// <para>
/// Immutable: its reference date and value are computed once, at construction, from the context's fixings and
/// evaluation date (§2.2's quote row; the S-core plan's CR1 (b)). QuantLib's <c>isValid</c> isn't ported: an index with
/// no stored fixing is refused at construction, with QuantLib's text.
/// </para>
/// <para>
/// The reference date is the last stored fixing's date on or before the evaluation date, where QuantLib's is the earlier
/// of the last stored date and the evaluation date (<c>lastfixingquote.cpp:40-43</c>): on a store holding fixings
/// after the evaluation date, as a backtest on one full history does, QuantLib's is the evaluation date itself, whose
/// fixing is refused on a non-fixing day or forecast where it is missing. Where the store holds no fixing on or before
/// the evaluation date, the reference date is QuantLib's, the evaluation date. A documented divergence (the plan's CR7
/// (xxxv)); on a store that ends on or before the evaluation date the two agree.
/// </para>
/// </remarks>
public sealed class LastFixingQuote
{
    /// <summary>Initializes a new instance of the <see cref="LastFixingQuote"/> class.</summary>
    /// <param name="index">The index.</param>
    /// <param name="context">The evaluation context, whose fixings and evaluation date the quote reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date isn't a valid fixing date.</exception>
    /// <exception cref="NanoQLException">The index has no stored fixing ("<c>name</c> has no fixing"), or it can't give
    /// the fixing at the reference date.</exception>
    public LastFixingQuote(FinancialIndex index, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(context);
        TimeSeries<double> series = index.TimeSeries(context);
        if (series.Empty)
        {
            throw new NanoQLException($"{index.Name} has no fixing");
        }

        Index = index;
        ReferenceDate = LastDateOnOrBefore(series.Dates(), context.EvaluationDate);
        Value = index.Fixing(ReferenceDate, context);
    }

    /// <summary>Gets the index.</summary>
    public FinancialIndex Index { get; }

    /// <summary>
    /// Gets the reference date: the last stored fixing's date on or before the evaluation date, or the evaluation date
    /// where there is none.
    /// </summary>
    public Date ReferenceDate { get; }

    /// <summary>Gets the value: the index's fixing at the reference date.</summary>
    public double Value { get; }

    // The last of the sorted dates on or before the evaluation date; the evaluation date where none is, which is
    // QuantLib's min(lastDate, evaluationDate) there.
    private static Date LastDateOnOrBefore(ImmutableArray<Date> dates, Date evaluationDate)
    {
        int lo = 0, hi = dates.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (dates[mid] <= evaluationDate)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo > 0 ? dates[lo - 1] : evaluationDate;
    }
}
