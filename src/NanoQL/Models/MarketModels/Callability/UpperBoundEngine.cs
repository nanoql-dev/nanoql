// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/upperboundengine.hpp ql/models/marketmodels/callability/upperboundengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The Andersen-Broadie upper bound of a callable product: on each outer path the hedged portfolio's maximum over the
/// exercise dates, the hedge's unexercised value at each date from an inner simulation of the hedge's call-specified
/// product.
/// </summary>
/// <remarks>
/// <para>
/// A working type: it holds the caller's outer and inner evolvers by reference, one engine at a time, and clones of the
/// products, exercise values and strategy. An outer path allocates, as QuantLib's does: a curve-state copy a step and a
/// callable clone an inner simulation.
/// </para>
/// <para>
/// The exercise values (documented divergences): the two exercise-value parts are an internal adapter paying the
/// exercise value at every exercise date, done at its last step, where QuantLib reads them from an
/// <see cref="ExerciseAdapter"/> done at its first and relies on the composite stepping a finished part again; and no
/// date after the step at which the hedge ends is an exercise date, no inner simulation and no exercise value, a hedge
/// with no possible cash-flow times exempt, that date's inner evolver still taken in QuantLib's order. The end's own
/// date stays one (<c>Divergences/UpperBoundEngineTests.cs</c>).
/// </para>
/// <para>
/// Refused, QuantLib reading past them: fewer inner evolvers than the exercise dates before the last step.
/// </para>
/// </remarks>
[WorkingType]
public sealed class UpperBoundEngine
{
    private const int UnderlyingOffset = 0;
    private readonly MarketModelEvolver evolver;
    private readonly ImmutableArray<MarketModelEvolver> innerEvolvers;
    private readonly MultiProductComposite composite;
    private readonly double initialNumeraireValue;
    private readonly bool hedgeHasCashFlows;
    private readonly int underlyingSize;
    private readonly int rebateSize;
    private readonly int hedgeSize;
    private readonly int hedgeRebateSize;
    private readonly int rebateOffset;
    private readonly int hedgeOffset;
    private readonly int hedgeRebateOffset;
    private readonly int numberOfProducts;
    private readonly int numberOfSteps;
    private readonly ImmutableArray<bool> isExerciseTime;
    private readonly int[] numberCashFlowsThisStep;
    private readonly MarketModelMultiProductCashFlow[][] cashFlowsGenerated;
    private readonly ImmutableArray<MarketModelDiscounter> discounters;

