// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A swaption convention resolved in a set (<see cref="ConventionSet.Swaption"/>): its record, its base chain merged,
/// and the builder it feeds, on its swap index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the set's <c>MakeSwaption</c> record, read through the swap
/// index's provider when the swaption is built, or the swap index's own conventions.
/// </remarks>
public sealed class SwaptionConvention
{
    internal SwaptionConvention(ConventionSet set, SwaptionConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public SwaptionConventionRecord Record { get; }

    /// <summary>A swaption builder on the record's swap index built in the set at the swap's tenor, with every option
    /// the record gives set.</summary>
    /// <param name="optionTenor">The option's tenor.</param>
    /// <param name="strike">The strike, or <see langword="null"/> for the at-the-money strike.</param>
    /// <param name="swapTenor">The underlying swap's tenor.</param>
    /// <param name="forwardingCurve">The swap index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discountingCurve">An exogenous discount curve, or <see langword="null"/> for the forwarding curve; an
    /// overnight-indexed swap index takes none.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record gives no swap index, a name it gives is unknown, or a discount curve
    /// is given for an overnight-indexed swap index.</exception>
    public MakeSwaption Builder(
        Period optionTenor,
        double? strike,
        Period swapTenor,
        YieldTermStructure? forwardingCurve = null,
        YieldTermStructure? discountingCurve = null)
    {
        SwaptionConventionRecord r = Record;
        var b = new MakeSwaption(Index(swapTenor, forwardingCurve, discountingCurve), optionTenor, strike);
        b = r.SettlementType is SettlementType type ? b.WithSettlementType(type) : b;
        b = r.SettlementMethod is SettlementMethod method ? b.WithSettlementMethod(method) : b;
        b = r.OptionConvention is BusinessDayConvention convention ? b.WithOptionConvention(convention) : b;
        return r.ExerciseCalendar is string calendar ? b.WithExerciseCalendar(Set.Calendar(calendar)) : b;
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        if (Record.SwapIndex is not null)
        {
            _ = Index(new Period(10, TimeUnit.Years), null, null);
        }

        _ = CalendarOf(Set, Record.ExerciseCalendar);
    }

    private SwapIndex Index(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting) =>
        SwapIndexOf(Set, Required(Record, nameof(Record.SwapIndex), Record.SwapIndex), tenor, forwarding, discounting);
}
