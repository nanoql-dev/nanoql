// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/lookbackoption.hpp ql/instruments/lookbackoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A continuous partial-time fixed-strike lookback option (Heynen and Kat, 1994): the extremum is observed from a date
/// after the start to expiry, and a call pays the maximum less the strike, a put the strike less the minimum.
/// </summary>
/// <remarks>
/// Immutable. Its extremum observed so far is 0, as QuantLib's constructor sets it: the observation starts later. Its
/// terms are checked before they are read, where QuantLib's validation dereferences a European exercise unchecked.
/// </remarks>
public sealed class ContinuousPartialFixedLookbackOption : ContinuousFixedLookbackOption
{
    /// <summary>Initializes a new instance of the <see cref="ContinuousPartialFixedLookbackOption"/> class.</summary>
    /// <param name="lookbackPeriodStart">The date the observation of the extremum starts.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="lookbackPeriodStart"/> is the default date.</exception>
    public ContinuousPartialFixedLookbackOption(Date lookbackPeriodStart, StrikedTypePayoff payoff, Exercise exercise)
        : base(0.0, payoff, exercise)
    {
        Date.ThrowIfDefault(lookbackPeriodStart, nameof(lookbackPeriodStart));
        LookbackPeriodStart = lookbackPeriodStart;
    }

    /// <summary>Gets the date the observation of the extremum starts.</summary>
    public Date LookbackPeriodStart { get; }

    /// <summary>Checks the option's terms before an engine prices it: QuantLib's <c>arguments::validate</c>.</summary>
    /// <exception cref="NanoQLException">The exercise isn't European (QuantLib reads it unchecked), or the lookback
    /// period starts after the exercise date.</exception>
    public override void Validate()
    {
        base.Validate();
        if (Exercise is not EuropeanExercise europeanExercise)
        {
            throw new NanoQLException("a partial-time fixed lookback option needs a European exercise");
        }

        if (!(LookbackPeriodStart <= europeanExercise.LastDate()))
        {
            throw new NanoQLException("lookback start date must be earlier than exercise date");
        }
    }
}
