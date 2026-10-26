// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/compoundoption.hpp ql/instruments/compoundoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A compound option: the mother option, an option on its underlying daughter option.</summary>
/// <remarks>Immutable.</remarks>
public sealed class CompoundOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="CompoundOption"/> class.</summary>
    /// <param name="motherPayoff">The mother option's payoff, on the daughter's value.</param>
    /// <param name="motherExercise">The mother option's exercise.</param>
    /// <param name="daughterPayoff">The daughter option's payoff, on the asset.</param>
    /// <param name="daughterExercise">The daughter option's exercise.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CompoundOption(
        StrikedTypePayoff motherPayoff,
        Exercise motherExercise,
        StrikedTypePayoff daughterPayoff,
        Exercise daughterExercise)
        : base(motherPayoff, motherExercise)
    {
        ArgumentNullException.ThrowIfNull(daughterPayoff);
        ArgumentNullException.ThrowIfNull(daughterExercise);
        DaughterPayoff = daughterPayoff;
        DaughterExercise = daughterExercise;
    }

    /// <summary>Gets the daughter option's payoff.</summary>
    public StrikedTypePayoff DaughterPayoff { get; }

    /// <summary>Gets the daughter option's exercise.</summary>
    public Exercise DaughterExercise { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The mother option matures after the daughter.</exception>
    public override void Validate()
    {
        base.Validate();
        if (!(Exercise.LastDate() <= DaughterExercise.LastDate()))
        {
            throw new NanoQLException("maturity of compound option exceeds maturity of underlying option");
        }
    }
}
