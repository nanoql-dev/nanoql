// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/ConventionsXmlTests.cs (nanoql's own record kind, which QuantLib's presets don't use: the round trips, and the Describe and sensitivity facts)

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A bespoke calendar (<c>BespokeCalendar</c>): weekend days and holidays given as data, its name the calendar's own.
/// </summary>
public sealed record BespokeCalendarRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="BespokeCalendarRecord"/> class.</summary>
    /// <param name="name">The calendar's name, in the set and as its own.</param>
    /// <param name="base">The bespoke calendar it is based on, or <see langword="null"/>.</param>
    public BespokeCalendarRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the weekend days, added to the base's.</summary>
    public ImmutableArray<Weekday> Weekends { get; init; } = [];

    /// <summary>Gets the holidays, added after the base's.</summary>
    public ImmutableArray<Date> Holidays { get; init; } = [];

    /// <summary>Gets the dates removed from the holidays, weekend days among them, after the holidays are added.</summary>
    public ImmutableArray<Date> RemovedHolidays { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(BespokeCalendarRecord? other) =>
        other is not null && base.Equals(other) && RecordLists.Equal(Weekends, other.Weekends)
        && RecordLists.Equal(Holidays, other.Holidays) && RecordLists.Equal(RemovedHolidays, other.RemovedHolidays);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(base.GetHashCode());
        RecordLists.Add(ref hash, Weekends);
        RecordLists.Add(ref hash, Holidays);
        RecordLists.Add(ref hash, RemovedHolidays);
        return hash.ToHashCode();
    }
}
