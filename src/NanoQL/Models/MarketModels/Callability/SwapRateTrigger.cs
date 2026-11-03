// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/swapratetrigger.hpp ql/models/marketmodels/callability/swapratetrigger.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>
/// An exercise strategy on swap-rate triggers: at each exercise, exercises where the coterminal swap rate from the next
/// rate is above the exercise's trigger.
/// </summary>
/// <remarks>A working type.</remarks>
[WorkingType]
public sealed class SwapRateTrigger : ExerciseStrategy<CurveState>
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> swapTriggers;
    private readonly ImmutableArray<double> exerciseTimes;
    private readonly ImmutableArray<int> rateIndex;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="SwapRateTrigger"/> class.</summary>
    /// <param name="rateTimes">The rate times, increasing, at least two.</param>
    /// <param name="swapTriggers">The triggers, one an exercise.</param>
    /// <param name="exerciseTimes">The exercise times, increasing.</param>
    /// <exception cref="ArgumentException">
    /// The times aren't increasing, the rate times are fewer than two, or the triggers aren't one an exercise.
    /// </exception>
    public SwapRateTrigger(ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> swapTriggers, ReadOnlySpan<double> exerciseTimes)
    {
        this.rateTimes = [.. rateTimes];
        this.swapTriggers = [.. swapTriggers];
        this.exerciseTimes = [.. exerciseTimes];

        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(rateTimes.Length > 1))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        UtilitiesFunctions.CheckIncreasingTimes(exerciseTimes);

        if (swapTriggers.Length != exerciseTimes.Length)
        {
            throw new ArgumentException("swapTriggers/exerciseTimes mismatch", nameof(swapTriggers));
        }

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

    private SwapRateTrigger(SwapRateTrigger other)
    {
        rateTimes = other.rateTimes;
        swapTriggers = other.swapTriggers;
        exerciseTimes = other.exerciseTimes;
        rateIndex = other.rateIndex;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> ExerciseTimes() => exerciseTimes;

    /// <summary>The exercise times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> RelevantTimes() => exerciseTimes;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    public override bool Exercise(CurveState currentState)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        int index = rateIndex[currentIndex - 1];
        double currentSwapRate = currentState.CoterminalSwapRate(index);
        return swapTriggers[currentIndex - 1] < currentSwapRate;
    }

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState) => ++currentIndex;

    /// <inheritdoc/>
    public override ExerciseStrategy<CurveState> Clone() => new SwapRateTrigger(this);
}
