// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An FX swap's and forward's conventions (<c>FxSwapConvention</c>), which feed
/// <see cref="NanoQL.TermStructures.FxSwapRateHelper"/> and <see cref="NanoQL.Instruments.FxForward"/>.
/// </summary>
public sealed record FxSwapConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="FxSwapConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public FxSwapConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the base currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? BaseCurrency { get; init; }

    /// <summary>Gets the quote currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? QuoteCurrency { get; init; }

    /// <summary>Gets the spot days, or <see langword="null"/> to take the base's.</summary>
    public int? FixingDays { get; init; }

    /// <summary>Gets the calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>Gets whether the dates stay at month end, or <see langword="null"/> to take the base's.</summary>
    public bool? EndOfMonth { get; init; }

    /// <summary>
    /// Gets whether the base currency is the collateral currency, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? CollateralOnBaseCurrency { get; init; }

    /// <summary>Gets the trading calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? TradingCalendar { get; init; }

    /// <summary>Gets a forward's payment calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? PaymentCalendar { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal FxSwapConventionRecord Over(FxSwapConventionRecord lower) =>
        this with
        {
            BaseCurrency = BaseCurrency ?? lower.BaseCurrency,
            QuoteCurrency = QuoteCurrency ?? lower.QuoteCurrency,
            FixingDays = FixingDays ?? lower.FixingDays,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            EndOfMonth = EndOfMonth ?? lower.EndOfMonth,
            CollateralOnBaseCurrency = CollateralOnBaseCurrency ?? lower.CollateralOnBaseCurrency,
            TradingCalendar = TradingCalendar ?? lower.TradingCalendar,
            PaymentCalendar = PaymentCalendar ?? lower.PaymentCalendar,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        BaseCurrency,
        QuoteCurrency,
        Calendar,
        TradingCalendar,
        PaymentCalendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(BaseCurrency), BaseCurrency is not null),
            (nameof(QuoteCurrency), QuoteCurrency is not null),
            (nameof(FixingDays), FixingDays is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(EndOfMonth), EndOfMonth is not null),
            (nameof(CollateralOnBaseCurrency), CollateralOnBaseCurrency is not null),
            (nameof(TradingCalendar), TradingCalendar is not null),
            (nameof(PaymentCalendar), PaymentCalendar is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal FxSwapConventionRecord Named(string name) =>
        new(name)
        {
            BaseCurrency = BaseCurrency,
            QuoteCurrency = QuoteCurrency,
            FixingDays = FixingDays,
            Calendar = Calendar,
            Convention = Convention,
            EndOfMonth = EndOfMonth,
            CollateralOnBaseCurrency = CollateralOnBaseCurrency,
            TradingCalendar = TradingCalendar,
            PaymentCalendar = PaymentCalendar,
        };
}
