// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A year-on-year inflation swap convention resolved in a set (<see cref="ConventionSet.YearOnYearInflationSwap"/>):
/// its record, its base chain merged, and the rate helper it feeds, on its index built in the set.
/// </summary>
public sealed class YearOnYearInflationSwapConvention
{
    internal YearOnYearInflationSwapConvention(ConventionSet set, YearOnYearInflationSwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public YearOnYearInflationSwapConventionRecord Record { get; }

    /// <summary>A year-on-year inflation swap rate helper on the record's index built in the set.</summary>
    /// <param name="quote">The fair rate quoted.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="nominalCurve">The nominal curve the swap is discounted on, or <see langword="null"/> for
    /// none.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="ts">The index's year-on-year inflation curve, or <see langword="null"/> for the bootstrapped
    /// curve.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the helper needs, or a name it gives is
    /// unknown.</exception>
    public YearOnYearInflationSwapHelper RateHelper(
        double quote,
        Date maturity,
        YieldTermStructure? nominalCurve,
        EvaluationContext context,
        YoYInflationTermStructure? ts = null)
    {
        YearOnYearInflationSwapConventionRecord r = Record;
        return new YearOnYearInflationSwapHelper(
            quote,
            Required(r, nameof(r.ObservationLag), r.ObservationLag),
            maturity,
            Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar)),
            Required(r, nameof(r.PaymentConvention), r.PaymentConvention),
            Set.DayCounter(Required(r, nameof(r.DayCounter), r.DayCounter)),
            Index(ts),
            Required(r, nameof(r.Interpolation), r.Interpolation),
            nominalCurve,
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
        _ = DayCounterOf(Set, Record.DayCounter);
    }

    private YoYInflationIndex Index(YoYInflationTermStructure? ts) =>
        Set.YoYInflationIndex(Required(Record, nameof(Record.Index), Record.Index), ts);
}
