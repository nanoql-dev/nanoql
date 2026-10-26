// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/complexchooseroption.hpp ql/instruments/complexchooseroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A complex chooser option: its holder chooses at a date before exercise whether it is a call or a put, each with its
/// own strike and exercise.
/// </summary>
/// <remarks>Immutable; its payoff and exercise are the call's, as QuantLib's.</remarks>
public sealed class ComplexChooserOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="ComplexChooserOption"/> class.</summary>
    /// <param name="choosingDate">The date the holder chooses on.</param>
    /// <param name="strikeCall">The call's strike.</param>
    /// <param name="strikePut">The put's strike.</param>
    /// <param name="exerciseCall">The call's exercise.</param>
    /// <param name="exercisePut">The put's exercise.</param>
    /// <exception cref="ArgumentNullException">An exercise is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The choosing date is <c>default(Date)</c> (QuantLib's text).</exception>
    public ComplexChooserOption(
        Date choosingDate, double strikeCall, double strikePut, Exercise exerciseCall, Exercise exercisePut)
        : base(new PlainVanillaPayoff(OptionType.Call, strikeCall), exerciseCall)
    {
        ArgumentNullException.ThrowIfNull(exercisePut);
        if (choosingDate == default)
        {
            throw new ArgumentException(" no choosing date given", nameof(choosingDate));
        }

        ChoosingDate = choosingDate;
        StrikeCall = strikeCall;
        StrikePut = strikePut;
        ExercisePut = exercisePut;
    }

    /// <summary>Gets the date the holder chooses on.</summary>
    public Date ChoosingDate { get; }

    /// <summary>Gets the call's strike.</summary>
    public double StrikeCall { get; }

    /// <summary>Gets the put's strike.</summary>
    public double StrikePut { get; }

    /// <summary>Gets the call's exercise, the option's own.</summary>
    public Exercise ExerciseCall => Exercise;

    /// <summary>Gets the put's exercise.</summary>
    public Exercise ExercisePut { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The choosing date isn't before both maturities.</exception>
    public override void Validate()
    {
        base.Validate();
        if (!(ChoosingDate < ExerciseCall.LastDate()))
        {
            throw new NanoQLException("choosing date later than or equal to Call maturity date");
        }

        if (!(ChoosingDate < ExercisePut.LastDate()))
        {
            throw new NanoQLException("choosing date later than or equal to Put maturity date");
        }
    }
}
