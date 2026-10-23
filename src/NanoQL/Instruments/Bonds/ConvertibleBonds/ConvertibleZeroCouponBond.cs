// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/convertiblebonds.hpp ql/instruments/bonds/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A convertible zero-coupon bond: its redemption alone, on a notional of 100 at the schedule's end date.</summary>
/// <remarks>
/// Most members inherited from <see cref="Bond"/>, such as the yield and the yield-based prices, refer to the
/// underlying plain-vanilla bond and don't take convertibility and callability into account, as QuantLib warns.
/// </remarks>
public sealed class ConvertibleZeroCouponBond : ConvertibleBond
{
    /// <summary>Initializes a new instance of the <see cref="ConvertibleZeroCouponBond"/> class.</summary>
    /// <param name="exercise">The conversion's exercise.</param>
    /// <param name="conversionRatio">The number of shares one bond converts into.</param>
    /// <param name="callability">The calls, soft calls and puts, copied, the last one the latest.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="dayCounter">The day counter, which QuantLib takes and doesn't read; <see langword="null"/> allowed.</param>
    /// <param name="schedule">The schedule, whose calendar is the bond's and whose end date its maturity.</param>
    /// <param name="redemption">The redemption, per 100 of notional.</param>
    /// <exception cref="ArgumentNullException">The exercise, the callabilities or the schedule is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>; a callability is <see langword="null"/>; or the last callability is after
    /// the maturity date.
    /// </exception>
    /// <exception cref="NanoQLException">The schedule is empty.</exception>
    /// <remarks>The notional is 100 whatever the redemption, as QuantLib forces it.</remarks>
    public ConvertibleZeroCouponBond(
        Exercise exercise,
        double conversionRatio,
        IEnumerable<Callability> callability,
        Date? issueDate,
        int settlementDays,
        DayCounter? dayCounter,
        Schedule schedule,
        double redemption = 100)
        : base(exercise, conversionRatio, callability, issueDate, settlementDays, schedule, redemption)
    {
        _ = dayCounter;
        cashflows = [];

        // !!! notional forcibly set to 100
        SetSingleRedemption(100.0, redemption, maturityDate!.Value);
    }
}
