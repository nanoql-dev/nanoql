// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/exerciseadapter.hpp ql/models/marketmodels/products/multistep/exerciseadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// An exercise value as a product: stepped at the exercise value's times, it pays the exercise value at its first
/// exercise time and is done there.
/// </summary>
/// <remarks>
/// A working type: it steps its own clone of the exercise value. A call-specified product's rebate in QuantLib's
/// callable cases.
/// </remarks>
[WorkingType]
public sealed class ExerciseAdapter : MultiProductMultiStep
{
    private readonly MarketModelExerciseValue exercise;
    private readonly int numberOfProducts;
    private readonly ImmutableArray<bool> isExerciseTime;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="ExerciseAdapter"/> class.</summary>
    /// <param name="exercise">The exercise value, cloned.</param>
    /// <param name="numberOfProducts">The number of products, the first paid.</param>
    /// <exception cref="ArgumentException">The exercise value's rate times are fewer than two.</exception>
    public ExerciseAdapter(MarketModelExerciseValue exercise, int numberOfProducts = 1)
        : base((exercise ?? throw new ArgumentNullException(nameof(exercise))).Evolution.RateTimes.AsSpan())
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfProducts);
        this.exercise = exercise.Clone();
        this.numberOfProducts = numberOfProducts;
        isExerciseTime = exercise.IsExerciseTime();
    }

    private ExerciseAdapter(ExerciseAdapter other)
        : base(other)
    {
        exercise = other.exercise.Clone();
        numberOfProducts = other.numberOfProducts;
        isExerciseTime = other.isExerciseTime;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets the exercise value's evolution.</summary>
    public override EvolutionDescription Evolution => exercise.Evolution;

    /// <inheritdoc/>
    public override int NumberOfProducts => numberOfProducts;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>Gets the exercise value the adapter steps: its own clone, which reading doesn't change.</summary>
    public MarketModelExerciseValue ExerciseValue => exercise;

    /// <summary>The exercise value's cash-flow times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => exercise.PossibleCashFlowTimes();

    /// <inheritdoc/>
    public override void Reset()
    {
        exercise.Reset();
        currentIndex = 0;
    }

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        numberCashFlowsThisStep.Clear();
        bool done = false;

        exercise.NextStep(currentState);
        if (isExerciseTime[currentIndex])
        {
            MarketModelMultiProductCashFlow cashflow = exercise.Value(currentState);
            numberCashFlowsThisStep[0] = 1;
            cashFlowsGenerated[0][0] = cashflow;
            done = true;
        }

        ++currentIndex;
        return done || currentIndex == isExerciseTime.Length;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new ExerciseAdapter(this);
}
