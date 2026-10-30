// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Time;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A cross-currency basis swap's conventions (<c>CrossCurrencyBasisSwapConvention</c>), which feed the
/// constant-notional or, marked to market, the mark-to-market basis swap helper.
/// </summary>
[Experimental("NQL0001")]
public sealed record CrossCurrencyBasisSwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CrossCurrencyBasisSwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CrossCurrencyBasisSwapConventionRecord(string name, string? @base = null)
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

    /// <summary>
    /// Gets the FX base currency leg's Ibor index's name, or an Ibor family's, or <see langword="null"/> to take the
    /// base's.
    /// </summary>
    public string? BaseCurrencyIndex { get; init; }

    /// <summary>
    /// Gets the tenor of the Ibor family BaseCurrencyIndex names, or <see langword="null"/> for none: it comes with
    /// <see cref="BaseCurrencyIndex"/>, from the record that gives that; none for an index.
    /// </summary>
    public Period? BaseCurrencyIndexTenor { get; init; }

    /// <summary>
    /// Gets the FX quote currency leg's Ibor index's name, or an Ibor family's, or <see langword="null"/> to take the
    /// base's.
    /// </summary>
    public string? QuoteCurrencyIndex { get; init; }

    /// <summary>
    /// Gets the tenor of the Ibor family QuoteCurrencyIndex names, or <see langword="null"/> for none: it comes with
    /// <see cref="QuoteCurrencyIndex"/>, from the record that gives that; none for an index.
    /// </summary>
    public Period? QuoteCurrencyIndexTenor { get; init; }

    /// <summary>
    /// Gets whether the FX base currency is the collateral currency, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? CollateralOnBaseCurrency { get; init; }

    /// <summary>
    /// Gets whether the basis is on the FX base currency leg, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? BasisOnBaseCurrencyLeg { get; init; }

    /// <summary>
    /// Gets the base currency leg's payment frequency, and the quote currency leg's unless
    /// QuoteCurrencyPaymentFrequency is given, or <see langword="null"/> to take the base's: the index's tenor by
    /// default.
    /// </summary>
    public Frequency? PaymentFrequency { get; init; }

    /// <summary>
    /// Gets the quote currency leg's payment frequency, or <see langword="null"/> to take the base's.
    /// </summary>
    public Frequency? QuoteCurrencyPaymentFrequency { get; init; }

    /// <summary>Gets the payment lag, in business days, or <see langword="null"/> to take the base's.</summary>
    public int? PaymentLag { get; init; }

    /// <summary>
    /// Gets whether the floating coupons are indexed rather than par coupons, or <see langword="null"/> to take the
    /// base's: the context's switch by default.
    /// </summary>
    public bool? IndexedCoupons { get; init; }

    /// <summary>
    /// Gets whether the payment lag applies to the notional exchanges, or <see langword="null"/> to take the base's:
    /// constant-notional swaps only.
    /// </summary>
    public bool? PaymentLagOnNotionalExchanges { get; init; }

    /// <summary>Gets whether the swap is marked to market, or <see langword="null"/> to take the base's.</summary>
    public bool? MarkToMarket { get; init; }

    /// <summary>
    /// Gets whether the FX base currency leg's notional resets, or <see langword="null"/> to take the base's: marked to
    /// market only.
    /// </summary>
    public bool? BaseCurrencyLegResettable { get; init; }

    /// <summary>
    /// Gets the FX reset's fixing days, or <see langword="null"/> to take the base's: marked to market only.
    /// </summary>
    public int? FxResetFixingDays { get; init; }

    /// <summary>
    /// Gets the FX reset's fixing calendar's name, or <see langword="null"/> to take the base's: marked to market only.
    /// </summary>
    public string? FxResetFixingCalendar { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CrossCurrencyBasisSwapConventionRecord Over(CrossCurrencyBasisSwapConventionRecord lower) =>
        this with
        {
            FixingDays = FixingDays ?? lower.FixingDays,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            EndOfMonth = EndOfMonth ?? lower.EndOfMonth,
            BaseCurrencyIndex = BaseCurrencyIndex ?? lower.BaseCurrencyIndex,
            BaseCurrencyIndexTenor = BaseCurrencyIndex is null ? BaseCurrencyIndexTenor ?? lower.BaseCurrencyIndexTenor : BaseCurrencyIndexTenor,
            QuoteCurrencyIndex = QuoteCurrencyIndex ?? lower.QuoteCurrencyIndex,
            QuoteCurrencyIndexTenor = QuoteCurrencyIndex is null ? QuoteCurrencyIndexTenor ?? lower.QuoteCurrencyIndexTenor : QuoteCurrencyIndexTenor,
            CollateralOnBaseCurrency = CollateralOnBaseCurrency ?? lower.CollateralOnBaseCurrency,
            BasisOnBaseCurrencyLeg = BasisOnBaseCurrencyLeg ?? lower.BasisOnBaseCurrencyLeg,
            PaymentFrequency = PaymentFrequency ?? lower.PaymentFrequency,
            QuoteCurrencyPaymentFrequency = QuoteCurrencyPaymentFrequency ?? lower.QuoteCurrencyPaymentFrequency,
            PaymentLag = PaymentLag ?? lower.PaymentLag,
            IndexedCoupons = IndexedCoupons ?? lower.IndexedCoupons,
            PaymentLagOnNotionalExchanges = PaymentLagOnNotionalExchanges ?? lower.PaymentLagOnNotionalExchanges,
            MarkToMarket = MarkToMarket ?? lower.MarkToMarket,
            BaseCurrencyLegResettable = BaseCurrencyLegResettable ?? lower.BaseCurrencyLegResettable,
            FxResetFixingDays = FxResetFixingDays ?? lower.FxResetFixingDays,
            FxResetFixingCalendar = FxResetFixingCalendar ?? lower.FxResetFixingCalendar,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Calendar,
        BaseCurrencyIndex,
        QuoteCurrencyIndex,
        FxResetFixingCalendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(FixingDays), FixingDays is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(EndOfMonth), EndOfMonth is not null),
            (nameof(BaseCurrencyIndex), BaseCurrencyIndex is not null),
            (nameof(BaseCurrencyIndexTenor), BaseCurrencyIndexTenor is not null),
            (nameof(QuoteCurrencyIndex), QuoteCurrencyIndex is not null),
            (nameof(QuoteCurrencyIndexTenor), QuoteCurrencyIndexTenor is not null),
            (nameof(CollateralOnBaseCurrency), CollateralOnBaseCurrency is not null),
            (nameof(BasisOnBaseCurrencyLeg), BasisOnBaseCurrencyLeg is not null),
            (nameof(PaymentFrequency), PaymentFrequency is not null),
            (nameof(QuoteCurrencyPaymentFrequency), QuoteCurrencyPaymentFrequency is not null),
            (nameof(PaymentLag), PaymentLag is not null),
            (nameof(IndexedCoupons), IndexedCoupons is not null),
            (nameof(PaymentLagOnNotionalExchanges), PaymentLagOnNotionalExchanges is not null),
            (nameof(MarkToMarket), MarkToMarket is not null),
            (nameof(BaseCurrencyLegResettable), BaseCurrencyLegResettable is not null),
            (nameof(FxResetFixingDays), FxResetFixingDays is not null),
            (nameof(FxResetFixingCalendar), FxResetFixingCalendar is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CrossCurrencyBasisSwapConventionRecord Named(string name) =>
        new(name)
        {
            FixingDays = FixingDays,
            Calendar = Calendar,
            Convention = Convention,
            EndOfMonth = EndOfMonth,
            BaseCurrencyIndex = BaseCurrencyIndex,
            BaseCurrencyIndexTenor = BaseCurrencyIndexTenor,
            QuoteCurrencyIndex = QuoteCurrencyIndex,
            QuoteCurrencyIndexTenor = QuoteCurrencyIndexTenor,
            CollateralOnBaseCurrency = CollateralOnBaseCurrency,
            BasisOnBaseCurrencyLeg = BasisOnBaseCurrencyLeg,
            PaymentFrequency = PaymentFrequency,
            QuoteCurrencyPaymentFrequency = QuoteCurrencyPaymentFrequency,
            PaymentLag = PaymentLag,
            IndexedCoupons = IndexedCoupons,
            PaymentLagOnNotionalExchanges = PaymentLagOnNotionalExchanges,
            MarkToMarket = MarkToMarket,
            BaseCurrencyLegResettable = BaseCurrencyLegResettable,
            FxResetFixingDays = FxResetFixingDays,
            FxResetFixingCalendar = FxResetFixingCalendar,
        };
}
