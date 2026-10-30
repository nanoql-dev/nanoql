// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A multiple-resets swap's conventions (<c>MultipleResetsSwapConvention</c>), which feed
/// <see cref="NanoQL.Instruments.MakeMultipleResetsSwap"/>; or, named <c>MakeMultipleResetsSwap</c>, the builder's
/// defaults.
/// </summary>
public sealed record MultipleResetsSwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="MultipleResetsSwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public MultipleResetsSwapConventionRecord(string name, string? @base = null)
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
    /// Gets the resets per floating coupon, or <see langword="null"/> to take the base's: none in the default record.
    /// </summary>
    public int? ResetsPerCoupon { get; init; }

    /// <summary>
    /// Gets the settlement days, or <see langword="null"/> to take the base's: the index's fixing days by default.
    /// </summary>
    public int? SettlementDays { get; init; }

    /// <summary>
    /// Gets the fixed leg's frequency, or <see langword="null"/> to take the base's: the coupons' resets times the
    /// index's tenor by default.
    /// </summary>
    public Frequency? FixedLegFrequency { get; init; }

    /// <summary>
    /// Gets the fixed leg's day counter's name, or <see langword="null"/> to take the base's: the index's by default.
    /// </summary>
    public string? FixedLegDayCounter { get; init; }

    /// <summary>Gets the fixed leg's business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? FixedLegConvention { get; init; }

    /// <summary>
    /// Gets whether a coupon's resets are compounded or averaged, or <see langword="null"/> to take the base's.
    /// </summary>
    public RateAveragingType? Averaging { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal MultipleResetsSwapConventionRecord Over(MultipleResetsSwapConventionRecord lower) =>
        this with
        {
            Index = Index ?? lower.Index,
            IndexTenor = Index is null ? IndexTenor ?? lower.IndexTenor : IndexTenor,
            ResetsPerCoupon = ResetsPerCoupon ?? lower.ResetsPerCoupon,
            SettlementDays = SettlementDays ?? lower.SettlementDays,
            FixedLegFrequency = FixedLegFrequency ?? lower.FixedLegFrequency,
            FixedLegDayCounter = FixedLegDayCounter ?? lower.FixedLegDayCounter,
            FixedLegConvention = FixedLegConvention ?? lower.FixedLegConvention,
            Averaging = Averaging ?? lower.Averaging,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Index,
        FixedLegDayCounter,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Index), Index is not null),
            (nameof(IndexTenor), IndexTenor is not null),
            (nameof(ResetsPerCoupon), ResetsPerCoupon is not null),
            (nameof(SettlementDays), SettlementDays is not null),
            (nameof(FixedLegFrequency), FixedLegFrequency is not null),
            (nameof(FixedLegDayCounter), FixedLegDayCounter is not null),
            (nameof(FixedLegConvention), FixedLegConvention is not null),
            (nameof(Averaging), Averaging is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal MultipleResetsSwapConventionRecord Named(string name) =>
        new(name)
        {
            Index = Index,
            IndexTenor = IndexTenor,
            ResetsPerCoupon = ResetsPerCoupon,
            SettlementDays = SettlementDays,
            FixedLegFrequency = FixedLegFrequency,
            FixedLegDayCounter = FixedLegDayCounter,
            FixedLegConvention = FixedLegConvention,
            Averaging = Averaging,
        };
}
