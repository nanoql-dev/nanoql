// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeCreditDefaultSwap"/> takes for an option it isn't given, which
/// <see cref="ConventionProvider.ResolveCdsDefaults"/> returns: QuantLib's, the calendar among them.
/// </summary>
/// <param name="CouponTenor">The coupon tenor of a schedule the builder generates.</param>
/// <param name="Rule">The date-generation rule.</param>
/// <param name="Convention">The business-day convention of the schedule and the payments.</param>
/// <param name="DayCounter">The accrual's day counter.</param>
/// <param name="LastPeriodDayCounter">The last period's accrual day counter, or <see langword="null"/> for the
/// accrual's.</param>
/// <param name="SettlesAccrual">Whether the accrued coupon is due on a default.</param>
/// <param name="PaysAtDefaultTime">Whether the payments a default triggers are due at the default time.</param>
/// <param name="RebatesAccrual">Whether the seller pays the accrued current coupon at the cash settlement date.</param>
/// <param name="CashSettlementDays">The business days from the trade date to the cash settlement date.</param>
/// <param name="Calendar">The calendar the schedule and the cash settlement date are built on.</param>
public sealed record CdsDefaults(
    Period CouponTenor,
    DateGenerationRule Rule,
    BusinessDayConvention Convention,
    DayCounter DayCounter,
    DayCounter? LastPeriodDayCounter,
    bool SettlesAccrual,
    bool PaysAtDefaultTime,
    bool RebatesAccrual,
    int CashSettlementDays,
    Calendar Calendar);
