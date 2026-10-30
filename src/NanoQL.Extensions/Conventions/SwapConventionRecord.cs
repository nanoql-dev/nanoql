// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A fixed-against-Ibor swap's conventions (<c>SwapConvention</c>), which feed
/// <see cref="NanoQL.Instruments.MakeVanillaSwap"/> and <see cref="NanoQL.TermStructures.SwapRateHelper"/>; or, named
/// <c>MakeVanillaSwap.</c> and a currency's code, the builder's defaults for that currency.
/// </summary>
public sealed record SwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="SwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public SwapConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the settlement days, or <see langword="null"/> to take the base's: the index's value date by default.
    /// </summary>
    public int? SettlementDays { get; init; }

    /// <summary>
    /// Gets the calendar the settlement days are counted on, or <see langword="null"/> to take the base's: the floating
    /// leg's by default.
    /// </summary>
    public string? SettlementCalendar { get; init; }

    /// <summary>Gets the fixed leg's tenor, or <see langword="null"/> to take the base's.</summary>
    public Period? FixedLegTenor { get; init; }

    /// <summary>
    /// Gets the fixed leg's tenor at the swap tenors on one side of a threshold, or <see langword="null"/> for none: it
    /// comes with <see cref="FixedLegTenor"/>, from the record that gives that; a currency's default record only.
    /// </summary>
    public SwapTenorThreshold? FixedLegTenorThreshold { get; init; }

    /// <summary>
    /// Gets the fixed leg's calendar's name, or <see langword="null"/> to take the base's: the index's fixing calendar
    /// by default.
    /// </summary>
    public string? FixedLegCalendar { get; init; }

    /// <summary>Gets the fixed leg's business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? FixedLegConvention { get; init; }

    /// <summary>
    /// Gets the fixed leg's termination-date convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? FixedLegTerminationDateConvention { get; init; }

    /// <summary>Gets the fixed leg's date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? FixedLegRule { get; init; }

    /// <summary>
    /// Gets whether the fixed leg's dates stay at month end, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? FixedLegEndOfMonth { get; init; }

    /// <summary>Gets the fixed leg's day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? FixedLegDayCounter { get; init; }

    /// <summary>
    /// Gets the floating leg's Ibor index's name, or an Ibor family's, or <see langword="null"/> to take the base's:
    /// none in a currency's default record.
    /// </summary>
    public string? Index { get; init; }

    /// <summary>
    /// Gets the tenor of the Ibor family Index names, or <see langword="null"/> for none: it comes with
    /// <see cref="Index"/>, from the record that gives that; none for an index.
    /// </summary>
    public Period? IndexTenor { get; init; }

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
    /// Gets whether the floating leg's dates stay at month end, or <see langword="null"/> to take the base's: the
    /// maturity's default too.
    /// </summary>
    public bool? FloatingLegEndOfMonth { get; init; }

    /// <summary>
    /// Gets the floating coupons' day counter's name, or <see langword="null"/> to take the base's: the index's by
    /// default.
    /// </summary>
    public string? FloatingLegDayCounter { get; init; }

    /// <summary>
    /// Gets the payment convention, or <see langword="null"/> to take the base's: the floating schedule's by default.
    /// </summary>
    public BusinessDayConvention? PaymentConvention { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal SwapConventionRecord Over(SwapConventionRecord lower) =>
        this with
        {
            SettlementDays = SettlementDays ?? lower.SettlementDays,
            SettlementCalendar = SettlementCalendar ?? lower.SettlementCalendar,
            FixedLegTenor = FixedLegTenor ?? lower.FixedLegTenor,
            FixedLegTenorThreshold = FixedLegTenor is null ? FixedLegTenorThreshold ?? lower.FixedLegTenorThreshold : FixedLegTenorThreshold,
            FixedLegCalendar = FixedLegCalendar ?? lower.FixedLegCalendar,
            FixedLegConvention = FixedLegConvention ?? lower.FixedLegConvention,
            FixedLegTerminationDateConvention = FixedLegTerminationDateConvention ?? lower.FixedLegTerminationDateConvention,
            FixedLegRule = FixedLegRule ?? lower.FixedLegRule,
            FixedLegEndOfMonth = FixedLegEndOfMonth ?? lower.FixedLegEndOfMonth,
            FixedLegDayCounter = FixedLegDayCounter ?? lower.FixedLegDayCounter,
            Index = Index ?? lower.Index,
            IndexTenor = Index is null ? IndexTenor ?? lower.IndexTenor : IndexTenor,
            FloatingLegTenor = FloatingLegTenor ?? lower.FloatingLegTenor,
            FloatingLegCalendar = FloatingLegCalendar ?? lower.FloatingLegCalendar,
            FloatingLegConvention = FloatingLegConvention ?? lower.FloatingLegConvention,
            FloatingLegTerminationDateConvention = FloatingLegTerminationDateConvention ?? lower.FloatingLegTerminationDateConvention,
            FloatingLegRule = FloatingLegRule ?? lower.FloatingLegRule,
            FloatingLegEndOfMonth = FloatingLegEndOfMonth ?? lower.FloatingLegEndOfMonth,
            FloatingLegDayCounter = FloatingLegDayCounter ?? lower.FloatingLegDayCounter,
            PaymentConvention = PaymentConvention ?? lower.PaymentConvention,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        SettlementCalendar,
        FixedLegCalendar,
        FixedLegDayCounter,
        Index,
        FloatingLegCalendar,
        FloatingLegDayCounter,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(SettlementDays), SettlementDays is not null),
            (nameof(SettlementCalendar), SettlementCalendar is not null),
            (nameof(FixedLegTenor), FixedLegTenor is not null),
            (nameof(FixedLegTenorThreshold), FixedLegTenorThreshold is not null),
            (nameof(FixedLegCalendar), FixedLegCalendar is not null),
            (nameof(FixedLegConvention), FixedLegConvention is not null),
            (nameof(FixedLegTerminationDateConvention), FixedLegTerminationDateConvention is not null),
            (nameof(FixedLegRule), FixedLegRule is not null),
            (nameof(FixedLegEndOfMonth), FixedLegEndOfMonth is not null),
            (nameof(FixedLegDayCounter), FixedLegDayCounter is not null),
            (nameof(Index), Index is not null),
            (nameof(IndexTenor), IndexTenor is not null),
            (nameof(FloatingLegTenor), FloatingLegTenor is not null),
            (nameof(FloatingLegCalendar), FloatingLegCalendar is not null),
            (nameof(FloatingLegConvention), FloatingLegConvention is not null),
            (nameof(FloatingLegTerminationDateConvention), FloatingLegTerminationDateConvention is not null),
            (nameof(FloatingLegRule), FloatingLegRule is not null),
            (nameof(FloatingLegEndOfMonth), FloatingLegEndOfMonth is not null),
            (nameof(FloatingLegDayCounter), FloatingLegDayCounter is not null),
            (nameof(PaymentConvention), PaymentConvention is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal SwapConventionRecord Named(string name) =>
        new(name)
        {
            SettlementDays = SettlementDays,
            SettlementCalendar = SettlementCalendar,
            FixedLegTenor = FixedLegTenor,
            FixedLegTenorThreshold = FixedLegTenorThreshold,
            FixedLegCalendar = FixedLegCalendar,
            FixedLegConvention = FixedLegConvention,
            FixedLegTerminationDateConvention = FixedLegTerminationDateConvention,
            FixedLegRule = FixedLegRule,
            FixedLegEndOfMonth = FixedLegEndOfMonth,
            FixedLegDayCounter = FixedLegDayCounter,
            Index = Index,
            IndexTenor = IndexTenor,
            FloatingLegTenor = FloatingLegTenor,
            FloatingLegCalendar = FloatingLegCalendar,
            FloatingLegConvention = FloatingLegConvention,
            FloatingLegTerminationDateConvention = FloatingLegTerminationDateConvention,
            FloatingLegRule = FloatingLegRule,
            FloatingLegEndOfMonth = FloatingLegEndOfMonth,
            FloatingLegDayCounter = FloatingLegDayCounter,
            PaymentConvention = PaymentConvention,
        };
}
