// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/index.hpp ql/index.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// The base class of indexes: QuantLib's <c>Index</c>, renamed because <c>Index</c> clashes with
/// <see cref="System.Index"/>.
/// </summary>
/// <remarks>
/// <para>
/// An index holds its conventions, and for an Ibor or overnight index its forecasting curve, never fixings: its stored
/// fixings are read from <see cref="EvaluationContext.Fixings"/>, which <see cref="Indexes.IndexFixings"/> writes.
/// QuantLib's <c>addFixing</c>, <c>addFixings</c> and <c>clearFixings</c> are members of the store, which take the
/// index.
/// </para>
/// <para>
/// No check is made that a requested fixing is for a date on or before the evaluation date: as in QuantLib, it is up to
/// the caller not to see into the future. Two indexes are never compared as values: QuantLib compares their
/// <see cref="Name"/>, and so does the port.
/// </para>
/// </remarks>
public abstract class FinancialIndex
{
    /// <summary>
    /// Gets the index's name, which keys its stored fixings. It is meant for output and comparison, not for code that
    /// switches on the index's type.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>Gets the calendar defining valid fixing dates.</summary>
    public abstract Calendar FixingCalendar { get; }

    /// <summary>
    /// Gets a value indicating whether fixings may be stored for this index; when <see langword="false"/>, the store
    /// refuses them (an index whose fixings are those of the indexes it is built from).
    /// </summary>
    public virtual bool AllowsNativeFixings => true;

    /// <summary>Whether a date is a valid fixing date.</summary>
    /// <param name="fixingDate">The date.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public abstract bool IsValidFixingDate(Date fixingDate);

    /// <summary>Whether a fixing is stored for a date.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns><see langword="true"/> when the context holds a fixing for the index on that date.</returns>
    public bool HasHistoricalFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        return context.Fixings.HasHistoricalFixing(Name, fixingDate);
    }

    /// <summary>The fixing on a date: stored, or forecast.</summary>
    /// <param name="fixingDate">The actual calendar date of the fixing, with no settlement days applied.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="forecastTodaysFixing">Whether a fixing on the evaluation date is forecast even when one is stored.</param>
    /// <returns>The fixing.</returns>
    public abstract double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false);

    /// <summary>The stored fixing on a date.</summary>
    /// <param name="fixingDate">The actual calendar date of the fixing, with no settlement days applied.</param>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The fixing, or <see langword="null"/> when none is stored, where QuantLib returns
    /// <c>Null&lt;Real&gt;()</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    public virtual double? PastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (!IsValidFixingDate(fixingDate))
        {
            throw new ArgumentException($"{fixingDate} is not a valid fixing date", nameof(fixingDate));
        }

        return context.Fixings.GetHistory(Name).TryGetValue(fixingDate, out double fixing) ? fixing : null;
    }

    /// <summary>The index's stored fixings.</summary>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The fixings, empty when none are stored; reading them never changes the store.</returns>
    public TimeSeries<double> TimeSeries(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Fixings.GetHistory(Name);
    }
}
