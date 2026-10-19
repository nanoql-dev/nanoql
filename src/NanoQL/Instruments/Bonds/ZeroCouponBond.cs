// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/zerocouponbond.hpp ql/instruments/bonds/zerocouponbond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A zero-coupon bond: a single redemption at maturity.</summary>
/// <remarks>
/// The maturity date is the one given, unadjusted; the redemption is paid on it adjusted by the payment convention on
/// the calendar, so the two differ when the maturity falls on a holiday, as in QuantLib.
/// </remarks>
public sealed class ZeroCouponBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="ZeroCouponBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">
    /// The settlement and payment calendar, or <see langword="null"/>, QuantLib's empty calendar, which builds only
    /// with an <see cref="BusinessDayConvention.Unadjusted"/> payment convention.
    /// </param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="paymentConvention">The business-day convention of the redemption date.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or the convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The redemption date is adjusted on QuantLib's empty calendar.</exception>
    public ZeroCouponBond(
        int settlementDays,
        Calendar? calendar,
        double faceAmount,
        Date maturityDate,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        double redemption = 100.0,
        Date? issueDate = null)
        : base(settlementDays, calendar, issueDate)
    {
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));

        this.maturityDate = maturityDate;
        Date redemptionDate = (Calendar ?? EmptyCalendar.Instance).Adjust(maturityDate, paymentConvention);
        SetSingleRedemption(faceAmount, redemption, redemptionDate);
    }
}
