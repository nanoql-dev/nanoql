// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A schedule's conventions (<c>ScheduleConvention</c>), which feed <see cref="NanoQL.Time.MakeSchedule"/>; the dates
/// are the trade's.
/// </summary>
public sealed record ScheduleConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public ScheduleConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the schedule's tenor, or <see langword="null"/> to take the base's.</summary>
    public Period? Tenor { get; init; }

    /// <summary>
    /// Gets the schedule's frequency, in place of its tenor, or <see langword="null"/> to take the base's.
    /// </summary>
    public Frequency? Frequency { get; init; }

    /// <summary>Gets the calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>
    /// Gets the termination date's business-day convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? TerminationDateConvention { get; init; }

    /// <summary>Gets the date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? Rule { get; init; }

    /// <summary>Gets whether the dates stay at month end, or <see langword="null"/> to take the base's.</summary>
    public bool? EndOfMonth { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal ScheduleConventionRecord Over(ScheduleConventionRecord lower) =>
        this with
        {
            Tenor = Tenor ?? lower.Tenor,
            Frequency = Frequency ?? lower.Frequency,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            TerminationDateConvention = TerminationDateConvention ?? lower.TerminationDateConvention,
            Rule = Rule ?? lower.Rule,
            EndOfMonth = EndOfMonth ?? lower.EndOfMonth,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Calendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Tenor), Tenor is not null),
            (nameof(Frequency), Frequency is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(TerminationDateConvention), TerminationDateConvention is not null),
            (nameof(Rule), Rule is not null),
            (nameof(EndOfMonth), EndOfMonth is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal ScheduleConventionRecord Named(string name) =>
        new(name)
        {
            Tenor = Tenor,
            Frequency = Frequency,
            Calendar = Calendar,
            Convention = Convention,
            TerminationDateConvention = TerminationDateConvention,
            Rule = Rule,
            EndOfMonth = EndOfMonth,
        };
}
