// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2023 Andrea Pellegatta
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/defaultprobabilityhelpers.hpp ql/termstructures/credit/defaultprobabilityhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A bootstrap helper on a credit default swap quoted as a running spread: its implied quote is the swap's
/// fair spread.</summary>
/// <remarks>The swap buys protection on a notional of 100 at a running spread of 1%, from the protection start; the
/// rest is <see cref="CdsHelper"/>'s.</remarks>
public sealed class SpreadCdsHelper : CdsHelper
{
    /// <summary>Initializes a new instance of the <see cref="SpreadCdsHelper"/> class.</summary>
    /// <param name="runningSpread">The quoted running spread, in fractional units.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="settlementDays">The calendar days from the evaluation date to the protection start.</param>
    /// <param name="calendar">The swap's calendar, or <see langword="null"/> for QuantLib's empty calendar, used
    /// unchecked as QuantLib uses it.</param>
    /// <param name="frequency">The coupon frequency.</param>
    /// <param name="paymentConvention">The convention of the schedule and the payments.</param>
    /// <param name="rule">The date-generation rule.</param>
    /// <param name="dayCounter">The day counter of the coupons.</param>
    /// <param name="recoveryRate">The reference entity's recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/>, QuantLib's empty handle, which the
    /// engines refuse.</param>
    /// <param name="context">The evaluation context whose evaluation date the helper's dates follow.</param>
    /// <param name="settlesAccrual">Whether the accrued coupon is due on a default.</param>
    /// <param name="paysAtDefaultTime">Whether the payments a default triggers are due at the default time.</param>
    /// <param name="startDate">The schedule's start and the date the maturity is counted from, or
    /// <see langword="null"/> for the protection start and the evaluation date.</param>
    /// <param name="lastPeriodDayCounter">The day counter of the last coupon, or <see langword="null"/> for
    /// <paramref name="dayCounter"/>.</param>
    /// <param name="rebatesAccrual">Whether the accrual is rebated at the cash settlement date.</param>
    /// <param name="model">The model the swap is priced with.</param>
    /// <param name="tradeDate">The swap's trade date, or <see langword="null"/> for the swap's own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the schedule can't be built.</exception>
    /// <exception cref="NanoQLException">The schedule has no maturity, or can't be generated.</exception>
    public SpreadCdsHelper(
        double runningSpread,
        Period tenor,
        int settlementDays,
        Calendar? calendar,
        Frequency frequency,
        BusinessDayConvention paymentConvention,
        DateGenerationRule rule,
        DayCounter dayCounter,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        EvaluationContext context,
        bool settlesAccrual = true,
        bool paysAtDefaultTime = true,
        Date? startDate = null,
        DayCounter? lastPeriodDayCounter = null,
        bool rebatesAccrual = true,
        CreditDefaultSwapPricingModel model = CreditDefaultSwapPricingModel.Midpoint,
        Date? tradeDate = null)
        : base(
            runningSpread,
            tenor,
            settlementDays,
            calendar,
            frequency,
            paymentConvention,
            rule,
            dayCounter,
            recoveryRate,
            discountCurve,
            context,
            settlesAccrual,
            paysAtDefaultTime,
            startDate,
            lastPeriodDayCounter,
            rebatesAccrual,
            model,
            tradeDate)
    {
    }

    /// <inheritdoc/>
    /// <remarks>The swap's fair spread on the bound curve.</remarks>
    /// <exception cref="NanoQLException">The engine leaves the fair spread null, with QuantLib's "fair spread not
    /// available".</exception>
    public override double ImpliedQuote(EvaluationContext context) =>
        Price(context).FairSpread ?? throw new NanoQLException("fair spread not available");

    /// <inheritdoc/>
    protected override CreditDefaultSwap BuildSwap() =>
        new(
            ProtectionSide.Buyer,
            100.0,
            0.01,
            Schedule,
            PaymentConvention,
            DayCounter,
            SettlesAccrual,
            PaysAtDefaultTime,
            ProtectionStart,
            null,
            LastPeriodDayCounter,
            RebatesAccrual,
            TradeDate);
}
