// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/option.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The base option class: an instrument with a payoff and an exercise.</summary>
/// <remarks>
/// <para>
/// QuantLib's <c>Option::arguments</c> are the option's own terms (playbook §2.1): <see cref="Validate"/> runs their
/// check. The payoff may be <see langword="null"/>, as a swaption's is, and an option without one is refused there,
/// as QuantLib refuses it, "no payoff given". The exercise is refused on entry when <see langword="null"/>, where
/// QuantLib's "no exercise given" check comes later: every option's <see cref="Instrument.IsExpired"/> reads the
/// exercise's dates before the arguments are checked, which for a null exercise is undefined in QuantLib.
/// </para>
/// <para>An extension point, as in QuantLib: swaptions and the equity options derive from it.</para>
/// </remarks>
public abstract class Option : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="Option"/> class.</summary>
    /// <param name="payoff">The payoff, or <see langword="null"/> for an option that has none, as a swaption.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    protected Option(Payoff? payoff, Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        Payoff = payoff;
        Exercise = exercise;
    }

    /// <summary>Gets the payoff, or <see langword="null"/> for an option that has none, as a swaption.</summary>
    public Payoff? Payoff { get; }

    /// <summary>Gets the exercise.</summary>
    public Exercise Exercise { get; }

    /// <summary>Checks the option's terms before an engine prices it: QuantLib's <c>Option::arguments::validate</c>.</summary>
    /// <exception cref="NanoQLException">The option has no payoff.</exception>
    public override void Validate()
    {
        if (Payoff is null)
        {
            throw new NanoQLException("no payoff given");
        }

        // QuantLib's "no exercise given" can't fail: the exercise is refused at construction when null.
    }
}
