// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/lsstrategy.hpp ql/models/marketmodels/callability/lsstrategy.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>
/// The Longstaff-Schwartz exercise strategy: at each exercise, exercises where the exercise value, in units of the
/// numeraire portfolio, reaches the continuation value the regression's coefficients estimate on the basis functions,
/// plus the control's value.
/// </summary>
/// <remarks>
/// <para>
/// A working type: it steps its own clones of the basis system, the exercise value and the control, and sizes every
/// buffer at construction, so a decision allocates nothing.
/// </para>
/// <para>
/// Refused, QuantLib reading past them: a basis system with fewer exercises than the exercise value's, fewer
/// coefficient vectors than exercises, or an exercise's coefficients more than its basis functions.
/// </para>
/// </remarks>
[WorkingType]
public sealed class LongstaffSchwartzExerciseStrategy : ExerciseStrategy<CurveState>
{
    private readonly MarketModelBasisSystem basisSystem;
    private readonly ImmutableArray<ImmutableArray<double>> basisCoefficients;
    private readonly MarketModelExerciseValue exercise;
    private readonly MarketModelExerciseValue control;
    private readonly ImmutableArray<int> numeraires;
    private readonly ImmutableArray<double> exerciseTimes;
    private readonly ImmutableArray<double> relevantTimes;
    private readonly ImmutableArray<bool> isBasisTime;
    private readonly ImmutableArray<bool> isRebateTime;
    private readonly ImmutableArray<bool> isControlTime;
    private readonly ImmutableArray<MarketModelDiscounter> rebateDiscounters;
    private readonly ImmutableArray<MarketModelDiscounter> controlDiscounters;
    private readonly double[][] basisValues;
    private readonly ImmutableArray<int> exerciseIndex;
    private int currentIndex;
    private double principalInNumerairePortfolio;
    private double newPrincipal;

    /// <summary>Initializes a new instance of the <see cref="LongstaffSchwartzExerciseStrategy"/> class.</summary>
    /// <param name="basisSystem">The basis functions, cloned.</param>
    /// <param name="basisCoefficients">Each exercise's coefficients, the regression's.</param>
    /// <param name="evolution">The evolution the strategy is stepped on.</param>
    /// <param name="numeraires">The numeraires, one a step.</param>
    /// <param name="exercise">The exercise value, cloned.</param>
    /// <param name="control">The control, cloned.</param>
    /// <exception cref="ArgumentException">
    /// The numeraires don't fit the evolution, or the basis system or coefficients are too few for the exercises, or an
    /// exercise's coefficients more than its basis functions.
    /// </exception>
    public LongstaffSchwartzExerciseStrategy(
        MarketModelBasisSystem basisSystem,
        ImmutableArray<ImmutableArray<double>> basisCoefficients,
        EvolutionDescription evolution,
        ReadOnlySpan<int> numeraires,
        MarketModelExerciseValue exercise,
        MarketModelExerciseValue control)
    {
        ArgumentNullException.ThrowIfNull(basisSystem);
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(control);
        this.basisSystem = basisSystem.Clone();
        this.basisCoefficients = basisCoefficients.IsDefault ? [] : basisCoefficients;
        this.exercise = exercise.Clone();
        this.control = control.Clone();
        this.numeraires = [.. numeraires];

        EvolutionDescriptionFunctions.CheckCompatibility(evolution, numeraires);
        relevantTimes = evolution.EvolutionTimes;

        isBasisTime = UtilitiesFunctions.IsInSubset(relevantTimes.AsSpan(), this.basisSystem.Evolution.EvolutionTimes.AsSpan());
        isRebateTime = UtilitiesFunctions.IsInSubset(relevantTimes.AsSpan(), this.exercise.Evolution.EvolutionTimes.AsSpan());
        isControlTime = UtilitiesFunctions.IsInSubset(relevantTimes.AsSpan(), this.control.Evolution.EvolutionTimes.AsSpan());

        int[] indices = new int[relevantTimes.Length];
        var times = new List<double>();
        ImmutableArray<bool> v = this.exercise.IsExerciseTime();
        int exercises = 0, idx = 0;
        for (int i = 0; i < relevantTimes.Length; ++i)
        {
            indices[i] = exercises;
            if (isRebateTime[i] && v[idx++])
            {
                times.Add(relevantTimes[i]);
                ++exercises;
            }
        }

        exerciseIndex = ImmutableArray.Create(indices);
        exerciseTimes = [.. times];

        ImmutableArray<double> rateTimes = evolution.RateTimes;
        rebateDiscounters = [.. this.exercise.PossibleCashFlowTimes().Select(t => new MarketModelDiscounter(t, rateTimes.AsSpan()))];
        controlDiscounters = [.. this.control.PossibleCashFlowTimes().Select(t => new MarketModelDiscounter(t, rateTimes.AsSpan()))];

        ImmutableArray<int> basisSizes = this.basisSystem.NumberOfFunctions();
        if (this.basisSystem.NumberOfExercises < exercises)
        {
            throw new ArgumentException(
                $"the basis system has {this.basisSystem.NumberOfExercises} exercises, fewer than the exercise value's {exercises}",
                nameof(basisSystem));
        }

        if (this.basisCoefficients.Length < exercises)
        {
            throw new ArgumentException(
                $"{this.basisCoefficients.Length} exercises' coefficients given, fewer than the {exercises} exercises", nameof(basisCoefficients));
        }

        for (int i = 0; i < exercises; ++i)
        {
            if (this.basisCoefficients[i].Length > basisSizes[i])
            {
                throw new ArgumentException(
                    $"exercise {i}'s {this.basisCoefficients[i].Length} coefficients are more than its {basisSizes[i]} basis functions",
                    nameof(basisCoefficients));
            }
        }

        basisValues = new double[this.basisSystem.NumberOfExercises][];
        for (int i = 0; i < basisValues.Length; ++i)
        {
            basisValues[i] = new double[basisSizes[i]];
        }
    }