    /// <summary>Initializes a new instance of the <see cref="UpperBoundEngine"/> class.</summary>
    /// <param name="evolver">The outer paths' evolver, held, on the engine's composite evolution.</param>
    /// <param name="innerEvolvers">The inner simulations' evolvers, held, one an exercise date before the last step.</param>
    /// <param name="underlying">The callable's underlying, cloned.</param>
    /// <param name="rebate">The callable's exercise value, cloned.</param>
    /// <param name="hedge">The hedge, cloned, on the underlying's rate and exercise times.</param>
    /// <param name="hedgeRebate">The hedge's exercise value, cloned.</param>
    /// <param name="hedgeStrategy">The hedge's exercise strategy, cloned.</param>
    /// <param name="initialNumeraireValue">The initial numeraire's value.</param>
    /// <exception cref="ArgumentException">The inner evolvers are fewer than the exercise dates before the last step.</exception>
    public UpperBoundEngine(
        MarketModelEvolver evolver,
        ReadOnlySpan<MarketModelEvolver> innerEvolvers,
        MarketModelMultiProduct underlying,
        MarketModelExerciseValue rebate,
        MarketModelMultiProduct hedge,
        MarketModelExerciseValue hedgeRebate,
        ExerciseStrategy<CurveState> hedgeStrategy,
        double initialNumeraireValue)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(rebate);
        ArgumentNullException.ThrowIfNull(hedge);
        ArgumentNullException.ThrowIfNull(hedgeRebate);
        ArgumentNullException.ThrowIfNull(hedgeStrategy);
        foreach (MarketModelEvolver e in innerEvolvers)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(innerEvolvers));
        }

        this.evolver = evolver;
        this.innerEvolvers = [.. innerEvolvers];
        this.initialNumeraireValue = initialNumeraireValue;
        hedgeHasCashFlows = !hedge.PossibleCashFlowTimes().IsEmpty;

        composite = new MultiProductComposite();
        composite.Add(underlying);
        composite.Add(new AllDatesExerciseAdapter(rebate));
        composite.Add(hedge);
        composite.Add(new AllDatesExerciseAdapter(hedgeRebate));
        composite.Add(new DecoratedHedge(new CallSpecifiedMultiProduct(hedge, hedgeStrategy, new ExerciseAdapter(hedgeRebate))));
        composite.FinalizeComposite();

        underlyingSize = underlying.NumberOfProducts;
        rebateOffset = underlyingSize;
        rebateSize = 1;
        hedgeOffset = underlyingSize + rebateSize;
        hedgeSize = hedge.NumberOfProducts;
        hedgeRebateOffset = underlyingSize + rebateSize + hedgeSize;
        hedgeRebateSize = 1;

        numberOfProducts = composite.NumberOfProducts;

        ImmutableArray<double> evolutionTimes = composite.Evolution.EvolutionTimes;
        numberOfSteps = evolutionTimes.Length;

        isExerciseTime = UtilitiesFunctions.IsInSubset(evolutionTimes.AsSpan(), hedgeStrategy.ExerciseTimes().AsSpan());
        int innerSimulations = 0;
        for (int k = 0; k < numberOfSteps - 1; ++k)
        {
            if (isExerciseTime[k])
            {
                ++innerSimulations;
            }
        }

        if (innerEvolvers.Length < innerSimulations)
        {
            throw new ArgumentException(
                $"{innerEvolvers.Length} inner evolvers given, fewer than the {innerSimulations} exercise dates before the last step",
                nameof(innerEvolvers));
        }

        numberCashFlowsThisStep = new int[numberOfProducts];
        cashFlowsGenerated = new MarketModelMultiProductCashFlow[numberOfProducts][];
        for (int i = 0; i < numberOfProducts; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelMultiProductCashFlow[composite.MaxNumberOfCashFlowsPerProductPerStep];
        }

        ImmutableArray<double> rateTimes = composite.Evolution.RateTimes;
        discounters = [.. composite.PossibleCashFlowTimes().Select(t => new MarketModelDiscounter(t, rateTimes.AsSpan()))];
    }

    /// <summary>Adds each outer path's upper-bound value, with its weight, to the statistics.</summary>
    /// <param name="stats">The statistics added to.</param>
    /// <param name="outerPaths">The number of outer paths.</param>
    /// <param name="innerPaths">The number of inner paths an inner simulation.</param>
    public void MultiplePathValues(GeneralStatistics stats, int outerPaths, int innerPaths)
    {
        ArgumentNullException.ThrowIfNull(stats);
        for (int i = 0; i < outerPaths; ++i)
        {
            (double value, double weight) = SinglePathValue(innerPaths);
            stats.Add(value, weight);
        }
    }

    /// <summary>One outer path's upper-bound value, in cash, and its weight.</summary>
    /// <param name="innerPaths">The number of inner paths an inner simulation.</param>
    /// <returns>The value and the weight.</returns>
    public (double Value, double Weight) SinglePathValue(int innerPaths)
    {
        var callable = (DecoratedHedge)composite.Item(4);
        ExerciseStrategy<CurveState> strategy = callable.Strategy;

        double maximumValue = double.MinValue;
        double numerairesHeld = 0.0;
        double weight = evolver.StartNewPath();
        callable.Clear();
        composite.Reset();
        callable.DisableCallability();
        double principalInNumerairePortfolio = 1.0;
        int exercise = 0;

        for (int k = 0; k < numberOfSteps; ++k)
        {
            weight *= evolver.AdvanceStep();

            bool hedgeEndedBefore = hedgeHasCashFlows && composite.IsPartDone(2);
            composite.NextTimeStep(evolver.CurrentState, numberCashFlowsThisStep, cashFlowsGenerated);

            // First, we accumulate cash flows from both the
            // underlying...
            double underlyingCashFlows =
                CollectCashFlows(k, principalInNumerairePortfolio, UnderlyingOffset, UnderlyingOffset + underlyingSize);

            // ...and the hedge
            double hedgeCashFlows = CollectCashFlows(k, principalInNumerairePortfolio, hedgeOffset, hedgeOffset + hedgeSize);

            // we do the same for the rebates, which the every-date
            // adapters pay at each exercise date
            double rebateCashFlow = CollectCashFlows(k, principalInNumerairePortfolio, rebateOffset, rebateOffset + rebateSize);

            double hedgeRebateCashFlow =
                CollectCashFlows(k, principalInNumerairePortfolio, hedgeRebateOffset, hedgeRebateOffset + hedgeRebateSize);

            numerairesHeld += underlyingCashFlows - hedgeCashFlows;

            // Second, we do the upper-bound thing, at each exercise date
            // up to the hedge's end
            if (isExerciseTime[k] && !hedgeEndedBefore)
            {
                double unexercisedHedgeValue = 0.0;

                if (k != numberOfSteps - 1)
                {
                    // Here, we setup the relevant inner evolver and
                    // the decorated callable hedge such that their
                    // Reset() method brings them to the current point
                    // rather than the beginning of the path.
                    MarketModelEvolver currentEvolver = innerEvolvers[exercise];
                    currentEvolver.SetInitialState(evolver.CurrentState);

                    callable.StopRecording();
                    callable.EnableCallability();
                    callable.Save();

                    // the result is in numeraire units
                    var engine = new AccountingEngine(currentEvolver, callable, 1.0);
                    var innerStats = new SequenceStatisticsInc(callable.NumberOfProducts);
                    engine.MultiplePathValues(innerStats, innerPaths);

                    ImmutableArray<double> values = innerStats.Mean();
                    double sum = 0.0;
                    foreach (double x in values)
                    {
                        sum += x;
                    }

                    unexercisedHedgeValue = sum / principalInNumerairePortfolio;

                    callable.DisableCallability();
                    callable.StartRecording();
                }

                // Now, we can calculate the total value of our hedged
                // portfolio...
                double portfolioValue = numerairesHeld;
                if (strategy.Exercise(evolver.CurrentState))
                {
                    // get the rebates...
                    portfolioValue += rebateCashFlow - hedgeRebateCashFlow;

                    // ...and reinvest to rehedge
                    numerairesHeld += unexercisedHedgeValue - hedgeRebateCashFlow;
                }
                else
                {
                    portfolioValue += rebateCashFlow - unexercisedHedgeValue;
                }

                // ...and use it to update the maximum value
                maximumValue = Math.Max(maximumValue, portfolioValue);
            }

            // QuantLib takes an inner evolver at each exercise date
            // before the last step, in order
            if (isExerciseTime[k] && k != numberOfSteps - 1)
            {
                ++exercise;
            }

            // Lastly, we do the homework for next step (if any)
            if (k < numberOfSteps - 1)
            {
                // The numeraire might change between steps. This implies
                // that we might have to convert the numeraire bonds for
                // this step into a corresponding amount of numeraire
                // bonds for the next step. This can be done by changing
                // the principal of the numeraire and updating the number
                // of bonds in the numeraire portfolio accordingly.
                int numeraire = evolver.Numeraires[k];
                int nextNumeraire = evolver.Numeraires[k + 1];

                principalInNumerairePortfolio *= evolver.CurrentState.DiscountRatio(numeraire, nextNumeraire);
            }
        }

        // finally, we update the maximum with the total accumulated
        // cash flows (in case we never exercised)
        maximumValue = Math.Max(maximumValue, numerairesHeld);

        // all done; we just convert the result back to cash
        maximumValue *= initialNumeraireValue;

        return (maximumValue, weight);
    }

    private double CollectCashFlows(int currentStep, double principalInNumerairePortfolio, int beginProduct, int endProduct)
    {
        int numeraire = evolver.Numeraires[currentStep];

        double numeraireUnits = 0.0;

        // For each product in range...
        for (int i = beginProduct; i < endProduct; ++i)
        {
            // ...and for each cash flow...
            MarketModelMultiProductCashFlow[] cashflows = cashFlowsGenerated[i];
            for (int j = 0; j < numberCashFlowsThisStep[i]; ++j)
            {
                // ...convert the cash flow to numeraires.  This is
                // done by calculating the number of numeraire bonds
                // corresponding to such cash flow...
                MarketModelDiscounter discounter = discounters[cashflows[j].TimeIndex];

                // ...and adding the newly bought bonds to the total
                numeraireUnits += cashflows[j].Amount * discounter.NumeraireBonds(evolver.CurrentState, numeraire);
            }
        }

        return numeraireUnits / principalInNumerairePortfolio;
    }
}

