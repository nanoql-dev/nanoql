// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A swaption's conventions (<c>SwaptionConvention</c>), which feed <see cref="NanoQL.Instruments.MakeSwaption"/>; or,
/// named <c>MakeSwaption</c>, the builder's defaults.
/// </summary>
public sealed record SwaptionConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="SwaptionConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public SwaptionConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the swap index family's name, or an overnight-indexed swap index's, or <see langword="null"/> to take the
    /// base's: none in the default record.
    /// </summary>
    public string? SwapIndex { get; init; }

    /// <summary>Gets the settlement type, or <see langword="null"/> to take the base's.</summary>
    public SettlementType? SettlementType { get; init; }

    /// <summary>Gets the settlement method, or <see langword="null"/> to take the base's.</summary>
    public SettlementMethod? SettlementMethod { get; init; }

    /// <summary>
    /// Gets the business-day convention the option tenor is advanced by, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? OptionConvention { get; init; }

    /// <summary>
    /// Gets the exercise calendar's name, or <see langword="null"/> to take the base's: the swap index's fixing
    /// calendar by default.
    /// </summary>
    public string? ExerciseCalendar { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal SwaptionConventionRecord Over(SwaptionConventionRecord lower) =>
        this with
        {
            SwapIndex = SwapIndex ?? lower.SwapIndex,
            SettlementType = SettlementType ?? lower.SettlementType,
            SettlementMethod = SettlementMethod ?? lower.SettlementMethod,
            OptionConvention = OptionConvention ?? lower.OptionConvention,
            ExerciseCalendar = ExerciseCalendar ?? lower.ExerciseCalendar,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        SwapIndex,
        ExerciseCalendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(SwapIndex), SwapIndex is not null),
            (nameof(SettlementType), SettlementType is not null),
            (nameof(SettlementMethod), SettlementMethod is not null),
            (nameof(OptionConvention), OptionConvention is not null),
            (nameof(ExerciseCalendar), ExerciseCalendar is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal SwaptionConventionRecord Named(string name) =>
        new(name)
        {
            SwapIndex = SwapIndex,
            SettlementType = SettlementType,
            SettlementMethod = SettlementMethod,
            OptionConvention = OptionConvention,
            ExerciseCalendar = ExerciseCalendar,
        };
}
