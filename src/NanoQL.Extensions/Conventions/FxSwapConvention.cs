// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An FX swap and forward convention resolved in a set (<see cref="ConventionSet.FxSwap"/>): its record, its base chain
/// merged, and the rate helper and forward it feeds.
/// </summary>
/// <remarks>
/// The helper takes the spot days, calendar, convention, end of month, collateral side and trading calendar, but no
/// currencies, its quotes those of whatever pair the curves are, and no payment calendar; the forward takes the
/// currencies, the spot days as its settlement days and the payment calendar, and none of the others: a record giving
/// a field its route can't take is refused there.
/// </remarks>
public sealed class FxSwapConvention
{
    internal FxSwapConvention(ConventionSet set, FxSwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public FxSwapConventionRecord Record { get; }

    /// <summary>An FX swap rate helper.</summary>
    /// <param name="forwardPoints">The forward points quoted.</param>
    /// <param name="spot">The spot exchange rate, units of quote currency per unit of base currency.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="collateralCurve">The collateral currency's discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the helper needs, a name it gives is unknown, or
    /// it gives a value the helper can't take.</exception>
    public FxSwapRateHelper RateHelper(double forwardPoints, double spot, Period tenor, YieldTermStructure? collateralCurve, EvaluationContext context)
    {
        const string route = nameof(FxSwapRateHelper);
        FxSwapConventionRecord r = Record;
        Untaken(route, r, nameof(r.BaseCurrency), r.BaseCurrency);
        Untaken(route, r, nameof(r.QuoteCurrency), r.QuoteCurrency);
        Untaken(route, r, nameof(r.PaymentCalendar), r.PaymentCalendar);
        return new FxSwapRateHelper(
            forwardPoints,
            spot,
            tenor,
            Required(r, nameof(r.FixingDays), r.FixingDays),
            Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar)),
            Required(r, nameof(r.Convention), r.Convention),
            Required(r, nameof(r.EndOfMonth), r.EndOfMonth),
            Required(r, nameof(r.CollateralOnBaseCurrency), r.CollateralOnBaseCurrency),
            collateralCurve,
            context,
            CalendarOf(Set, r.TradingCalendar));
    }

    /// <summary>An FX forward between the record's currencies.</summary>
    /// <param name="baseNominal">The amount in the base currency.</param>
    /// <param name="quoteNominal">The amount in the quote currency.</param>
    /// <param name="maturity">The date the amounts are exchanged.</param>
    /// <param name="payBaseCurrency">Whether the base currency is paid and the quote currency received; otherwise the
    /// reverse.</param>
    /// <returns>The forward, settling after the record's spot days on its payment calendar.</returns>
    /// <exception cref="ArgumentException">The record leaves out a currency, a name it gives is unknown, or it gives a
    /// value the forward can't take.</exception>
    public FxForward Forward(double baseNominal, double quoteNominal, Date maturity, bool payBaseCurrency)
    {
        const string route = nameof(FxForward);
        FxSwapConventionRecord r = Record;
        Untaken(route, r, nameof(r.Calendar), r.Calendar);
        Untaken(route, r, nameof(r.Convention), r.Convention);
        Untaken(route, r, nameof(r.EndOfMonth), r.EndOfMonth);
        Untaken(route, r, nameof(r.CollateralOnBaseCurrency), r.CollateralOnBaseCurrency);
        Untaken(route, r, nameof(r.TradingCalendar), r.TradingCalendar);
        return new FxForward(
            baseNominal,
            Set.Currency(Required(r, nameof(r.BaseCurrency), r.BaseCurrency)),
            quoteNominal,
            Set.Currency(Required(r, nameof(r.QuoteCurrency), r.QuoteCurrency)),
            maturity,
            payBaseCurrency,
            r.FixingDays ?? 2,
            CalendarOf(Set, r.PaymentCalendar));
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        FxSwapConventionRecord r = Record;
        _ = r.BaseCurrency is null ? null : Set.Currency(r.BaseCurrency);
        _ = r.QuoteCurrency is null ? null : Set.Currency(r.QuoteCurrency);
        _ = CalendarOf(Set, r.Calendar);
        _ = CalendarOf(Set, r.TradingCalendar);
        _ = CalendarOf(Set, r.PaymentCalendar);
    }
}
