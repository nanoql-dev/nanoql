// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeOIS"/> takes for an option it isn't given, on an overnight index, which
/// <see cref="ConventionProvider.ResolveOisDefaults"/> returns: QuantLib's, its settlement days by the index's class.
/// The calendars and the fixed leg's day counter are the index's, the payment calendar each leg's, the end-of-month
/// flags the start date's and the lookback the index's fixing days, so they aren't defaults.
/// </summary>
/// <param name="SettlementDays">The settlement days.</param>
/// <param name="FixedLegPaymentFrequency">The fixed leg's payment frequency.</param>
/// <param name="OvernightLegPaymentFrequency">The overnight leg's payment frequency.</param>
/// <param name="PaymentAdjustment">The payment dates' business-day convention.</param>
/// <param name="PaymentLag">The payment lag, in business days.</param>
/// <param name="FixedLegConvention">The fixed leg's business-day convention.</param>
/// <param name="FixedLegTerminationDateConvention">The fixed leg's termination-date convention.</param>
/// <param name="OvernightLegConvention">The overnight leg's business-day convention.</param>
/// <param name="OvernightLegTerminationDateConvention">The overnight leg's termination-date convention.</param>
/// <param name="FixedLegRule">The fixed leg's date-generation rule.</param>
/// <param name="OvernightLegRule">The overnight leg's date-generation rule.</param>
/// <param name="TelescopicValueDates">Whether value dates are telescopic.</param>
/// <param name="AveragingMethod">Whether the overnight rates are compounded or averaged.</param>
/// <param name="LockoutDays">The lockout days.</param>
/// <param name="ApplyObservationShift">Whether the observation shift applies.</param>
public sealed record OisDefaults(
    int SettlementDays,
    Frequency FixedLegPaymentFrequency,
    Frequency OvernightLegPaymentFrequency,
    BusinessDayConvention PaymentAdjustment,
    int PaymentLag,
    BusinessDayConvention FixedLegConvention,
    BusinessDayConvention FixedLegTerminationDateConvention,
    BusinessDayConvention OvernightLegConvention,
    BusinessDayConvention OvernightLegTerminationDateConvention,
    DateGenerationRule FixedLegRule,
    DateGenerationRule OvernightLegRule,
    bool TelescopicValueDates,
    RateAveragingType AveragingMethod,
    int LockoutDays,
    bool ApplyObservationShift);
