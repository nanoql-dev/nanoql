// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/swapforwardbasissystem.hpp ql/models/marketmodels/callability/swapforwardbasissystem.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The quadratic basis functions in the next forward rate x, the coterminal swap rate after it y and the discount
/// ratio to the last rate time z at each exercise: 1, x, y, z, xy, yz, zx, x², y², z²; at an exercise on the
/// second-to-last rate, 1, x, the last forward y, x², xy, y²; on the last, 1, x, x².
/// </summary>
/// <remarks>
/// <para>A working type (the S-mmp plan's MMPR1 (i)).</para>
/// <para>
/// Each exercise's number of functions follows its own rate (the S-mmp plan's MMPR4 (xv), a documented divergence):
/// QuantLib adjusts only the last exercise's count (<c>swapforwardbasissystem.cpp:53</c>, <c>:56</c>), so an earlier
/// exercise in the last periods reports 10 functions where <see cref="Values"/> gives 6 or 3
/// (<c>Divergences/BasisSystemFunctionCountTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class SwapForwardBasisSystem : MarketModelBasisSystem
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> exerciseTimes;
    private readonly ImmutableArray<int> rateIndex;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="SwapForwardBasisSystem"/> class.</summary>
    /// <param name="rateTimes">The rate times.</param>
    /// <param name="exerciseTimes">The exercise times, the basis system's evolution.</param>
    /// <exception cref="ArgumentException">The times don't make an evolution.</exception>
    public SwapForwardBasisSystem(ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> exerciseTimes)
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

    private SwapForwardBasisSystem(SwapForwardBasisSystem other)
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

    /// <summary>
    /// The number of basis functions at each exercise: 10, 6 at an exercise on the second-to-last rate, 3 on the last.
    /// </summary>
    /// <returns>One count an exercise.</returns>
    public override ImmutableArray<int> NumberOfFunctions() =>
        [.. rateIndex.Select(r => r < rateTimes.Length - 3 ? 10 : r == rateTimes.Length - 3 ? 6 : 3)];

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

        if (index < rateTimes.Length - 3)
        {
            double x = currentState.ForwardRate(index);
            double y = currentState.CoterminalSwapRate(index + 1);
            double z = currentState.DiscountRatio(index, rateTimes.Length - 1);

            results[0] = 1.0;
            results[1] = x;
            results[2] = y;
            results[3] = z;
            results[4] = x * y;
            results[5] = y * z;
            results[6] = z * x;
            results[7] = x * x;
            results[8] = y * y;
            results[9] = z * z;
        }
        else if (index == rateTimes.Length - 3)
        {
            double x = currentState.ForwardRate(index);
            double y = currentState.ForwardRate(index + 1);
            results[0] = 1.0;
            results[1] = x;
            results[2] = y;
            results[3] = x * x;
            results[4] = x * y;
            results[5] = y * y;
        }
        else
        {
            double x = currentState.ForwardRate(index);
            results[0] = 1.0;
            results[1] = x;
            results[2] = x * x;
        }
    }

    /// <inheritdoc/>
    public override MarketModelBasisSystem Clone() => new SwapForwardBasisSystem(this);
}