// The hedge's call-specified product, recording the outer path's states so that its reset replays them with its
// callability disabled, bringing an inner simulation's clone to the current point of the path.
file sealed class DecoratedHedge : CallSpecifiedMultiProduct
{
    private readonly List<CurveState> savedStates;
    private readonly int[] numberCashFlowsThisStep;
    private readonly MarketModelMultiProductCashFlow[][] cashFlowsGenerated;
    private int lastSavedStep;
    private bool recording;

    public DecoratedHedge(CallSpecifiedMultiProduct product)
        : base(product)
    {
        savedStates = new List<CurveState>(product.Evolution.NumberOfSteps);

        int n = product.NumberOfProducts;
        numberCashFlowsThisStep = new int[n];
        cashFlowsGenerated = new MarketModelMultiProductCashFlow[n][];
        for (int i = 0; i < n; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelMultiProductCashFlow[product.MaxNumberOfCashFlowsPerProductPerStep];
        }

        Clear();
    }

    // The saved states are never changed once saved, so a clone shares them.
    private DecoratedHedge(DecoratedHedge other)
        : base(other)
    {
        savedStates = [.. other.savedStates];
        numberCashFlowsThisStep = (int[])other.numberCashFlowsThisStep.Clone();
        cashFlowsGenerated = [.. other.cashFlowsGenerated.Select(f => (MarketModelMultiProductCashFlow[])f.Clone())];
        lastSavedStep = other.lastSavedStep;
        recording = other.recording;
    }

    public override void Reset()
    {
        base.Reset();
        DisableCallability();
        for (int i = 0; i < lastSavedStep; ++i)
        {
            base.NextTimeStep(savedStates[i], numberCashFlowsThisStep, cashFlowsGenerated);
        }

        EnableCallability();
    }

    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        if (recording)
        {
            savedStates.Add(currentState.Clone());
        }

        return base.NextTimeStep(currentState, numberCashFlowsThisStep, cashFlowsGenerated);
    }

    public override MarketModelMultiProduct Clone() => new DecoratedHedge(this);

    public void Save() => lastSavedStep = savedStates.Count;

    public void Clear()
    {
        lastSavedStep = 0;
        savedStates.Clear();
        recording = true;
    }

    public void StartRecording() => recording = true;

    public void StopRecording() => recording = false;
}

