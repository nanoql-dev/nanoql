// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A zero-coupon inflation swap's conventions (<c>ZeroCouponInflationSwapConvention</c>), which feed
/// <see cref="NanoQL.Instruments.ZeroCouponInflationSwap"/> and
/// <see cref="NanoQL.TermStructures.ZeroCouponInflationSwapHelper"/>.
/// </summary>
public sealed record ZeroCouponInflationSwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="ZeroCouponInflationSwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public ZeroCouponInflationSwapConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the zero inflation index's name, or <see langword="null"/> to take the base's.</summary>
    public string? Index { get; init; }

    /// <summary>Gets the observation lag, or <see langword="null"/> to take the base's.</summary>
    public Period? ObservationLag { get; init; }

    /// <summary>Gets how the index is observed, or <see langword="null"/> to take the base's.</summary>
    public CPIInterpolationType? Interpolation { get; init; }

    /// <summary>Gets the fixed leg's calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the fixed leg's business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>Gets the day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? DayCounter { get; init; }

    /// <summary>
    /// Gets the inflation leg's calendar's name, or <see langword="null"/> to take the base's: the fixed leg's by
    /// default.
    /// </summary>
    public string? InflationCalendar { get; init; }

    /// <summary>
    /// Gets the inflation leg's business-day convention, or <see langword="null"/> to take the base's: the fixed leg's
    /// by default.
    /// </summary>
    public BusinessDayConvention? InflationConvention { get; init; }

    /// <summary>
    /// Gets whether the observation dates are adjusted, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? AdjustObservationDates { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal ZeroCouponInflationSwapConventionRecord Over(ZeroCouponInflationSwapConventionRecord lower) =>
        this with
        {
            Index = Index ?? lower.Index,
            ObservationLag = ObservationLag ?? lower.ObservationLag,
            Interpolation = Interpolation ?? lower.Interpolation,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            DayCounter = DayCounter ?? lower.DayCounter,
            InflationCalendar = InflationCalendar ?? lower.InflationCalendar,
            InflationConvention = InflationConvention ?? lower.InflationConvention,
            AdjustObservationDates = AdjustObservationDates ?? lower.AdjustObservationDates,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Index,
        Calendar,
        DayCounter,
        InflationCalendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Index), Index is not null),
            (nameof(ObservationLag), ObservationLag is not null),
            (nameof(Interpolation), Interpolation is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(DayCounter), DayCounter is not null),
            (nameof(InflationCalendar), InflationCalendar is not null),
            (nameof(InflationConvention), InflationConvention is not null),
            (nameof(AdjustObservationDates), AdjustObservationDates is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal ZeroCouponInflationSwapConventionRecord Named(string name) =>
        new(name)
        {
            Index = Index,
            ObservationLag = ObservationLag,
            Interpolation = Interpolation,
            Calendar = Calendar,
            Convention = Convention,
            DayCounter = DayCounter,
            InflationCalendar = InflationCalendar,
            InflationConvention = InflationConvention,
            AdjustObservationDates = AdjustObservationDates,
        };
}
