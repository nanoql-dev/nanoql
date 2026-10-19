// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2017 BN Algorithms Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebond.hpp ql/experimental/callablebonds/callablebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A callable or puttable zero-coupon bond: a single redemption at maturity.</summary>
/// <remarks>
/// The maturity date is the one given, unadjusted; the redemption is paid on it adjusted by the payment convention on
/// the calendar, as in QuantLib. Its frequency is <see cref="Frequency.Once"/>, which the Black engine's forward yield
/// reads as annual.
/// </remarks>
[Experimental("NQL0001")]
public sealed class CallableZeroCouponBond : CallableBond
{
    /// <summary>Initializes a new instance of the <see cref="CallableZeroCouponBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="faceAmount">The notional.</param>
    /// <param name="calendar">
    /// The settlement and payment calendar, or <see langword="null"/>, QuantLib's empty calendar, which builds only
    /// with an <see cref="BusinessDayConvention.Unadjusted"/> payment convention.
    /// </param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="dayCounter">
    /// The day counter of the payments, which the Black engine's forward yield reads, or <see langword="null"/>,
    /// QuantLib's empty day counter, which only the Black engine refuses.
    /// </param>
    /// <param name="paymentConvention">The business-day convention of the redemption date.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="putCallSchedule">The calls and puts, copied; none by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or the convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>; a callability is <see langword="null"/>, or after the maturity date.
    /// </exception>
    /// <exception cref="NanoQLException">The redemption date is adjusted on QuantLib's empty calendar.</exception>
    public CallableZeroCouponBond(
        int settlementDays,
        double faceAmount,
        Calendar? calendar,
        Date maturityDate,
        DayCounter? dayCounter,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        double redemption = 100.0,
        Date? issueDate = null,
        IEnumerable<Callability>? putCallSchedule = null)
        : base(settlementDays, maturityDate, calendar, dayCounter, faceAmount, Frequency.Once, issueDate, putCallSchedule)
    {
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));

        Date redemptionDate = (Calendar ?? EmptyCalendar.Instance).Adjust(maturityDate, paymentConvention);
        SetSingleRedemption(faceAmount, redemption, redemptionDate);
    }
}
