// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/triggeredswapexercise.hpp ql/models/marketmodels/callability/triggeredswapexercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A parametric exercise on a swap-rate trigger: one variable, the coterminal swap rate from the next rate at each
/// exercise, and one parameter, the trigger, exercised when the rate reaches it; the strikes are the triggers' guesses.
/// </summary>
/// <remarks>A working type (the S-mmp plan's MMPR1 (i)).</remarks>
[WorkingType]
public sealed class TriggeredSwapExercise : MarketModelParametricExercise
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> exerciseTimes;
    private readonly ImmutableArray<double> strikes;
    private readonly ImmutableArray<int> rateIndex;
    private readonly EvolutionDescription evolution;
    private int currentStep;

    /// <summary>Initializes a new instance of the <see cref="TriggeredSwapExercise"/> class.</summary>
    /// <param name="rateTimes">The rate times.</param>
    /// <param name="exerciseTimes">The exercise times, the rule's evolution.</param>
    /// <param name="strikes">The triggers' guesses, one an exercise.</param>
    /// <exception cref="ArgumentException">The times don't make an evolution.</exception>
    public TriggeredSwapExercise(ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> exerciseTimes, ReadOnlySpan<double> strikes)
    {
        this.rateTimes = [.. rateTimes];
        this.exerciseTimes = [.. exerciseTimes];
        this.strikes = [.. strikes];
        evolution = new EvolutionDescription(rateTimes, exerciseTimes);
        int[] indices = new int[exerciseTimes.Length];
        int j = 0;
        for (int i = 0; i < exerciseTimes.Length; ++i)
        {
            while (j < rateTimes.Length && rateTimes[j] < exerciseTimes[i])
            {
                ++j;
            }

            indices[i] = j;
        }

        rateIndex = ImmutableArray.Create(indices);
    }

    private TriggeredSwapExercise(TriggeredSwapExercise other)
    {
        rateTimes = other.rateTimes;
        exerciseTimes = other.exerciseTimes;
        strikes = other.strikes;
        rateIndex = other.rateIndex;
        evolution = other.evolution;
        currentStep = other.currentStep;
    }

    /// <inheritdoc/>
    public override int NumberOfExercises => exerciseTimes.Length;

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState) => ++currentStep;

    /// <inheritdoc/>
    public override void Reset() => currentStep = 0;

    /// <summary>Every evolution time is an exercise time.</summary>
    /// <returns>One flag an exercise, every one set.</returns>
    public override ImmutableArray<bool> IsExerciseTime() => [.. Enumerable.Repeat(true, NumberOfExercises)];

    /// <summary>Writes the current exercise's variable, the coterminal swap rate from the next rate.</summary>
    /// <param name="currentState">The state.</param>
    /// <param name="results">The variable written.</param>
    public override void Values(CurveState currentState, Span<double> results)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        int swapIndex = rateIndex[currentStep - 1];
        results[0] = currentState.CoterminalSwapRate(swapIndex);
    }

    /// <summary>One variable an exercise.</summary>
    /// <returns>One count an exercise.</returns>
    public override ImmutableArray<int> NumberOfVariables() => [.. Enumerable.Repeat(1, NumberOfExercises)];

    /// <summary>One parameter an exercise.</summary>
    /// <returns>One count an exercise.</returns>
    public override ImmutableArray<int> NumberOfParameters() => [.. Enumerable.Repeat(1, NumberOfExercises)];

    /// <summary>Exercises where the swap rate reaches the trigger.</summary>
    /// <param name="exerciseNumber">The exercise's index, unread.</param>
    /// <param name="parameters">The trigger.</param>
    /// <param name="variables">The swap rate.</param>
    /// <returns><see langword="true"/> to exercise.</returns>
    public override bool Exercise(int exerciseNumber, ReadOnlySpan<double> parameters, ReadOnlySpan<double> variables) =>
        variables[0] >= parameters[0];

    /// <summary>Guesses the exercise's trigger: its strike.</summary>
    /// <param name="exerciseNumber">The exercise's index.</param>
    /// <param name="parameters">The trigger written.</param>
    /// <exception cref="ArgumentOutOfRangeException">There's no strike for the exercise, QuantLib's checked read.</exception>
    public override void Guess(int exerciseNumber, Span<double> parameters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exerciseNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(exerciseNumber, strikes.Length);
        parameters[0] = strikes[exerciseNumber];
    }

    /// <inheritdoc/>
    public override MarketModelParametricExercise Clone() => new TriggeredSwapExercise(this);
}
