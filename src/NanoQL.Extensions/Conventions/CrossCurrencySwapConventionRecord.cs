// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Time;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A cross-currency fixed-against-floating swap's conventions (<c>CrossCurrencySwapConvention</c>), which feed the
/// constant-notional cross-currency swap helper.
/// </summary>
[Experimental("NQL0001")]
public sealed record CrossCurrencySwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CrossCurrencySwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CrossCurrencySwapConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the fixing days, or <see langword="null"/> to take the base's.</summary>
    public int? FixingDays { get; init; }

    /// <summary>Gets the calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>Gets whether the dates stay at month end, or <see langword="null"/> to take the base's.</summary>
    public bool? EndOfMonth { get; init; }

    /// <summary>Gets the fixed leg's frequency, or <see langword="null"/> to take the base's.</summary>
    public Frequency? FixedFrequency { get; init; }

    /// <summary>Gets the fixed leg's day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? FixedDayCounter { get; init; }

    /// <summary>
    /// Gets the floating leg's Ibor index's name, or an Ibor family's, or <see langword="null"/> to take the base's.
    /// </summary>
    public string? FloatingIndex { get; init; }

    /// <summary>
    /// Gets the tenor of the Ibor family FloatingIndex names, or <see langword="null"/> for none: it comes with
    /// <see cref="FloatingIndex"/>, from the record that gives that; none for an index.
    /// </summary>
    public Period? FloatingIndexTenor { get; init; }

    /// <summary>
    /// Gets whether the fixed leg's currency is the collateral currency, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? CollateralOnFixedLeg { get; init; }

    /// <summary>Gets the payment lag, in business days, or <see langword="null"/> to take the base's.</summary>
    public int? PaymentLag { get; init; }

    /// <summary>
    /// Gets the floating leg's payment frequency, or <see langword="null"/> to take the base's: the index's tenor by
    /// default.
    /// </summary>
    public Frequency? FloatingPaymentFrequency { get; init; }

    /// <summary>
    /// Gets whether the floating coupons are indexed rather than par coupons, or <see langword="null"/> to take the
    /// base's: the context's switch by default.
    /// </summary>
    public bool? IndexedCoupons { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CrossCurrencySwapConventionRecord Over(CrossCurrencySwapConventionRecord lower) =>
        this with
        {
            FixingDays = FixingDays ?? lower.FixingDays,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            EndOfMonth = EndOfMonth ?? lower.EndOfMonth,
            FixedFrequency = FixedFrequency ?? lower.FixedFrequency,
            FixedDayCounter = FixedDayCounter ?? lower.FixedDayCounter,
            FloatingIndex = FloatingIndex ?? lower.FloatingIndex,
            FloatingIndexTenor = FloatingIndex is null ? FloatingIndexTenor ?? lower.FloatingIndexTenor : FloatingIndexTenor,
            CollateralOnFixedLeg = CollateralOnFixedLeg ?? lower.CollateralOnFixedLeg,
            PaymentLag = PaymentLag ?? lower.PaymentLag,
            FloatingPaymentFrequency = FloatingPaymentFrequency ?? lower.FloatingPaymentFrequency,
            IndexedCoupons = IndexedCoupons ?? lower.IndexedCoupons,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Calendar,
        FixedDayCounter,
        FloatingIndex,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(FixingDays), FixingDays is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(EndOfMonth), EndOfMonth is not null),
            (nameof(FixedFrequency), FixedFrequency is not null),
            (nameof(FixedDayCounter), FixedDayCounter is not null),
            (nameof(FloatingIndex), FloatingIndex is not null),
            (nameof(FloatingIndexTenor), FloatingIndexTenor is not null),
            (nameof(CollateralOnFixedLeg), CollateralOnFixedLeg is not null),
            (nameof(PaymentLag), PaymentLag is not null),
            (nameof(FloatingPaymentFrequency), FloatingPaymentFrequency is not null),
            (nameof(IndexedCoupons), IndexedCoupons is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CrossCurrencySwapConventionRecord Named(string name) =>
        new(name)
        {
            FixingDays = FixingDays,
            Calendar = Calendar,
            Convention = Convention,
            EndOfMonth = EndOfMonth,
            FixedFrequency = FixedFrequency,
            FixedDayCounter = FixedDayCounter,
            FloatingIndex = FloatingIndex,
            FloatingIndexTenor = FloatingIndexTenor,
            CollateralOnFixedLeg = CollateralOnFixedLeg,
            PaymentLag = PaymentLag,
            FloatingPaymentFrequency = FloatingPaymentFrequency,
            IndexedCoupons = IndexedCoupons,
        };
}
