// SPDX-FileCopyrightText: Copyright (C) 2025 Hiroto Ogawa
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/perpetualfutures.hpp ql/instruments/perpetualfutures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A perpetual futures contract on an exchange rate FOR/DOM: no expiry, its price kept near the spot by funding
/// payments at the funding frequency (continuous where the frequency's length is 0). See Ackerer, Hugonnier and Jermann,
/// "Perpetual Futures Pricing" (2024).
/// </summary>
/// <remarks>Immutable; it never expires.</remarks>
public sealed class PerpetualFutures : Instrument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PerpetualFutures"/> class funded with the current spot every eight
    /// hours, on a null calendar and Actual/Actual (ISDA): QuantLib's defaults.
    /// </summary>
    /// <param name="payoffType">The payoff's type.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payoffType"/> isn't a member of its type.</exception>
    public PerpetualFutures(PerpetualFuturesPayoffType payoffType)
        : this(payoffType, PerpetualFuturesFundingType.FundingWithCurrentSpot)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PerpetualFutures"/> class funded every eight hours, on a null
    /// calendar and Actual/Actual (ISDA): QuantLib's defaults.
    /// </summary>
    /// <param name="payoffType">The payoff's type.</param>
    /// <param name="fundingType">The funding's type.</param>
    /// <exception cref="ArgumentOutOfRangeException">A type isn't a member of its type.</exception>
    public PerpetualFutures(PerpetualFuturesPayoffType payoffType, PerpetualFuturesFundingType fundingType)
        : this(payoffType, fundingType, new Period(8, TimeUnit.Hours))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PerpetualFutures"/> class on a null calendar and Actual/Actual
    /// (ISDA): QuantLib's defaults.
    /// </summary>
    /// <param name="payoffType">The payoff's type.</param>
    /// <param name="fundingType">The funding's type.</param>
    /// <param name="fundingFrequency">The funding's frequency; a length of 0 for continuous funding.</param>
    /// <exception cref="ArgumentOutOfRangeException">A type isn't a member of its type.</exception>
    public PerpetualFutures(
        PerpetualFuturesPayoffType payoffType, PerpetualFuturesFundingType fundingType, Period fundingFrequency)
        : this(payoffType, fundingType, fundingFrequency, new NullCalendar(), new ActualActual(ActualActualConvention.ISDA))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PerpetualFutures"/> class.</summary>
    /// <param name="payoffType">The payoff's type.</param>
    /// <param name="fundingType">The funding's type.</param>
    /// <param name="fundingFrequency">The funding's frequency; a length of 0 for continuous funding.</param>
    /// <param name="calendar">The calendar advancing the funding dates of a frequency in days or weeks.</param>
    /// <param name="dayCounter">The day counter of the funding times.</param>
    /// <exception cref="ArgumentNullException">The calendar or the day counter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A type isn't a member of its type.</exception>
    public PerpetualFutures(
        PerpetualFuturesPayoffType payoffType,
        PerpetualFuturesFundingType fundingType,
        Period fundingFrequency,
        Calendar calendar,
        DayCounter dayCounter)
    {
        EnumArguments.Check(payoffType, nameof(payoffType));
        EnumArguments.Check(fundingType, nameof(fundingType));
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(dayCounter);
        PayoffType = payoffType;
        FundingType = fundingType;
        FundingFrequency = fundingFrequency;
        Calendar = calendar;
        DayCounter = dayCounter;
    }

    /// <summary>Gets the payoff's type.</summary>
    public PerpetualFuturesPayoffType PayoffType { get; }

    /// <summary>Gets the funding's type.</summary>
    public PerpetualFuturesFundingType FundingType { get; }

    /// <summary>Gets the funding's frequency; a length of 0 for continuous funding.</summary>
    public Period FundingFrequency { get; }

    /// <summary>Gets the calendar advancing the funding dates of a frequency in days or weeks.</summary>
    public Calendar Calendar { get; }

    /// <summary>Gets the day counter of the funding times.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Whether the instrument has expired: a perpetual futures contract never does.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return false;
    }
}
