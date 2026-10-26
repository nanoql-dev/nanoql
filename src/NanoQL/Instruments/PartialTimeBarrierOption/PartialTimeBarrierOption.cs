// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/partialtimebarrieroption.hpp ql/instruments/partialtimebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A barrier option whose barrier is monitored over part of its lifetime: from the start to a cover event, or from the
/// cover event to the exercise date.
/// </summary>
/// <remarks>Immutable and sealed: QuantLib derives nothing from it.</remarks>
public sealed class PartialTimeBarrierOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="PartialTimeBarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrierRange">When the barrier is monitored.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="coverEventDate">The cover event's date.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> or
    /// <paramref name="barrierRange"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public PartialTimeBarrierOption(
        BarrierType barrierType,
        PartialBarrierRange barrierRange,
        double barrier,
        double rebate,
        Date coverEventDate,
        StrikedTypePayoff payoff,
        Exercise exercise)
        : base(payoff, exercise)
    {
        EnumArguments.Check(barrierType, nameof(barrierType));
        EnumArguments.Check(barrierRange, nameof(barrierRange));
        BarrierType = barrierType;
        BarrierRange = barrierRange;
        Barrier = barrier;
        Rebate = rebate;
        CoverEventDate = coverEventDate;
    }

    /// <summary>Gets the barrier's type.</summary>
    public BarrierType BarrierType { get; }

    /// <summary>Gets when the barrier is monitored.</summary>
    public PartialBarrierRange BarrierRange { get; }

    /// <summary>Gets the barrier.</summary>
    public double Barrier { get; }

    /// <summary>Gets the rebate.</summary>
    public double Rebate { get; }

    /// <summary>Gets the cover event's date.</summary>
    public Date CoverEventDate { get; }

    /// <summary>Checks the option's terms before an engine prices it: QuantLib's <c>arguments::validate</c>.</summary>
    /// <exception cref="NanoQLException">The option has no payoff, or a cover event on or after the exercise
    /// date.</exception>
    public override void Validate()
    {
        base.Validate();

        // QuantLib's "no cover event date given" can't fail: the date isn't nullable
        if (!(CoverEventDate < Exercise.LastDate()))
        {
            throw new NanoQLException("cover event date equal or later than exercise date");
        }
    }
}
