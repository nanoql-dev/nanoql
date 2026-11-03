// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/holderextensibleoption.hpp ql/instruments/holderextensibleoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A holder-extensible option: exercised at maturity, or extended by its holder to a second maturity, at a second
/// strike, for a premium paid to the writer.
/// </summary>
/// <remarks>
/// Immutable. The option type argument is QuantLib's, which its engine never reads (the payoff's type rules); it is kept
/// for QuantLib's signature.
/// </remarks>
public sealed class HolderExtensibleOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="HolderExtensibleOption"/> class.</summary>
    /// <param name="type">The option's type, which QuantLib's engine doesn't read.</param>
    /// <param name="premium">The premium the holder pays to extend.</param>
    /// <param name="secondExpiryDate">The extended maturity.</param>
    /// <param name="secondStrike">The extended strike.</param>
    /// <param name="payoff">The payoff at the first maturity.</param>
    /// <param name="exercise">The exercise at the first maturity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The extended maturity is <c>default(Date)</c> (QuantLib's text).</exception>
    public HolderExtensibleOption(
        OptionType type,
        double premium,
        Date secondExpiryDate,
        double secondStrike,
        StrikedTypePayoff payoff,
        Exercise exercise)
        : base(payoff, exercise)
    {
        EnumArguments.Check(type, nameof(type));
        if (secondExpiryDate == default)
        {
            throw new ArgumentException("no extending date given", nameof(secondExpiryDate));
        }

        Type = type;
        Premium = premium;
        SecondExpiryDate = secondExpiryDate;
        SecondStrike = secondStrike;
    }

    /// <summary>Gets the option's type, which QuantLib's engine doesn't read.</summary>
    public OptionType Type { get; }

    /// <summary>Gets the premium the holder pays to extend.</summary>
    public double Premium { get; }

    /// <summary>Gets the extended maturity.</summary>
    public Date SecondExpiryDate { get; }

    /// <summary>Gets the extended strike.</summary>
    public double SecondStrike { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The premium isn't positive, or the extended maturity is before the first.</exception>
    public override void Validate()
    {
        base.Validate();
        if (!(Premium > 0))
        {
            throw new NanoQLException("negative premium not allowed");
        }

        if (!(SecondExpiryDate >= Exercise.LastDate()))
        {
            throw new NanoQLException("extended date is earlier than or equal to first maturity date");
        }
    }
}
