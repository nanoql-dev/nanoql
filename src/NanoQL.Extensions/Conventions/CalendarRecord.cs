// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/ConventionsXmlTests.cs (nanoql's own record kind, which QuantLib's presets don't use: the round trips, and the set's, pairing, Describe and sensitivity facts)

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A named calendar on a base calendar with holidays added or removed (<c>Calendar</c>), as QuantLib's
/// <c>addHoliday</c> and <c>removeHoliday</c> edit one: under the base's own name, the base's edits.
/// </summary>
/// <remarks>
/// The calendar is the base's, its name the base's name (two calendars are equal by type, name and edits), with the
/// record's edits applied after the base's. A record of a preset's name based on that name edits the preset wherever
/// it is built under the set.
/// </remarks>
public sealed record CalendarRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CalendarRecord"/> class.</summary>
    /// <param name="name">The calendar's name in the set.</param>
    /// <param name="base">The calendar it edits: any calendar name, its own for the record below it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="base"/> is null.</exception>
    public CalendarRecord(string name, string @base)
        : base(name, @base ?? throw new ArgumentNullException(nameof(@base)))
    {
    }

    /// <summary>Gets the dates added to the base's holidays.</summary>
    public ImmutableArray<Date> AddedHolidays { get; init; } = [];

    /// <summary>Gets the dates removed from the base's holidays.</summary>
    public ImmutableArray<Date> RemovedHolidays { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(CalendarRecord? other) =>
        other is not null && base.Equals(other)
        && RecordLists.Equal(AddedHolidays, other.AddedHolidays) && RecordLists.Equal(RemovedHolidays, other.RemovedHolidays);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(base.GetHashCode());
        RecordLists.Add(ref hash, AddedHolidays);
        RecordLists.Add(ref hash, RemovedHolidays);
        return hash.ToHashCode();
    }
}
