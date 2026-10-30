// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A CMS swap's conventions (<c>CmsConvention</c>), which feed <see cref="NanoQL.Instruments.MakeCms"/>; or, named
/// <c>MakeCms</c>, the builder's defaults.
/// </summary>
public sealed record CmsConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CmsConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CmsConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the CMS leg's swap index family's name, or <see langword="null"/> to take the base's: none in the default
    /// record.
    /// </summary>
    public string? SwapIndex { get; init; }

    /// <summary>
    /// Gets the swap index's tenor, or <see langword="null"/> for none: it comes with <see cref="SwapIndex"/>, from the
    /// record that gives that.
    /// </summary>
    public Period? SwapIndexTenor { get; init; }

    /// <summary>Gets the CMS leg's tenor, or <see langword="null"/> to take the base's.</summary>
    public Period? CmsLegTenor { get; init; }

    /// <summary>
    /// Gets the CMS leg's calendar's name, or <see langword="null"/> to take the base's: the swap index's fixing
    /// calendar by default.
    /// </summary>
    public string? CmsLegCalendar { get; init; }

    /// <summary>Gets the CMS leg's business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? CmsLegConvention { get; init; }

    /// <summary>Gets the CMS leg's termination-date convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? CmsLegTerminationDateConvention { get; init; }

    /// <summary>Gets the CMS leg's date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? CmsLegRule { get; init; }

    /// <summary>
    /// Gets whether the CMS leg's dates stay at month end, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? CmsLegEndOfMonth { get; init; }

    /// <summary>Gets the CMS leg's day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? CmsLegDayCounter { get; init; }

    /// <summary>
    /// Gets the floating leg's Ibor index's name, or an Ibor family's, or <see langword="null"/> to take the base's:
    /// the swap index's own by default.
    /// </summary>
    public string? FloatingLegIndex { get; init; }

    /// <summary>
    /// Gets the tenor of the Ibor family FloatingLegIndex names, or <see langword="null"/> for none: it comes with
    /// <see cref="FloatingLegIndex"/>, from the record that gives that; none for an index.
    /// </summary>
    public Period? FloatingLegIndexTenor { get; init; }

    /// <summary>
    /// Gets the floating leg's tenor, or <see langword="null"/> to take the base's: the index's by default.
    /// </summary>
    public Period? FloatingLegTenor { get; init; }

    /// <summary>
    /// Gets the floating leg's calendar's name, or <see langword="null"/> to take the base's: the index's fixing
    /// calendar by default.
    /// </summary>
    public string? FloatingLegCalendar { get; init; }

    /// <summary>
    /// Gets the floating leg's business-day convention, or <see langword="null"/> to take the base's: the index's by
    /// default.
    /// </summary>
    public BusinessDayConvention? FloatingLegConvention { get; init; }

    /// <summary>
    /// Gets the floating leg's termination-date convention, or <see langword="null"/> to take the base's: the index's
    /// convention by default.
    /// </summary>
    public BusinessDayConvention? FloatingLegTerminationDateConvention { get; init; }

    /// <summary>Gets the floating leg's date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? FloatingLegRule { get; init; }

    /// <summary>
    /// Gets whether the floating leg's dates stay at month end, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? FloatingLegEndOfMonth { get; init; }

    /// <summary>
    /// Gets the floating leg's day counter's name, or <see langword="null"/> to take the base's: the index's by
    /// default.
    /// </summary>
    public string? FloatingLegDayCounter { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CmsConventionRecord Over(CmsConventionRecord lower) =>
        this with
        {
            SwapIndex = SwapIndex ?? lower.SwapIndex,
            SwapIndexTenor = SwapIndex is null ? SwapIndexTenor ?? lower.SwapIndexTenor : SwapIndexTenor,
            CmsLegTenor = CmsLegTenor ?? lower.CmsLegTenor,
            CmsLegCalendar = CmsLegCalendar ?? lower.CmsLegCalendar,
            CmsLegConvention = CmsLegConvention ?? lower.CmsLegConvention,
            CmsLegTerminationDateConvention = CmsLegTerminationDateConvention ?? lower.CmsLegTerminationDateConvention,
            CmsLegRule = CmsLegRule ?? lower.CmsLegRule,
            CmsLegEndOfMonth = CmsLegEndOfMonth ?? lower.CmsLegEndOfMonth,
            CmsLegDayCounter = CmsLegDayCounter ?? lower.CmsLegDayCounter,
            FloatingLegIndex = FloatingLegIndex ?? lower.FloatingLegIndex,
            FloatingLegIndexTenor = FloatingLegIndex is null ? FloatingLegIndexTenor ?? lower.FloatingLegIndexTenor : FloatingLegIndexTenor,
            FloatingLegTenor = FloatingLegTenor ?? lower.FloatingLegTenor,
            FloatingLegCalendar = FloatingLegCalendar ?? lower.FloatingLegCalendar,
            FloatingLegConvention = FloatingLegConvention ?? lower.FloatingLegConvention,
            FloatingLegTerminationDateConvention = FloatingLegTerminationDateConvention ?? lower.FloatingLegTerminationDateConvention,
            FloatingLegRule = FloatingLegRule ?? lower.FloatingLegRule,
            FloatingLegEndOfMonth = FloatingLegEndOfMonth ?? lower.FloatingLegEndOfMonth,
            FloatingLegDayCounter = FloatingLegDayCounter ?? lower.FloatingLegDayCounter,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        SwapIndex,
        CmsLegCalendar,
        CmsLegDayCounter,
        FloatingLegIndex,
        FloatingLegCalendar,
        FloatingLegDayCounter,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(SwapIndex), SwapIndex is not null),
            (nameof(SwapIndexTenor), SwapIndexTenor is not null),
            (nameof(CmsLegTenor), CmsLegTenor is not null),
            (nameof(CmsLegCalendar), CmsLegCalendar is not null),
            (nameof(CmsLegConvention), CmsLegConvention is not null),
            (nameof(CmsLegTerminationDateConvention), CmsLegTerminationDateConvention is not null),
            (nameof(CmsLegRule), CmsLegRule is not null),
            (nameof(CmsLegEndOfMonth), CmsLegEndOfMonth is not null),
            (nameof(CmsLegDayCounter), CmsLegDayCounter is not null),
            (nameof(FloatingLegIndex), FloatingLegIndex is not null),
            (nameof(FloatingLegIndexTenor), FloatingLegIndexTenor is not null),
            (nameof(FloatingLegTenor), FloatingLegTenor is not null),
            (nameof(FloatingLegCalendar), FloatingLegCalendar is not null),
            (nameof(FloatingLegConvention), FloatingLegConvention is not null),
            (nameof(FloatingLegTerminationDateConvention), FloatingLegTerminationDateConvention is not null),
            (nameof(FloatingLegRule), FloatingLegRule is not null),
            (nameof(FloatingLegEndOfMonth), FloatingLegEndOfMonth is not null),
            (nameof(FloatingLegDayCounter), FloatingLegDayCounter is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CmsConventionRecord Named(string name) =>
        new(name)
        {
            SwapIndex = SwapIndex,
            SwapIndexTenor = SwapIndexTenor,
            CmsLegTenor = CmsLegTenor,
            CmsLegCalendar = CmsLegCalendar,
            CmsLegConvention = CmsLegConvention,
            CmsLegTerminationDateConvention = CmsLegTerminationDateConvention,
            CmsLegRule = CmsLegRule,
            CmsLegEndOfMonth = CmsLegEndOfMonth,
            CmsLegDayCounter = CmsLegDayCounter,
            FloatingLegIndex = FloatingLegIndex,
            FloatingLegIndexTenor = FloatingLegIndexTenor,
            FloatingLegTenor = FloatingLegTenor,
            FloatingLegCalendar = FloatingLegCalendar,
            FloatingLegConvention = FloatingLegConvention,
            FloatingLegTerminationDateConvention = FloatingLegTerminationDateConvention,
            FloatingLegRule = FloatingLegRule,
            FloatingLegEndOfMonth = FloatingLegEndOfMonth,
            FloatingLegDayCounter = FloatingLegDayCounter,
        };
}