// An exercise value as a product paying the exercise value at every exercise date, done at its last step: the engine's
// exercise-value parts.
file sealed class AllDatesExerciseAdapter : MultiProductMultiStep
{
    private readonly MarketModelExerciseValue exercise;
    private readonly ImmutableArray<bool> isExerciseTime;
    private int currentIndex;

    public AllDatesExerciseAdapter(MarketModelExerciseValue exercise)
        : base(exercise.Evolution.RateTimes.AsSpan())
    {
        this.exercise = exercise.Clone();
        isExerciseTime = exercise.IsExerciseTime();
    }

    private AllDatesExerciseAdapter(AllDatesExerciseAdapter other)
        : base(other)
    {
        exercise = other.exercise.Clone();
        isExerciseTime = other.isExerciseTime;
        currentIndex = other.currentIndex;
    }

    public override EvolutionDescription Evolution => exercise.Evolution;

    public override int NumberOfProducts => 1;

    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    public override ImmutableArray<double> PossibleCashFlowTimes() => exercise.PossibleCashFlowTimes();

    public override void Reset()
    {
        exercise.Reset();
        currentIndex = 0;
    }

    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        numberCashFlowsThisStep.Clear();
        exercise.NextStep(currentState);
        if (isExerciseTime[currentIndex])
        {
            numberCashFlowsThisStep[0] = 1;
            cashFlowsGenerated[0][0] = exercise.Value(currentState);
        }

        ++currentIndex;
        return currentIndex == isExerciseTime.Length;
    }

    public override MarketModelMultiProduct Clone() => new AllDatesExerciseAdapter(this);
}
