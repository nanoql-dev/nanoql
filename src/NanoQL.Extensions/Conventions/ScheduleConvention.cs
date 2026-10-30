// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A schedule convention resolved in a set (<see cref="ConventionSet.Schedule"/>): its record, its base chain merged,
/// and the builder it feeds, on the set's calendars.
/// </summary>
public sealed class ScheduleConvention
{
    internal ScheduleConvention(ConventionSet set, ScheduleConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public ScheduleConventionRecord Record { get; }

    /// <summary>
    /// A schedule builder with every option the record gives set; the dates are the trade's, and an option the
    /// record leaves out is <see cref="MakeSchedule"/>'s default.
    /// </summary>
    /// <returns>The builder.</returns>
    public MakeSchedule Builder()
    {
        ScheduleConventionRecord r = Record;
        var b = new MakeSchedule();
        if (r.Tenor is Period tenor)
        {
            b = b.WithTenor(tenor);
        }

        if (r.Frequency is Frequency frequency)
        {
            b = b.WithFrequency(frequency);
        }

        if (r.Calendar is string calendar)
        {
            b = b.WithCalendar(Set.Calendar(calendar));
        }

        if (r.Convention is BusinessDayConvention convention)
        {
            b = b.WithConvention(convention);
        }

        if (r.TerminationDateConvention is BusinessDayConvention termination)
        {
            b = b.WithTerminationDateConvention(termination);
        }

        if (r.Rule is DateGenerationRule rule)
        {
            b = b.WithRule(rule);
        }

        return r.EndOfMonth is bool endOfMonth ? b.EndOfMonth(endOfMonth) : b;
    }

    // Every name the record gives resolves.
    internal void Validate() => _ = Builder();
}
