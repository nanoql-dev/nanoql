// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A year-on-year inflation cap and floor convention resolved in a set (<see cref="ConventionSet.YoYInflationCapFloor"/>):
/// its record, its base chain merged, and the builder it feeds, on its index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the set's <c>MakeYoYInflationCapFloor</c> record, read through
/// the index's provider when the instrument is built.
/// </remarks>
public sealed class YoYInflationCapFloorConvention
{
    internal YoYInflationCapFloorConvention(ConventionSet set, YoYInflationCapFloorConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public YoYInflationCapFloorConventionRecord Record { get; }

    /// <summary>A year-on-year inflation cap or floor builder on the record's index built in the set, with every option
    /// the record gives set.</summary>
    /// <param name="type">A cap or a floor.</param>
    /// <param name="length">The instrument's length, in years.</param>
    /// <param name="ts">The index's year-on-year inflation curve, or <see langword="null"/> for none.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record leaves out the index, its observation, or the calendar, or a name
    /// it gives is unknown.</exception>
    public MakeYoYInflationCapFloor Builder(YoYInflationCapFloorType type, int length, YoYInflationTermStructure? ts = null)
    {
        YoYInflationCapFloorConventionRecord r = Record;
        var b = new MakeYoYInflationCapFloor(
            type,
            Set.YoYInflationIndex(Required(r, nameof(r.Index), r.Index), ts),
            length,
            Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar)),
            Required(r, nameof(r.ObservationLag), r.ObservationLag),
            Required(r, nameof(r.Interpolation), r.Interpolation));
        b = r.PaymentDayCounter is string dayCounter ? b.WithPaymentDayCounter(Set.DayCounter(dayCounter)) : b;
        b = r.PaymentAdjustment is BusinessDayConvention adjustment ? b.WithPaymentAdjustment(adjustment) : b;
        return r.FixingDays is int fixingDays ? b.WithFixingDays(fixingDays) : b;
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        _ = Record.Index is null ? null : Set.YoYInflationIndex(Record.Index);
        _ = CalendarOf(Set, Record.Calendar);
        _ = DayCounterOf(Set, Record.PaymentDayCounter);
    }
}
