// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/parametricexerciseadapter.hpp ql/models/marketmodels/callability/parametricexerciseadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>
/// An exercise strategy from a parametric exercise rule and its parameters: at each exercise, the rule on the
/// exercise's parameters and the variables the rule reads in the state.
/// </summary>
/// <remarks>
/// A working type: it steps its own clone of the rule, and its variables' buffer is sized at construction, so a
/// decision allocates nothing.
/// </remarks>
[WorkingType]
public sealed class ParametricExerciseAdapter : ExerciseStrategy<CurveState>
{
    private readonly MarketModelParametricExercise exercise;
    private readonly ImmutableArray<ImmutableArray<double>> parameters;
    private readonly ImmutableArray<double> exerciseTimes;
    private readonly ImmutableArray<bool> isExerciseTime;
    private readonly ImmutableArray<int> numberOfVariables;
    private readonly double[] variables;
    private int currentStep;
    private int currentExercise;

    /// <summary>Initializes a new instance of the <see cref="ParametricExerciseAdapter"/> class.</summary>
    /// <param name="exercise">The rule, cloned.</param>
    /// <param name="parameters">Each exercise's parameters, the rule's count of them at least.</param>
    /// <exception cref="ArgumentException">
    /// The parameters are fewer than the exercises, or an exercise's fewer than the rule's count, which QuantLib reads
    /// past.
    /// </exception>
    public ParametricExerciseAdapter(MarketModelParametricExercise exercise, ReadOnlySpan<ImmutableArray<double>> parameters)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        this.exercise = exercise.Clone();
        this.parameters = [.. parameters];
        isExerciseTime = exercise.IsExerciseTime();
        numberOfVariables = exercise.NumberOfVariables();
        ImmutableArray<double> evolutionTimes = this.exercise.Evolution.EvolutionTimes;
        var times = new List<double>();
        for (int i = 0; i < evolutionTimes.Length; ++i)
        {
            if (isExerciseTime[i])
            {
                times.Add(evolutionTimes[i]);
            }
        }

        exerciseTimes = [.. times];

        ImmutableArray<int> numberOfParameters = exercise.NumberOfParameters();
        if (parameters.Length < exerciseTimes.Length)
        {
            throw new ArgumentException(
                $"{parameters.Length} exercises' parameters given, fewer than the {exerciseTimes.Length} exercises", nameof(parameters));
        }

        for (int i = 0; i < exerciseTimes.Length; ++i)
        {
            if (parameters[i].Length < numberOfParameters[i])
            {
                throw new ArgumentException(
                    $"exercise {i}'s {parameters[i].Length} parameters are fewer than the rule's {numberOfParameters[i]}", nameof(parameters));
            }
        }

        variables = new double[numberOfVariables.IsEmpty ? 0 : numberOfVariables.Max()];
    }

    private ParametricExerciseAdapter(ParametricExerciseAdapter other)
    {
        exercise = other.exercise.Clone();
        parameters = other.parameters;
        exerciseTimes = other.exerciseTimes;
        isExerciseTime = other.isExerciseTime;
        numberOfVariables = other.numberOfVariables;
        variables = (double[])other.variables.Clone();
        currentStep = other.currentStep;
        currentExercise = other.currentExercise;
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> ExerciseTimes() => exerciseTimes;

    /// <summary>The rule's evolution times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> RelevantTimes() => exercise.Evolution.EvolutionTimes;

    /// <inheritdoc/>
    public override void Reset()
    {
        exercise.Reset();
        currentStep = currentExercise = 0;
    }

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState)
    {
        exercise.NextStep(currentState);
        if (isExerciseTime[currentStep])
        {
            ++currentExercise;
        }

        ++currentStep;
    }

    /// <inheritdoc/>
    public override bool Exercise(CurveState currentState)
    {
        Span<double> v = variables.AsSpan(0, numberOfVariables[currentExercise - 1]);
        exercise.Values(currentState, v);
        return exercise.Exercise(currentExercise - 1, parameters[currentExercise - 1].AsSpan(), v);
    }

    /// <inheritdoc/>
    public override ExerciseStrategy<CurveState> Clone() => new ParametricExerciseAdapter(this);
}
