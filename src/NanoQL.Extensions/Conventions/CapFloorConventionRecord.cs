// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A cap's or floor's conventions (<c>CapFloorConvention</c>), which feed
/// <see cref="NanoQL.Instruments.MakeCapFloor"/>; or, named <c>MakeCapFloor</c>, the builder's defaults.
/// </summary>
public sealed record CapFloorConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CapFloorConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CapFloorConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the Ibor index's name, or an Ibor family's, or <see langword="null"/> to take the base's: none in the
    /// default record.
    /// </summary>
    public string? Index { get; init; }

    /// <summary>
    /// Gets the tenor of the Ibor family Index names, or <see langword="null"/> for none: it comes with
    /// <see cref="Index"/>, from the record that gives that; none for an index.
    /// </summary>
    public Period? IndexTenor { get; init; }

    /// <summary>
    /// Gets the coupons' tenor, or <see langword="null"/> to take the base's: the index's by default.
    /// </summary>
    public Period? Tenor { get; init; }

    /// <summary>
    /// Gets the leg's calendar's name, or <see langword="null"/> to take the base's: the index's fixing calendar by
    /// default.
    /// </summary>
    public string? Calendar { get; init; }

    /// <summary>
    /// Gets the leg's business-day convention, or <see langword="null"/> to take the base's: the index's by default.
    /// </summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>
    /// Gets the leg's termination-date convention, or <see langword="null"/> to take the base's: the index's convention
    /// by default.
    /// </summary>
    public BusinessDayConvention? TerminationDateConvention { get; init; }

    /// <summary>
    /// Gets the leg's date-generation rule, or <see langword="null"/> to take the base's: the swap builder's default by
    /// default.
    /// </summary>
    public DateGenerationRule? Rule { get; init; }

    /// <summary>
    /// Gets whether the leg's dates stay at month end, or <see langword="null"/> to take the base's: the swap builder's
    /// default by default.
    /// </summary>
    public bool? EndOfMonth { get; init; }

    /// <summary>
    /// Gets the leg's day counter's name, or <see langword="null"/> to take the base's: the index's by default.
    /// </summary>
    public string? DayCounter { get; init; }

    /// <summary>
    /// Gets whether the first caplet is excluded, or <see langword="null"/> to take the base's: the default record
    /// only.
    /// </summary>
    public bool? FirstCapletExcluded { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CapFloorConventionRecord Over(CapFloorConventionRecord lower) =>
        this with
        {
            Index = Index ?? lower.Index,
            IndexTenor = Index is null ? IndexTenor ?? lower.IndexTenor : IndexTenor,
            Tenor = Tenor ?? lower.Tenor,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            TerminationDateConvention = TerminationDateConvention ?? lower.TerminationDateConvention,
            Rule = Rule ?? lower.Rule,
            EndOfMonth = EndOfMonth ?? lower.EndOfMonth,
            DayCounter = DayCounter ?? lower.DayCounter,
            FirstCapletExcluded = FirstCapletExcluded ?? lower.FirstCapletExcluded,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Index,
        Calendar,
        DayCounter,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Index), Index is not null),
            (nameof(IndexTenor), IndexTenor is not null),
            (nameof(Tenor), Tenor is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(TerminationDateConvention), TerminationDateConvention is not null),
            (nameof(Rule), Rule is not null),
            (nameof(EndOfMonth), EndOfMonth is not null),
            (nameof(DayCounter), DayCounter is not null),
            (nameof(FirstCapletExcluded), FirstCapletExcluded is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CapFloorConventionRecord Named(string name) =>
        new(name)
        {
            Index = Index,
            IndexTenor = IndexTenor,
            Tenor = Tenor,
            Calendar = Calendar,
            Convention = Convention,
            TerminationDateConvention = TerminationDateConvention,
            Rule = Rule,
            EndOfMonth = EndOfMonth,
            DayCounter = DayCounter,
            FirstCapletExcluded = FirstCapletExcluded,
        };
}