    private LongstaffSchwartzExerciseStrategy(LongstaffSchwartzExerciseStrategy other)
    {
        basisSystem = other.basisSystem.Clone();
        basisCoefficients = other.basisCoefficients;
        exercise = other.exercise.Clone();
        control = other.control.Clone();
        numeraires = other.numeraires;
        exerciseTimes = other.exerciseTimes;
        relevantTimes = other.relevantTimes;
        isBasisTime = other.isBasisTime;
        isRebateTime = other.isRebateTime;
        isControlTime = other.isControlTime;
        rebateDiscounters = other.rebateDiscounters;
        controlDiscounters = other.controlDiscounters;
        basisValues = [.. other.basisValues.Select(b => (double[])b.Clone())];
        exerciseIndex = other.exerciseIndex;
        currentIndex = other.currentIndex;
        principalInNumerairePortfolio = other.principalInNumerairePortfolio;
        newPrincipal = other.newPrincipal;
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> ExerciseTimes() => exerciseTimes;

    /// <summary>The evolution's times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> RelevantTimes() => relevantTimes;

    /// <inheritdoc/>
    public override void Reset()
    {
        exercise.Reset();
        control.Reset();
        basisSystem.Reset();
        currentIndex = 0;
        principalInNumerairePortfolio = newPrincipal = 1.0;
    }

    /// <inheritdoc/>
    public override bool Exercise(CurveState currentState)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        int index = exerciseIndex[currentIndex - 1];

        MarketModelMultiProductCashFlow exerciseCF = exercise.Value(currentState);
        double exerciseValue = exerciseCF.Amount
            * rebateDiscounters[exerciseCF.TimeIndex].NumeraireBonds(currentState, numeraires[currentIndex - 1])
            / principalInNumerairePortfolio;

        MarketModelMultiProductCashFlow controlCF = control.Value(currentState);
        double controlValue = controlCF.Amount
            * controlDiscounters[controlCF.TimeIndex].NumeraireBonds(currentState, numeraires[currentIndex - 1])
            / principalInNumerairePortfolio;

        double[] values = basisValues[index];
        basisSystem.Values(currentState, values);

        ImmutableArray<double> alphas = basisCoefficients[index];
        double continuationValue = controlValue;
        for (int k = 0; k < alphas.Length; ++k)
        {
            continuationValue += alphas[k] * values[k];
        }

        return exerciseValue >= continuationValue;
    }

    /// <inheritdoc/>
    public override void NextStep(CurveState currentState)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        principalInNumerairePortfolio = newPrincipal;

        if (isRebateTime[currentIndex])
        {
            exercise.NextStep(currentState);
        }

        if (isControlTime[currentIndex])
        {
            control.NextStep(currentState);
        }

        if (isBasisTime[currentIndex])
        {
            basisSystem.NextStep(currentState);
        }

        if (currentIndex < numeraires.Length - 1)
        {
            int numeraire = numeraires[currentIndex];
            int nextNumeraire = numeraires[currentIndex + 1];
            newPrincipal *= currentState.DiscountRatio(numeraire, nextNumeraire);
        }

        ++currentIndex;
    }

    /// <inheritdoc/>
    public override ExerciseStrategy<CurveState> Clone() => new LongstaffSchwartzExerciseStrategy(this);
}
