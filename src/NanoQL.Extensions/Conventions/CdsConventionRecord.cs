// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A credit default swap's conventions (<c>CdsConvention</c>), which feed
/// <see cref="NanoQL.Instruments.MakeCreditDefaultSwap"/>, <see cref="NanoQL.TermStructures.SpreadCdsHelper"/> and
/// <see cref="NanoQL.TermStructures.UpfrontCdsHelper"/>; or, named <c>MakeCreditDefaultSwap</c>, the builder's
/// defaults.
/// </summary>
public sealed record CdsConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="CdsConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public CdsConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the helpers' settlement days, or <see langword="null"/> to take the base's.</summary>
    public int? SettlementDays { get; init; }

    /// <summary>Gets the calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the coupon frequency, or <see langword="null"/> to take the base's.</summary>
    public Frequency? CouponFrequency { get; init; }

    /// <summary>
    /// Gets the business-day convention of the schedule and the payments, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? PaymentConvention { get; init; }

    /// <summary>Gets the date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? Rule { get; init; }

    /// <summary>Gets the accrual's day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? DayCounter { get; init; }

    /// <summary>
    /// Gets the last period's accrual day counter's name, or <see langword="null"/> to take the base's.
    /// </summary>
    public string? LastPeriodDayCounter { get; init; }

    /// <summary>
    /// Gets whether the accrued coupon is due on a default, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? SettlesAccrual { get; init; }

    /// <summary>
    /// Gets whether the payments a default triggers are due at the default time, or <see langword="null"/> to take the
    /// base's.
    /// </summary>
    public bool? PaysAtDefaultTime { get; init; }

    /// <summary>
    /// Gets whether the seller pays the accrued current coupon at the cash settlement date, or <see langword="null"/>
    /// to take the base's.
    /// </summary>
    public bool? RebatesAccrual { get; init; }

    /// <summary>
    /// Gets the business days from the trade date to the cash settlement date, or <see langword="null"/> to take the
    /// base's.
    /// </summary>
    public int? CashSettlementDays { get; init; }

    /// <summary>Gets the model the helpers price with, or <see langword="null"/> to take the base's.</summary>
    public CreditDefaultSwapPricingModel? PricingModel { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal CdsConventionRecord Over(CdsConventionRecord lower) =>
        this with
        {
            SettlementDays = SettlementDays ?? lower.SettlementDays,
            Calendar = Calendar ?? lower.Calendar,
            CouponFrequency = CouponFrequency ?? lower.CouponFrequency,
            PaymentConvention = PaymentConvention ?? lower.PaymentConvention,
            Rule = Rule ?? lower.Rule,
            DayCounter = DayCounter ?? lower.DayCounter,
            LastPeriodDayCounter = LastPeriodDayCounter ?? lower.LastPeriodDayCounter,
            SettlesAccrual = SettlesAccrual ?? lower.SettlesAccrual,
            PaysAtDefaultTime = PaysAtDefaultTime ?? lower.PaysAtDefaultTime,
            RebatesAccrual = RebatesAccrual ?? lower.RebatesAccrual,
            CashSettlementDays = CashSettlementDays ?? lower.CashSettlementDays,
            PricingModel = PricingModel ?? lower.PricingModel,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Calendar,
        DayCounter,
        LastPeriodDayCounter,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(SettlementDays), SettlementDays is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(CouponFrequency), CouponFrequency is not null),
            (nameof(PaymentConvention), PaymentConvention is not null),
            (nameof(Rule), Rule is not null),
            (nameof(DayCounter), DayCounter is not null),
            (nameof(LastPeriodDayCounter), LastPeriodDayCounter is not null),
            (nameof(SettlesAccrual), SettlesAccrual is not null),
            (nameof(PaysAtDefaultTime), PaysAtDefaultTime is not null),
            (nameof(RebatesAccrual), RebatesAccrual is not null),
            (nameof(CashSettlementDays), CashSettlementDays is not null),
            (nameof(PricingModel), PricingModel is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal CdsConventionRecord Named(string name) =>
        new(name)
        {
            SettlementDays = SettlementDays,
            Calendar = Calendar,
            CouponFrequency = CouponFrequency,
            PaymentConvention = PaymentConvention,
            Rule = Rule,
            DayCounter = DayCounter,
            LastPeriodDayCounter = LastPeriodDayCounter,
            SettlesAccrual = SettlesAccrual,
            PaysAtDefaultTime = PaysAtDefaultTime,
            RebatesAccrual = RebatesAccrual,
            CashSettlementDays = CashSettlementDays,
            PricingModel = PricingModel,
        };
}
