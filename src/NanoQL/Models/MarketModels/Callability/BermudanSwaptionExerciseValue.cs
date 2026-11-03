// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/bermudanswaptionexercisevalue.hpp ql/models/marketmodels/callability/bermudanswaptionexercisevalue.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.Models;

/// <summary>
/// The exercise value of a Bermudan swaption into the coterminal swaps: at rate time i, the coterminal swap's annuity
/// times its payoff on the coterminal swap rate, floored at 0, paid at rate time i.
/// </summary>
/// <remarks>A working type.</remarks>
[WorkingType]
public sealed class BermudanSwaptionExerciseValue : MarketModelExerciseValue
{
    private readonly int numberOfExercises;
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<Payoff> payoffs;
    private readonly EvolutionDescription evolution;
    private int currentIndex;
    private MarketModelMultiProductCashFlow cf;

    /// <summary>Initializes a new instance of the <see cref="BermudanSwaptionExerciseValue"/> class.</summary>
    /// <param name="rateTimes">The rate times, increasing, at least two.</param>
    /// <param name="payoffs">The payoffs, one an exercise at least: one a rate time but the last.</param>
    /// <exception cref="ArgumentException">
    /// The rate times aren't increasing or are fewer than two, or the payoffs are fewer than the exercises, which
    /// QuantLib reads past.
    /// </exception>
    public BermudanSwaptionExerciseValue(ReadOnlySpan<double> rateTimes, ReadOnlySpan<Payoff> payoffs)
    {
        foreach (Payoff p in payoffs)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(payoffs));
        }

        numberOfExercises = rateTimes.IsEmpty ? 0 : rateTimes.Length - 1;
        this.rateTimes = [.. rateTimes];
        this.payoffs = [.. payoffs];

        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(numberOfExercises > 0))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        MultiStepProductChecks.AtLeast(payoffs.Length, numberOfExercises, nameof(payoffs));
        evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }

    private BermudanSwaptionExerciseValue(BermudanSwaptionExerciseValue other)
    {
        numberOfExercises = other.numberOfExercises;
        rateTimes = other.rateTimes;
        payoffs = other.payoffs;
        evolution = other.evolution;
        currentIndex = other.currentIndex;
        cf = other.cf;
    }

    /// <inheritdoc/>
    public override int NumberOfExercises => numberOfExercises;

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>The rate times: exercise i pays at rate time i.</summary>
    /// <returns>The rate times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => rateTimes;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        Payoff p = payoffs[currentIndex];
        double value = currentState.CoterminalSwapAnnuity(currentIndex, currentIndex)
            * p.Evaluate(currentState.CoterminalSwapRate(currentIndex));

        value = Math.Max(value, 0.0);
        cf.TimeIndex = currentIndex;
        cf.Amount = value;
        ++currentIndex;
    }

    /// <summary>Every evolution time is an exercise time.</summary>
    /// <returns>One flag an exercise, every one set.</returns>
    public override ImmutableArray<bool> IsExerciseTime() => [.. Enumerable.Repeat(true, numberOfExercises)];

    /// <summary>The current exercise's cash flow, which <see cref="NextStep"/> computed.</summary>
    /// <param name="currentState">The state, unread.</param>
    /// <returns>The cash flow.</returns>
    public override MarketModelMultiProductCashFlow Value(CurveState currentState) => cf;

    /// <inheritdoc/>
    public override MarketModelExerciseValue Clone() => new BermudanSwaptionExerciseValue(this);
}
