// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A zero-coupon inflation swap convention resolved in a set (<see cref="ConventionSet.ZeroCouponInflationSwap"/>): its
/// record, its base chain merged, and the swap and rate helper it feeds, on its index built in the set.
/// </summary>
/// <remarks>
/// The rate helper's swap pays its inflation leg on the fixed leg's calendar and convention and observes the index on
/// unadjusted dates: a record whose values for them differ is refused there.
/// </remarks>
public sealed class ZeroCouponInflationSwapConvention
{
    private const string Helper = nameof(ZeroCouponInflationSwapHelper);

    internal ZeroCouponInflationSwapConvention(ConventionSet set, ZeroCouponInflationSwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public ZeroCouponInflationSwapConventionRecord Record { get; }

    /// <summary>A zero-coupon inflation swap on the record's index built in the set.</summary>
    /// <param name="type">Whether the fixed leg is paid or received.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="ts">The index's zero inflation curve, or <see langword="null"/> for none.</param>
    /// <returns>The swap.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the swap needs, or a name it gives is
    /// unknown.</exception>
    public ZeroCouponInflationSwap Swap(SwapType type, double nominal, Date startDate, Date maturity, double fixedRate, ZeroInflationTermStructure? ts = null)
    {
        ZeroCouponInflationSwapConventionRecord r = Record;
        return new ZeroCouponInflationSwap(
            type,
            nominal,
            startDate,
            maturity,
            Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar)),
            Required(r, nameof(r.Convention), r.Convention),
            Set.DayCounter(Required(r, nameof(r.DayCounter), r.DayCounter)),
            fixedRate,
            Index(ts),
            Required(r, nameof(r.ObservationLag), r.ObservationLag),
            Required(r, nameof(r.Interpolation), r.Interpolation),
            r.AdjustObservationDates ?? false,
            CalendarOf(Set, r.InflationCalendar),
            r.InflationConvention);
    }

    /// <summary>A zero-coupon inflation swap rate helper on the record's index built in the set.</summary>
    /// <param name="quote">The fair rate quoted.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="ts">The index's zero inflation curve, or <see langword="null"/> for the bootstrapped curve.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the helper needs, a name it gives is unknown, or
    /// it gives a value the helper can't take.</exception>
    public ZeroCouponInflationSwapHelper RateHelper(double quote, Date maturity, EvaluationContext context, ZeroInflationTermStructure? ts = null)
    {
        ZeroCouponInflationSwapConventionRecord r = Record;
        Calendar calendar = Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar));
        BusinessDayConvention convention = Required(r, nameof(r.Convention), r.Convention);
        Untaken(Helper, r, nameof(r.InflationCalendar), CalendarOf(Set, r.InflationCalendar), calendar);
        Untaken(Helper, r, nameof(r.InflationConvention), r.InflationConvention, convention);
        Untaken(Helper, r, nameof(r.AdjustObservationDates), r.AdjustObservationDates, false);
        return new ZeroCouponInflationSwapHelper(
            quote,
            Required(r, nameof(r.ObservationLag), r.ObservationLag),
            maturity,
            calendar,
            convention,
            Set.DayCounter(Required(r, nameof(r.DayCounter), r.DayCounter)),
            Index(ts),
            Required(r, nameof(r.Interpolation), r.Interpolation),
            context);
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        if (Record.Index is not null)
        {
            _ = Index(null);
        }

        _ = CalendarOf(Set, Record.Calendar);
        _ = CalendarOf(Set, Record.InflationCalendar);
        _ = DayCounterOf(Set, Record.DayCounter);
    }

    private ZeroInflationIndex Index(ZeroInflationTermStructure? ts) =>
        Set.ZeroInflationIndex(Required(Record, nameof(Record.Index), Record.Index), ts);
}
