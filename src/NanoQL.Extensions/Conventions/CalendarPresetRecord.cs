// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A calendar preset (<c>CalendarPreset</c>): one of QuantLib's calendars by its type and market, its rules code, with
/// QuantLib's name and the holiday edits the preset's constructor applies through the set.
/// </summary>
/// <remarks>
/// The shipped presets carry QuantLib's names and no edits. A record of a preset's name must keep QuantLib's name.
/// </remarks>
public sealed record CalendarPresetRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CalendarPresetRecord"/> class.</summary>
    /// <param name="name">The preset's name, its type and market (<c>TARGET</c>, <c>UnitedStates.NYSE</c>).</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CalendarPresetRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets QuantLib's name of the calendar, or <see langword="null"/> to take the base's.</summary>
    public string? QuantLibName { get; init; }

    /// <summary>Gets the dates added to the calendar's holidays.</summary>
    public ImmutableArray<Date> AddedHolidays { get; init; } = [];

    /// <summary>Gets the dates removed from the calendar's holidays.</summary>
    public ImmutableArray<Date> RemovedHolidays { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(CalendarPresetRecord? other) =>
        other is not null && base.Equals(other) && QuantLibName == other.QuantLibName
        && RecordLists.Equal(AddedHolidays, other.AddedHolidays) && RecordLists.Equal(RemovedHolidays, other.RemovedHolidays);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(base.GetHashCode());
        hash.Add(QuantLibName);
        RecordLists.Add(ref hash, AddedHolidays);
        RecordLists.Add(ref hash, RemovedHolidays);
        return hash.ToHashCode();
    }
}
