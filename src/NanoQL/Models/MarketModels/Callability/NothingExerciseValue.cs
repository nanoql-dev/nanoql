// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/nothingexercisevalue.hpp ql/models/marketmodels/callability/nothingexercisevalue.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>An exercise value of nothing: at each exercise time a cash flow of 0, paid at the current rate time.</summary>
/// <remarks>A working type: the control of QuantLib's callable cases.</remarks>
[WorkingType]
public sealed class NothingExerciseValue : MarketModelExerciseValue
{
    private readonly int numberOfExercises;
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<bool> isExerciseTime;
    private readonly EvolutionDescription evolution;
    private int currentIndex;
    private MarketModelMultiProductCashFlow cf;

    /// <summary>Initializes a new instance of the <see cref="NothingExerciseValue"/> class.</summary>
    /// <param name="rateTimes">The rate times, increasing, at least two.</param>
    /// <param name="isExerciseTime">
    /// Whether each rate time but the last is an exercise time; empty for every one of them.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The rate times aren't increasing or are fewer than two, or the flags aren't one a rate time but the last.
    /// </exception>
    public NothingExerciseValue(ReadOnlySpan<double> rateTimes, ReadOnlySpan<bool> isExerciseTime = default)
    {
        this.rateTimes = [.. rateTimes];

        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(rateTimes.Length >= 2))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        cf.Amount = 0.0;
        evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
        if (isExerciseTime.IsEmpty)
        {
            this.isExerciseTime = [.. Enumerable.Repeat(true, rateTimes.Length - 1)];
        }
        else
        {
            if (isExerciseTime.Length != rateTimes.Length - 1)
            {
                throw new ArgumentException(
                    $"isExerciseTime ({isExerciseTime.Length}) must have same size as rateTimes minus 1 ({rateTimes.Length - 1})",
                    nameof(isExerciseTime));
            }

            this.isExerciseTime = [.. isExerciseTime];
        }

        foreach (bool e in this.isExerciseTime)
        {
            if (e)
            {
                ++numberOfExercises;
            }
        }
    }

    private NothingExerciseValue(NothingExerciseValue other)
    {
        numberOfExercises = other.numberOfExercises;
        rateTimes = other.rateTimes;
        isExerciseTime = other.isExerciseTime;
        evolution = other.evolution;
        currentIndex = other.currentIndex;
        cf = other.cf;
    }

    /// <inheritdoc/>
    public override int NumberOfExercises => numberOfExercises;

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>The rate times: the cash flow at evolution time i is indexed at rate time i.</summary>
    /// <returns>The rate times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => rateTimes;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState)
    {
        cf.TimeIndex = currentIndex;
        ++currentIndex;
    }

    /// <inheritdoc/>
    public override ImmutableArray<bool> IsExerciseTime() => isExerciseTime;

    /// <summary>The current cash flow, of 0.</summary>
    /// <param name="currentState">The state, unread.</param>
    /// <returns>The cash flow.</returns>
    public override MarketModelMultiProductCashFlow Value(CurveState currentState) => cf;

    /// <inheritdoc/>
    public override MarketModelExerciseValue Clone() => new NothingExerciseValue(this);
}
