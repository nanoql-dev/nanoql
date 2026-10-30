// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A year-on-year inflation cap's or floor's conventions (<c>YoYInflationCapFloorConvention</c>), which feed
/// <see cref="NanoQL.Instruments.MakeYoYInflationCapFloor"/>; or, named <c>MakeYoYInflationCapFloor</c>, the builder's
/// defaults.
/// </summary>
public sealed record YoYInflationCapFloorConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationCapFloorConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public YoYInflationCapFloorConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the year-on-year inflation index's name, or <see langword="null"/> to take the base's: none in the default
    /// record.
    /// </summary>
    public string? Index { get; init; }

    /// <summary>
    /// Gets the observation lag, or <see langword="null"/> to take the base's: none in the default record.
    /// </summary>
    public Period? ObservationLag { get; init; }

    /// <summary>
    /// Gets how the index is observed, or <see langword="null"/> to take the base's: none in the default record.
    /// </summary>
    public CPIInterpolationType? Interpolation { get; init; }

    /// <summary>
    /// Gets the calendar's name, or <see langword="null"/> to take the base's: none in the default record.
    /// </summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the coupons' payment day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? PaymentDayCounter { get; init; }

    /// <summary>
    /// Gets the payment dates' business-day convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? PaymentAdjustment { get; init; }

    /// <summary>
    /// Gets the days from the evaluation date to the start, or <see langword="null"/> to take the base's.
    /// </summary>
    public int? FixingDays { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal YoYInflationCapFloorConventionRecord Over(YoYInflationCapFloorConventionRecord lower) =>
        this with
        {
            Index = Index ?? lower.Index,
            ObservationLag = ObservationLag ?? lower.ObservationLag,
            Interpolation = Interpolation ?? lower.Interpolation,
            Calendar = Calendar ?? lower.Calendar,
            PaymentDayCounter = PaymentDayCounter ?? lower.PaymentDayCounter,
            PaymentAdjustment = PaymentAdjustment ?? lower.PaymentAdjustment,
            FixingDays = FixingDays ?? lower.FixingDays,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Index,
        Calendar,
        PaymentDayCounter,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Index), Index is not null),
            (nameof(ObservationLag), ObservationLag is not null),
            (nameof(Interpolation), Interpolation is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(PaymentDayCounter), PaymentDayCounter is not null),
            (nameof(PaymentAdjustment), PaymentAdjustment is not null),
            (nameof(FixingDays), FixingDays is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal YoYInflationCapFloorConventionRecord Named(string name) =>
        new(name)
        {
            Index = Index,
            ObservationLag = ObservationLag,
            Interpolation = Interpolation,
            Calendar = Calendar,
            PaymentDayCounter = PaymentDayCounter,
            PaymentAdjustment = PaymentAdjustment,
            FixingDays = FixingDays,
        };
}
