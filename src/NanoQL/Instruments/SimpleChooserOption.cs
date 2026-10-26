// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/simplechooseroption.hpp ql/instruments/simplechooseroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A simple chooser option: its holder chooses at a date before exercise whether it is a call or a put, both with
/// one strike and one exercise.
/// </summary>
/// <remarks>Immutable; its payoff is the call's, as QuantLib's.</remarks>
public sealed class SimpleChooserOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="SimpleChooserOption"/> class.</summary>
    /// <param name="choosingDate">The date the holder chooses on.</param>
    /// <param name="strike">The strike of both the call and the put.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The choosing date is <c>default(Date)</c> (QuantLib's text).</exception>
    public SimpleChooserOption(Date choosingDate, double strike, Exercise exercise)
        : base(new PlainVanillaPayoff(OptionType.Call, strike), exercise)
    {
        if (choosingDate == default)
        {
            throw new ArgumentException(" no choosing date given", nameof(choosingDate));
        }

        ChoosingDate = choosingDate;
    }

    /// <summary>Gets the date the holder chooses on.</summary>
    public Date ChoosingDate { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The choosing date isn't before maturity.</exception>
    public override void Validate()
    {
        base.Validate();
        if (!(ChoosingDate < Exercise.LastDate()))
        {
            throw new NanoQLException("choosing date later than or equal to maturity date");
        }
    }
}
