// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/ConventionsXmlTests.cs (nanoql's own record kind, which QuantLib's presets don't use: the round trips, and the set's, Describe and sensitivity facts)

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>A joint calendar (<c>JointCalendar</c>): two or more calendars joined by a rule.</summary>
public sealed record JointCalendarRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="JointCalendarRecord"/> class.</summary>
    /// <param name="name">The calendar's name in the set.</param>
    /// <param name="base">The joint calendar it is based on, or <see langword="null"/>.</param>
    public JointCalendarRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the rule joining the members, or <see langword="null"/> for the base's, else QuantLib's default,
    /// <see cref="JointCalendarRule.JoinHolidays"/>.</summary>
    public JointCalendarRule? Rule { get; init; }

    /// <summary>Gets the joined calendars' names, in order: two or more, or none to take the base's.</summary>
    public ImmutableArray<string> Members { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(JointCalendarRecord? other) =>
        other is not null && base.Equals(other) && Rule == other.Rule && RecordLists.Equal(Members, other.Members);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(base.GetHashCode());
        hash.Add(Rule);
        RecordLists.Add(ref hash, Members);
        return hash.ToHashCode();
    }
}
