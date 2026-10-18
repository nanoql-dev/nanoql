// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/bootstraphelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base of the helpers a term structure is bootstrapped on: an instrument's quote, the dates it determines, and the
/// quote the curve implies.
/// </summary>
/// <typeparam name="TTermStructure">The kind of term structure the helper prices against.</typeparam>
/// <remarks>
/// <para>
/// A helper is immutable. QuantLib's <c>setTermStructure</c>, which links the curve under construction into the
/// helper, becomes <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/>: it returns a
/// copy bound to the term structure, which has rebuilt on itself whatever prices against it (an index cloned on the
/// curve, a swap's coupons copied with that index). The helper it was called on stays unbound, and an unbound helper's
/// <see cref="ImpliedQuote"/> throws "term structure not set". The bootstraps bind copies of a curve's helpers to the
/// curve under construction, and discard them when the curve is built.
/// </para>
/// <para>
/// A helper whose dates follow the evaluation date (QuantLib's <c>RelativeDateBootstrapHelper</c>) computes them in its
/// constructor from the context's evaluation date, which it records. QuantLib's helper re-runs
/// <c>initializeDates()</c> whenever the global evaluation date moves; here, a curve built for another evaluation date
/// binds a copy of the helper re-dated for it, through <see cref="InitializeDates"/>, so a curve's helpers always have
/// its evaluation date's dates. A helper built with explicit dates (<c>updateDates</c> false) is used as built.
/// </para>
/// <para>
/// The five dates are QuantLib's, with its fallbacks: the maturity date falls back on the latest relevant date, which
/// falls back on the latest date, as the pillar date does; the latest date falls back on the stored pillar date. A
/// null date is QuantLib's <c>Date()</c>, which the bootstraps read as earlier than every date. An extension point: the
/// protected date fields are written by a derived constructor and by <see cref="InitializeDates"/>, on a helper under
/// construction or on a fresh copy only.
/// </para>
/// </remarks>
public abstract class BootstrapHelper<TTermStructure> : IBootstrapHelper
    where TTermStructure : TermStructure
{
    /// <summary>QuantLib's <c>earliestDate_</c>: the earliest date at which data are needed.</summary>
    protected Date? earliestDate;

    /// <summary>QuantLib's <c>latestDate_</c>.</summary>
    protected Date? latestDate;

    /// <summary>QuantLib's <c>maturityDate_</c>: the instrument's maturity date.</summary>
    protected Date? maturityDate;

    /// <summary>QuantLib's <c>latestRelevantDate_</c>: the latest date at which data are needed.</summary>
    protected Date? latestRelevantDate;

    /// <summary>QuantLib's <c>pillarDate_</c>.</summary>
    protected Date? pillarDate;

    // Written only on a fresh copy (WithTermStructure, RedatedFor).
    private TTermStructure? termStructure;
    private Date? evaluationDate;

    /// <summary>Initializes a new instance of the <see cref="BootstrapHelper{TTermStructure}"/> class.</summary>
    /// <param name="quote">The quote the helper reprices.</param>
    protected BootstrapHelper(double quote)
    {
        Quote = quote;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BootstrapHelper{TTermStructure}"/> class whose dates may follow the
    /// evaluation date: QuantLib's <c>RelativeDateBootstrapHelper</c>.
    /// </summary>
    /// <param name="quote">The quote the helper reprices.</param>
    /// <param name="context">
    /// The evaluation context whose evaluation date the helper's dates are computed from; required when
    /// <paramref name="updateDates"/> is set.
    /// </param>
    /// <param name="updateDates">
    /// Whether the dates follow the evaluation date: <see langword="false"/> for a helper built with explicit dates.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="updateDates"/> is set and <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    protected BootstrapHelper(double quote, EvaluationContext? context, bool updateDates)
    {
        Quote = quote;
        UpdateDates = updateDates;
        if (updateDates)
        {
            ArgumentNullException.ThrowIfNull(context);
            evaluationDate = context.EvaluationDate;
        }
    }

    /// <inheritdoc/>
    public double Quote { get; }

    /// <inheritdoc/>
    public virtual Date? EarliestDate => earliestDate;

    /// <inheritdoc/>
    public virtual Date? MaturityDate => maturityDate ?? LatestRelevantDate;

    /// <inheritdoc/>
    public virtual Date? LatestRelevantDate => latestRelevantDate ?? LatestDate;

    /// <inheritdoc/>
    public virtual Date? PillarDate => pillarDate ?? LatestDate;

    /// <inheritdoc/>
    public virtual Date? LatestDate => latestDate ?? pillarDate;

    /// <summary>Gets a value indicating whether the helper's dates follow the evaluation date.</summary>
    protected bool UpdateDates { get; }

    /// <summary>
    /// Gets the evaluation date the helper's dates were computed from, or <see langword="null"/> for a helper built with
    /// explicit dates: QuantLib's <c>evaluationDate_</c>.
    /// </summary>
    protected Date? EvaluationDate => evaluationDate;

    /// <summary>Gets the term structure the helper is bound to, or <see langword="null"/> for an unbound helper.</summary>
    protected TTermStructure? TermStructure => termStructure;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The helper isn't bound to a term structure.</exception>
    public abstract double ImpliedQuote(EvaluationContext context);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The helper isn't bound to a term structure.</exception>
    public double QuoteError(EvaluationContext context) => Quote - ImpliedQuote(context);

    /// <summary>
    /// A copy of the helper bound to a term structure: QuantLib's <c>setTermStructure</c>. A derived helper that prices
    /// against the term structure overrides it, calls the base, and rebuilds on the copy what prices against it.
    /// </summary>
    /// <param name="t">The term structure.</param>
    /// <returns>The copy, of the receiver's runtime type; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="t"/> is <see langword="null"/>.</exception>
    protected internal virtual BootstrapHelper<TTermStructure> WithTermStructure(TTermStructure t)
    {
        if (t is null)
        {
            throw new ArgumentNullException(nameof(t), "null term structure given");
        }

        var copy = (BootstrapHelper<TTermStructure>)MemberwiseClone();
        copy.termStructure = t;
        return copy;
    }

    /// <summary>
    /// Computes the helper's dates for an evaluation date: QuantLib's <c>initializeDates()</c>. A helper whose dates
    /// follow the evaluation date overrides it and calls it from its constructor; the bootstraps call it on a fresh copy
    /// to re-date the helper for a curve built under another evaluation date. The base does nothing.
    /// </summary>
    /// <param name="context">The evaluation context whose evaluation date the dates are computed from.</param>
    protected virtual void InitializeDates(EvaluationContext context)
    {
    }

    /// <summary>Throws QuantLib's "term structure not set" for an unbound helper.</summary>
    /// <exception cref="NanoQLException">The helper isn't bound to a term structure.</exception>
    protected void RequireTermStructure()
    {
        if (termStructure is null)
        {
            throw new NanoQLException("term structure not set");
        }
    }

    // The helper QuantLib's update() leaves after the evaluation date moved: a copy re-dated for the context's
    // evaluation date, or the helper itself when its dates don't follow the evaluation date or already have it.
    internal BootstrapHelper<TTermStructure> RedatedFor(EvaluationContext context)
    {
        if (!UpdateDates || evaluationDate == context.EvaluationDate)
        {
            return this;
        }

        var copy = (BootstrapHelper<TTermStructure>)MemberwiseClone();
        copy.evaluationDate = context.EvaluationDate;
        copy.InitializeDates(context);
        return copy;
    }
}
