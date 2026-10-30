// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A government bond market's conventions (<c>BondConvention</c>), which feed
/// <see cref="NanoQL.Instruments.FixedRateBond"/> and <see cref="NanoQL.TermStructures.FixedRateBondHelper"/>; or,
/// named <c>FixedRateBond</c>, the defaults a set's bonds take; or, named <c>BTP</c>, the preset's conventions.
/// </summary>
public sealed record BondConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="BondConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public BondConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the settlement days, or <see langword="null"/> to take the base's.</summary>
    public int? SettlementDays { get; init; }

    /// <summary>Gets the schedule's frequency, or <see langword="null"/> to take the base's.</summary>
    public Frequency? Frequency { get; init; }

    /// <summary>Gets the schedule's calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? Calendar { get; init; }

    /// <summary>Gets the schedule's business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>
    /// Gets the schedule's termination-date convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? TerminationDateConvention { get; init; }

    /// <summary>Gets the schedule's date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? Rule { get; init; }

    /// <summary>
    /// Gets whether the schedule's dates stay at month end, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? EndOfMonth { get; init; }

    /// <summary>Gets the coupons' accrual day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? AccrualDayCounter { get; init; }

    /// <summary>
    /// Gets the payment dates' business-day convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? PaymentConvention { get; init; }

    /// <summary>
    /// Gets the payment calendar's name, or <see langword="null"/> to take the base's: the schedule's by default.
    /// </summary>
    public string? PaymentCalendar { get; init; }

    /// <summary>Gets the redemption, per 100 of face, or <see langword="null"/> to take the base's.</summary>
    public double? Redemption { get; init; }

    /// <summary>Gets the ex-coupon period, or <see langword="null"/> to take the base's.</summary>
    public Period? ExCouponPeriod { get; init; }

    /// <summary>Gets the ex-coupon period's calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? ExCouponCalendar { get; init; }

    /// <summary>
    /// Gets the ex-coupon dates' business-day convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? ExCouponConvention { get; init; }

    /// <summary>
    /// Gets whether the ex-coupon dates stay at month end, or <see langword="null"/> to take the base's.
    /// </summary>
    public bool? ExCouponEndOfMonth { get; init; }

    /// <summary>
    /// Gets whether the helper's price is clean or dirty, or <see langword="null"/> to take the base's.
    /// </summary>
    public BondPriceType? PriceType { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal BondConventionRecord Over(BondConventionRecord lower) =>
        this with
        {
            SettlementDays = SettlementDays ?? lower.SettlementDays,
            Frequency = Frequency ?? lower.Frequency,
            Calendar = Calendar ?? lower.Calendar,
            Convention = Convention ?? lower.Convention,
            TerminationDateConvention = TerminationDateConvention ?? lower.TerminationDateConvention,
            Rule = Rule ?? lower.Rule,
            EndOfMonth = EndOfMonth ?? lower.EndOfMonth,
            AccrualDayCounter = AccrualDayCounter ?? lower.AccrualDayCounter,
            PaymentConvention = PaymentConvention ?? lower.PaymentConvention,
            PaymentCalendar = PaymentCalendar ?? lower.PaymentCalendar,
            Redemption = Redemption ?? lower.Redemption,
            ExCouponPeriod = ExCouponPeriod ?? lower.ExCouponPeriod,
            ExCouponCalendar = ExCouponCalendar ?? lower.ExCouponCalendar,
            ExCouponConvention = ExCouponConvention ?? lower.ExCouponConvention,
            ExCouponEndOfMonth = ExCouponEndOfMonth ?? lower.ExCouponEndOfMonth,
            PriceType = PriceType ?? lower.PriceType,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Calendar,
        AccrualDayCounter,
        PaymentCalendar,
        ExCouponCalendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(SettlementDays), SettlementDays is not null),
            (nameof(Frequency), Frequency is not null),
            (nameof(Calendar), Calendar is not null),
            (nameof(Convention), Convention is not null),
            (nameof(TerminationDateConvention), TerminationDateConvention is not null),
            (nameof(Rule), Rule is not null),
            (nameof(EndOfMonth), EndOfMonth is not null),
            (nameof(AccrualDayCounter), AccrualDayCounter is not null),
            (nameof(PaymentConvention), PaymentConvention is not null),
            (nameof(PaymentCalendar), PaymentCalendar is not null),
            (nameof(Redemption), Redemption is not null),
            (nameof(ExCouponPeriod), ExCouponPeriod is not null),
            (nameof(ExCouponCalendar), ExCouponCalendar is not null),
            (nameof(ExCouponConvention), ExCouponConvention is not null),
            (nameof(ExCouponEndOfMonth), ExCouponEndOfMonth is not null),
            (nameof(PriceType), PriceType is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal BondConventionRecord Named(string name) =>
        new(name)
        {
            SettlementDays = SettlementDays,
            Frequency = Frequency,
            Calendar = Calendar,
            Convention = Convention,
            TerminationDateConvention = TerminationDateConvention,
            Rule = Rule,
            EndOfMonth = EndOfMonth,
            AccrualDayCounter = AccrualDayCounter,
            PaymentConvention = PaymentConvention,
            PaymentCalendar = PaymentCalendar,
            Redemption = Redemption,
            ExCouponPeriod = ExCouponPeriod,
            ExCouponCalendar = ExCouponCalendar,
            ExCouponConvention = ExCouponConvention,
            ExCouponEndOfMonth = ExCouponEndOfMonth,
            PriceType = PriceType,
        };
}
