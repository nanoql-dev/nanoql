// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/parametricexercise.hpp ql/methods/montecarlo/parametricexercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>The early-exercise optimisation on collected node data: QuantLib's free function.</summary>
public static class ParametricExerciseFunctions
{
    /// <summary>
    /// Optimises each exercise's parameters, from the last exercise back, to maximise the valid paths' mean value, the
    /// exercise value where the rule exercises and the cash flows after it otherwise; adds each valid path's value at
    /// an exercise into its cash flows at the exercise before; the estimate is the paths' mean at the start.
    /// </summary>
    /// <remarks>
    /// The data are QuantLib's: n exercises give n + 1 rows, row 0 each path's cash flows before the first exercise and
    /// row i + 1 its data at exercise i. The cash flows of every row before the last are changed in place, as QuantLib
    /// changes them. Each optimisation starts from the rule's guess, unconstrained.
    /// </remarks>
    /// <param name="simulationData">The node data, an exercise a row and a path a column, changed in place.</param>
    /// <param name="exercise">The exercise rule.</param>
    /// <param name="endCriteria">The criteria that end each optimisation.</param>
    /// <param name="method">The optimisation method.</param>
    /// <returns>The biased estimate and each exercise's parameters.</returns>
    /// <exception cref="ArgumentException">
    /// There's no row, or a row is shorter than the one before it, which QuantLib reads past.
    /// </exception>
    /// <exception cref="NanoQLException">No path is valid at an exercise.</exception>
    public static GenericEarlyExerciseOptimizationResults GenericEarlyExerciseOptimization(
        NodeData[][] simulationData, IParametricExercise exercise, EndCriteria endCriteria, OptimizationMethod method)
    {
        ArgumentNullException.ThrowIfNull(simulationData);
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(endCriteria);
        ArgumentNullException.ThrowIfNull(method);
        int steps = simulationData.Length;
        if (steps == 0)
        {
            throw new ArgumentException("the simulation data must hold the cash flows before the first exercise", nameof(simulationData));
        }

        for (int i = 0; i < steps; ++i)
        {
            ArgumentNullException.ThrowIfNull(simulationData[i], nameof(simulationData));
            // QuantLib updates the row before over its own paths, reading this row's (parametricexercise.cpp:113-118)
            if (i > 0 && simulationData[i].Length < simulationData[i - 1].Length)
            {
                throw new ArgumentException(
                    $"row {i} holds {simulationData[i].Length} paths, fewer than row {i - 1}'s {simulationData[i - 1].Length}",
                    nameof(simulationData));
            }
        }

        var parameters = new ImmutableArray<double>[steps - 1];
        ImmutableArray<int> numberOfParameters = exercise.NumberOfParameters();

        for (int i = steps - 1; i != 0; --i)
        {
            NodeData[] exerciseData = simulationData[i];

            double[] current = new double[numberOfParameters[i - 1]];

            // optimize
            var f = new ValueEstimate(exerciseData, exercise, i - 1);

            exercise.Guess(i - 1, current);

            var c = new NoConstraint();

            var p = new Problem(f, c, current);
            ProblemResults result = method.Minimize(p, endCriteria);

            result.CurrentValue.AsSpan().CopyTo(current);
            parameters[i - 1] = [.. current];

            NodeData[] previousData = simulationData[i - 1];
            for (int j = 0; j < previousData.Length; ++j)
            {
                if (exerciseData[j].IsValid)
                {
                    if (exercise.Exercise(i - 1, current, exerciseData[j].Values))
                    {
                        previousData[j].CumulatedCashFlows += exerciseData[j].ExerciseValue;
                    }
                    else
                    {
                        previousData[j].CumulatedCashFlows += exerciseData[j].CumulatedCashFlows;
                    }
                }
            }
        }

        double sum = 0.0;
        NodeData[] initialData = simulationData[0];
        foreach (NodeData d in initialData)
        {
            sum += d.CumulatedCashFlows;
        }

        return new GenericEarlyExerciseOptimizationResults(sum / initialData.Length, [.. parameters]);
    }
}

// The valid paths' mean value at an exercise under given parameters, negated for the minimisation.
file sealed class ValueEstimate : CostFunction
{
    private readonly NodeData[] simulationData;
    private readonly IParametricExercise exercise;
    private readonly int exerciseIndex;
    private readonly double[] parameters;

    public ValueEstimate(NodeData[] simulationData, IParametricExercise exercise, int exerciseIndex)
    {
        this.simulationData = simulationData;
        this.exercise = exercise;
        this.exerciseIndex = exerciseIndex;
        parameters = new double[exercise.NumberOfParameters()[exerciseIndex]];
        foreach (NodeData i in simulationData)
        {
            if (i.IsValid)
            {
                return;
            }
        }

        throw new NanoQLException("no valid paths");
    }

    public override double Value(Vector x)
    {
        x.AsSpan().CopyTo(parameters);
        double sum = 0.0;
        int n = 0;
        foreach (NodeData i in simulationData)
        {
            if (i.IsValid)
            {
                ++n;
                if (exercise.Exercise(exerciseIndex, parameters, i.Values))
                {
                    sum += i.ExerciseValue;
                }
                else
                {
                    sum += i.CumulatedCashFlows;
                }
            }
        }

        return -sum / n;
    }

    public override Vector Values(Vector x) => throw new NanoQLException("values method not implemented");
}
