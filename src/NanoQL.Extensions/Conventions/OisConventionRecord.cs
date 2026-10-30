// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An overnight-indexed swap's conventions (<c>OisConvention</c>), which feed <see cref="NanoQL.Instruments.MakeOIS"/>
/// and <see cref="NanoQL.TermStructures.OISRateHelper"/>; or, named <c>MakeOIS</c>, or <c>MakeOIS.</c> and an index's
/// class name, the builder's defaults.
/// </summary>
public sealed record OisConventionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="OisConventionRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public OisConventionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the overnight index's name, or <see langword="null"/> to take the base's: none in a default record.
    /// </summary>
    public string? Index { get; init; }

    /// <summary>Gets the settlement days, or <see langword="null"/> to take the base's.</summary>
    public int? SettlementDays { get; init; }

    /// <summary>
    /// Gets the calendar the settlement days are counted on, or <see langword="null"/> to take the base's: the
    /// overnight leg's by default.
    /// </summary>
    public string? SettlementCalendar { get; init; }

    /// <summary>Gets the payment lag, in business days, or <see langword="null"/> to take the base's.</summary>
    public int? PaymentLag { get; init; }

    /// <summary>
    /// Gets the payment dates' business-day convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? PaymentConvention { get; init; }

    /// <summary>
    /// Gets both legs' payment frequency, the fixed leg's unless FixedLegFrequency is given, or <see langword="null"/>
    /// to take the base's.
    /// </summary>
    public Frequency? PaymentFrequency { get; init; }

    /// <summary>
    /// Gets the payment calendar's name, or <see langword="null"/> to take the base's: each leg's schedule calendar by
    /// default.
    /// </summary>
    public string? PaymentCalendar { get; init; }

    /// <summary>Gets the fixed leg's payment frequency, or <see langword="null"/> to take the base's.</summary>
    public Frequency? FixedLegFrequency { get; init; }

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
    /// Gets whether the fixed leg's dates stay at month end, or <see langword="null"/> to take the base's: by default,
    /// with neither leg's given, whether the start date is at month end.
    /// </summary>
    public bool? FixedLegEndOfMonth { get; init; }

    /// <summary>
    /// Gets the fixed leg's day counter's name, or <see langword="null"/> to take the base's: the index's by default.
    /// </summary>
    public string? FixedLegDayCounter { get; init; }

    /// <summary>
    /// Gets the overnight leg's calendar's name, or <see langword="null"/> to take the base's: the index's fixing
    /// calendar by default.
    /// </summary>
    public string? OvernightLegCalendar { get; init; }

    /// <summary>
    /// Gets the overnight leg's business-day convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? OvernightLegConvention { get; init; }

    /// <summary>
    /// Gets the overnight leg's termination-date convention, or <see langword="null"/> to take the base's.
    /// </summary>
    public BusinessDayConvention? OvernightLegTerminationDateConvention { get; init; }

    /// <summary>Gets the overnight leg's date-generation rule, or <see langword="null"/> to take the base's.</summary>
    public DateGenerationRule? OvernightLegRule { get; init; }

    /// <summary>
    /// Gets whether the overnight leg's dates and the maturity stay at month end, or <see langword="null"/> to take the
    /// base's: by default, with neither leg's given, whether the start date is at month end.
    /// </summary>
    public bool? OvernightLegEndOfMonth { get; init; }

    /// <summary>Gets whether the value dates are telescopic, or <see langword="null"/> to take the base's.</summary>
    public bool? TelescopicValueDates { get; init; }

    /// <summary>
    /// Gets whether the overnight rates are compounded or averaged, or <see langword="null"/> to take the base's.
    /// </summary>
    public RateAveragingType? Averaging { get; init; }

    /// <summary>
    /// Gets the lookback days, or <see langword="null"/> to take the base's: none by default, the coupons fixing by the
    /// index's fixing days.
    /// </summary>
    public int? LookbackDays { get; init; }

    /// <summary>Gets the lockout days, or <see langword="null"/> to take the base's.</summary>
    public int? LockoutDays { get; init; }

    /// <summary>Gets whether the observation shift applies, or <see langword="null"/> to take the base's.</summary>
    public bool? ObservationShift { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal OisConventionRecord Over(OisConventionRecord lower) =>
        this with
        {
            Index = Index ?? lower.Index,
            SettlementDays = SettlementDays ?? lower.SettlementDays,
            SettlementCalendar = SettlementCalendar ?? lower.SettlementCalendar,
            PaymentLag = PaymentLag ?? lower.PaymentLag,
            PaymentConvention = PaymentConvention ?? lower.PaymentConvention,
            PaymentFrequency = PaymentFrequency ?? lower.PaymentFrequency,
            PaymentCalendar = PaymentCalendar ?? lower.PaymentCalendar,
            FixedLegFrequency = FixedLegFrequency ?? lower.FixedLegFrequency,
            FixedLegCalendar = FixedLegCalendar ?? lower.FixedLegCalendar,
            FixedLegConvention = FixedLegConvention ?? lower.FixedLegConvention,
            FixedLegTerminationDateConvention = FixedLegTerminationDateConvention ?? lower.FixedLegTerminationDateConvention,
            FixedLegRule = FixedLegRule ?? lower.FixedLegRule,
            FixedLegEndOfMonth = FixedLegEndOfMonth ?? lower.FixedLegEndOfMonth,
            FixedLegDayCounter = FixedLegDayCounter ?? lower.FixedLegDayCounter,
            OvernightLegCalendar = OvernightLegCalendar ?? lower.OvernightLegCalendar,
            OvernightLegConvention = OvernightLegConvention ?? lower.OvernightLegConvention,
            OvernightLegTerminationDateConvention = OvernightLegTerminationDateConvention ?? lower.OvernightLegTerminationDateConvention,
            OvernightLegRule = OvernightLegRule ?? lower.OvernightLegRule,
            OvernightLegEndOfMonth = OvernightLegEndOfMonth ?? lower.OvernightLegEndOfMonth,
            TelescopicValueDates = TelescopicValueDates ?? lower.TelescopicValueDates,
            Averaging = Averaging ?? lower.Averaging,
            LookbackDays = LookbackDays ?? lower.LookbackDays,
            LockoutDays = LockoutDays ?? lower.LockoutDays,
            ObservationShift = ObservationShift ?? lower.ObservationShift,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Index,
        SettlementCalendar,
        PaymentCalendar,
        FixedLegCalendar,
        FixedLegDayCounter,
        OvernightLegCalendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(Index), Index is not null),
            (nameof(SettlementDays), SettlementDays is not null),
            (nameof(SettlementCalendar), SettlementCalendar is not null),
            (nameof(PaymentLag), PaymentLag is not null),
            (nameof(PaymentConvention), PaymentConvention is not null),
            (nameof(PaymentFrequency), PaymentFrequency is not null),
            (nameof(PaymentCalendar), PaymentCalendar is not null),
            (nameof(FixedLegFrequency), FixedLegFrequency is not null),
            (nameof(FixedLegCalendar), FixedLegCalendar is not null),
            (nameof(FixedLegConvention), FixedLegConvention is not null),
            (nameof(FixedLegTerminationDateConvention), FixedLegTerminationDateConvention is not null),
            (nameof(FixedLegRule), FixedLegRule is not null),
            (nameof(FixedLegEndOfMonth), FixedLegEndOfMonth is not null),
            (nameof(FixedLegDayCounter), FixedLegDayCounter is not null),
            (nameof(OvernightLegCalendar), OvernightLegCalendar is not null),
            (nameof(OvernightLegConvention), OvernightLegConvention is not null),
            (nameof(OvernightLegTerminationDateConvention), OvernightLegTerminationDateConvention is not null),
            (nameof(OvernightLegRule), OvernightLegRule is not null),
            (nameof(OvernightLegEndOfMonth), OvernightLegEndOfMonth is not null),
            (nameof(TelescopicValueDates), TelescopicValueDates is not null),
            (nameof(Averaging), Averaging is not null),
            (nameof(LookbackDays), LookbackDays is not null),
            (nameof(LockoutDays), LockoutDays is not null),
            (nameof(ObservationShift), ObservationShift is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal OisConventionRecord Named(string name) =>
        new(name)
        {
            Index = Index,
            SettlementDays = SettlementDays,
            SettlementCalendar = SettlementCalendar,
            PaymentLag = PaymentLag,
            PaymentConvention = PaymentConvention,
            PaymentFrequency = PaymentFrequency,
            PaymentCalendar = PaymentCalendar,
            FixedLegFrequency = FixedLegFrequency,
            FixedLegCalendar = FixedLegCalendar,
            FixedLegConvention = FixedLegConvention,
            FixedLegTerminationDateConvention = FixedLegTerminationDateConvention,
            FixedLegRule = FixedLegRule,
            FixedLegEndOfMonth = FixedLegEndOfMonth,
            FixedLegDayCounter = FixedLegDayCounter,
            OvernightLegCalendar = OvernightLegCalendar,
            OvernightLegConvention = OvernightLegConvention,
            OvernightLegTerminationDateConvention = OvernightLegTerminationDateConvention,
            OvernightLegRule = OvernightLegRule,
            OvernightLegEndOfMonth = OvernightLegEndOfMonth,
            TelescopicValueDates = TelescopicValueDates,
            Averaging = Averaging,
            LookbackDays = LookbackDays,
            LockoutDays = LockoutDays,
            ObservationShift = ObservationShift,
        };
}
