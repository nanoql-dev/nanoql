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
/// A continuous partial-time floating-strike lookback option (Heynen and Kat, 1994): the extremum is observed from the
/// start up to a date before expiry, and a call pays the terminal spot less λ times the minimum, a put λ times the
/// maximum less the terminal spot.
/// </summary>
/// <remarks>
/// Immutable. Its terms are checked before they are read, where QuantLib's validation dereferences a European exercise
/// and a floating payoff unchecked.
/// </remarks>
public sealed class ContinuousPartialFloatingLookbackOption : ContinuousFloatingLookbackOption
{
    /// <summary>Initializes a new instance of the <see cref="ContinuousPartialFloatingLookbackOption"/> class.</summary>
    /// <param name="minmax">The extremum observed so far: the minimum for a call, the maximum for a put.</param>
    /// <param name="lambda">The multiplier of the extremum: 1 or more for a call, 1 or less for a put.</param>
    /// <param name="lookbackPeriodEnd">The date the observation of the extremum ends.</param>
    /// <param name="payoff">The floating payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="lookbackPeriodEnd"/> is the default date.</exception>
    public ContinuousPartialFloatingLookbackOption(
        double minmax, double lambda, Date lookbackPeriodEnd, TypePayoff payoff, Exercise exercise)
        : base(minmax, payoff, exercise)
    {
        Date.ThrowIfDefault(lookbackPeriodEnd, nameof(lookbackPeriodEnd));
        Lambda = lambda;
        LookbackPeriodEnd = lookbackPeriodEnd;
    }

    /// <summary>Gets the multiplier of the extremum.</summary>
    public double Lambda { get; }

    /// <summary>Gets the date the observation of the extremum ends.</summary>
    public Date LookbackPeriodEnd { get; }

    /// <summary>Checks the option's terms before an engine prices it: QuantLib's <c>arguments::validate</c>.</summary>
    /// <exception cref="NanoQLException">
    /// The extremum is negative, the exercise isn't European or the payoff isn't floating (QuantLib reads them
    /// unchecked), the lookback period ends after the exercise date, or λ is below 1 for a call or above 1 for a put.
    /// </exception>
    public override void Validate()
    {
        base.Validate();
        if (Exercise is not EuropeanExercise europeanExercise)
        {
            throw new NanoQLException("a partial-time floating lookback option needs a European exercise");
        }

        if (!(LookbackPeriodEnd <= europeanExercise.LastDate()))
        {
            throw new NanoQLException("lookback start date must be earlier than exercise date");
        }

        if (Payoff is not FloatingTypePayoff floatingTypePayoff)
        {
            throw new NanoQLException("a partial-time floating lookback option needs a floating payoff");
        }

        if (floatingTypePayoff.OptionType == OptionType.Call && !(Lambda >= 1.0))
        {
            throw new NanoQLException("lambda should be greater than or equal to 1 for calls");
        }

        if (floatingTypePayoff.OptionType == OptionType.Put && !(Lambda <= 1.0))
        {
            throw new NanoQLException("lambda should be smaller than or equal to 1 for puts");
        }
    }
}
