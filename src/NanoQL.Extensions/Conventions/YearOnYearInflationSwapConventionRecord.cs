// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A year-on-year inflation swap's conventions (<c>YearOnYearInflationSwapConvention</c>), which feed
/// <see cref="NanoQL.TermStructures.YearOnYearInflationSwapHelper"/>.
/// </summary>
public sealed record YearOnYearInflationSwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="YearOnYearInflationSwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public YearOnYearInflationSwapConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the year-on-year inflation index's name, or <see langword="null"/> to take the base's.</summary>
    public string? Index { get; init; }

    /// <summary>Gets the observation lag, or <see langword="null"/> to take the base's.</summary>
    public Period? ObservationLag { get; init; }

    /// <summary>Gets how the index is observed, or <see langword="null"/> to take the base's.</summary>
    public CPIInterpolationType? Interpolation { get; init; }

    /// <summary>Gets the calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the payment convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? PaymentConvention { get; init; }

    /// <summary>Gets the day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? DayCounter { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal YearOnYearInflationSwapConventionRecord Over(YearOnYearInflationSwapConventionRecord lower) =>
        this with
        {
            Index = Index ?? lower.Index,
            ObservationLag = ObservationLag ?? lower.ObservationLag,
            Interpolation = Interpolation ?? lower.Interpolation,
            Calendar = Calendar ?? lower.Calendar,
            PaymentConvention = PaymentConvention ?? lower.PaymentConvention,
            DayCounter = DayCounter ?? lower.DayCounter,
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
            (nameof(ObservationLag), ObservationLag is not null),
            (nameof(Interpolation), Interpolation is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(PaymentConvention), PaymentConvention is not null),
            (nameof(DayCounter), DayCounter is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal YearOnYearInflationSwapConventionRecord Named(string name) =>
        new(name)
        {
            Index = Index,
            ObservationLag = ObservationLag,
            Interpolation = Interpolation,
            Calendar = Calendar,
            PaymentConvention = PaymentConvention,
            DayCounter = DayCounter,
        };
}
