// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/swapbasissystem.hpp ql/models/marketmodels/callability/swapbasissystem.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The basis functions 1, the next forward rate and the coterminal swap rate after it, at each exercise; at an exercise
/// on the last rate, 1 and the forward rate.
/// </summary>
/// <remarks>
/// <para>A working type (the S-mmp plan's MMPR1 (i)).</para>
/// <para>
/// Each exercise's number of functions follows its own rate (the S-mmp plan's MMPR4 (xv), a documented divergence):
/// QuantLib adjusts only the last exercise's count (<c>swapbasissystem.cpp:46</c>), so an earlier exercise on the last
/// rate reports 3 functions where <see cref="Values"/> gives 2 (<c>Divergences/BasisSystemFunctionCountTests.cs</c>).
/// </para>
/// </remarks>
public sealed class SwapBasisSystem : MarketModelBasisSystem
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> exerciseTimes;
    private readonly ImmutableArray<int> rateIndex;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="SwapBasisSystem"/> class.</summary>
    /// <param name="rateTimes">The rate times.</param>
    /// <param name="exerciseTimes">The exercise times, the basis system's evolution.</param>
    /// <exception cref="ArgumentException">The times don't make an evolution.</exception>
    public SwapBasisSystem(ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> exerciseTimes)
    {
        this.rateTimes = [.. rateTimes];
        this.exerciseTimes = [.. exerciseTimes];
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

    private SwapBasisSystem(SwapBasisSystem other)
    {
        rateTimes = other.rateTimes;
        exerciseTimes = other.exerciseTimes;
        rateIndex = other.rateIndex;
        evolution = other.evolution;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override int NumberOfExercises => exerciseTimes.Length;

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>The number of basis functions at each exercise: 3, or 2 at an exercise on the last rate.</summary>
    /// <returns>One count an exercise.</returns>
    public override ImmutableArray<int> NumberOfFunctions() =>
        [.. rateIndex.Select(r => r < rateTimes.Length - 2 ? 3 : 2)];

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState) => ++currentIndex;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <summary>Every evolution time is an exercise time.</summary>
    /// <returns>One flag an exercise, every one set.</returns>
    public override ImmutableArray<bool> IsExerciseTime() => [.. Enumerable.Repeat(true, exerciseTimes.Length)];

    /// <inheritdoc/>
    public override void Values(CurveState currentState, Span<double> results)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        int index = rateIndex[currentIndex - 1];

        results[0] = 1.0;
        results[1] = currentState.ForwardRate(index);
        if (index < rateTimes.Length - 2)
        {
            results[2] = currentState.CoterminalSwapRate(index + 1);
        }
    }

    /// <inheritdoc/>
    public override MarketModelBasisSystem Clone() => new SwapBasisSystem(this);
}
