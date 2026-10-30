// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A cap and floor convention resolved in a set (<see cref="ConventionSet.CapFloor"/>): its record, its base chain
/// merged, and the builder it feeds, on its index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the index's conventions, the set's <c>MakeVanillaSwap.</c> record
/// for the leg's rule and end of month, and its <c>MakeCapFloor</c> record for the first caplet, which the builder sets
/// only through its forward start and effective date, so a record's other value is refused.
/// </remarks>
public sealed class CapFloorConvention
{
    internal CapFloorConvention(ConventionSet set, CapFloorConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public CapFloorConventionRecord Record { get; }

    /// <summary>A cap or floor builder on the record's index built in the set, with every option the record gives
    /// set.</summary>
    /// <param name="type">A cap or a floor.</param>
    /// <param name="tenor">The instrument's tenor.</param>
    /// <param name="forwardingCurve">The index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record gives no index, a name it gives is unknown, or it gives a value the
    /// builder can't take.</exception>
    public MakeCapFloor Builder(CapFloorType type, Period tenor, YieldTermStructure? forwardingCurve = null)
    {
        CapFloorConventionRecord r = Record;
        Untaken(nameof(MakeCapFloor), r, nameof(r.FirstCapletExcluded), r.FirstCapletExcluded, MakeCapFloor.Defaults(Set).FirstCapletExcluded);
        var b = new MakeCapFloor(type, tenor, Ibor(Set, Required(r, nameof(r.Index), r.Index), r.IndexTenor, forwardingCurve));
        b = r.Tenor is Period couponTenor ? b.WithTenor(couponTenor) : b;
        b = r.Calendar is string calendar ? b.WithCalendar(Set.Calendar(calendar)) : b;
        b = r.Convention is BusinessDayConvention convention ? b.WithConvention(convention) : b;
        b = r.TerminationDateConvention is BusinessDayConvention termination ? b.WithTerminationDateConvention(termination) : b;
        b = r.Rule is DateGenerationRule rule ? b.WithRule(rule) : b;
        b = r.EndOfMonth is bool endOfMonth ? b.WithEndOfMonth(endOfMonth) : b;
        return r.DayCounter is string dayCounter ? b.WithDayCount(Set.DayCounter(dayCounter)) : b;
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        if (Record.Index is not null)
        {
            _ = Ibor(Set, Record.Index, Record.IndexTenor, null);
        }

        _ = CalendarOf(Set, Record.Calendar);
        _ = DayCounterOf(Set, Record.DayCounter);
    }
}
