// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A fixed-rate bond preset's conventions, which <see cref="ConventionProvider.ResolveBond"/> returns: QuantLib's
/// values for the preset (<see cref="BTP"/>), which its class builds itself on. The trade's terms (the maturity, the
/// coupons, the dates and the redemption) are its constructor's arguments.
/// </summary>
/// <param name="SettlementDays">The settlement days.</param>
/// <param name="Tenor">The schedule's tenor.</param>
/// <param name="Calendar">The schedule's calendar.</param>
/// <param name="Convention">The schedule's business-day convention.</param>
/// <param name="TerminationDateConvention">The schedule's termination-date convention.</param>
/// <param name="Rule">The schedule's date-generation rule.</param>
/// <param name="EndOfMonth">Whether the schedule's dates stay at month end.</param>
/// <param name="AccrualDayCounter">The coupons' accrual day counter.</param>
/// <param name="PaymentConvention">The payment dates' business-day convention.</param>
/// <param name="PaymentCalendar">The payment calendar, or <see langword="null"/> for the schedule's.</param>
/// <param name="ExCouponPeriod">The ex-coupon period, or the empty period for none.</param>
/// <param name="ExCouponCalendar">The ex-coupon period's calendar, or <see langword="null"/>.</param>
/// <param name="ExCouponConvention">The ex-coupon dates' business-day convention.</param>
/// <param name="ExCouponEndOfMonth">Whether the ex-coupon dates stay at month end.</param>
public sealed record BondConventions(
    int SettlementDays,
    Period Tenor,
    Calendar Calendar,
    BusinessDayConvention Convention,
    BusinessDayConvention TerminationDateConvention,
    DateGenerationRule Rule,
    bool EndOfMonth,
    DayCounter AccrualDayCounter,
    BusinessDayConvention PaymentConvention,
    Calendar? PaymentCalendar,
    Period ExCouponPeriod,
    Calendar? ExCouponCalendar,
    BusinessDayConvention ExCouponConvention,
    bool ExCouponEndOfMonth);
