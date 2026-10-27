// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/collectnodedata.hpp ql/models/marketmodels/callability/collectnodedata.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>The node-data collection for a market-model callable product's exercise: QuantLib's free function.</summary>
public static class CollectNodeDataFunctions
{
    private const string SubsetsTodo =
        "evolution times for product, basis system, rebate and control must be subsets of the passed evolution times";

    private const string ExerciseTimesTodo =
        "rebate, control and basis system must have the same exercise---not evolution---times";

    /// <summary>
    /// Collects, on each path, each exercise's node data: the exercise value, the data provider's values and the
    /// control's value at the exercise, and the product's cash flows from it to the next exercise, each in units of the
    /// numeraire portfolio; row 0 holds the cash flows before the first exercise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It steps the caller's evolver, product, provider, rebate and control, each reset at each path's start, as
    /// QuantLib's does; a path that ends before an exercise leaves the exercise's data invalid and zero.
    /// </para>
    /// <para>
    /// Refused, QuantLib's TODO (<c>collectnodedata.cpp:49-53</c>) narrowed to what it reads, each text the TODO and its
    /// remedy: an evolver whose steps aren't the product's (QuantLib throws "invalid index" from inside); a provider,
    /// rebate or control with an evolution time outside the product's (QuantLib drops an exercise off the product's
    /// times, so the later nodes hold earlier exercises' values); a provider whose exercise times aren't the rebate's;
    /// and a control whose evolution misses one of the rebate's exercise times. QuantLib's default nothing control,
    /// flagged at every rate time beside a rebate at a subset, is accepted, its node data the matched control's.
    /// </para>
    /// </remarks>
    /// <param name="evolver">The evolver, stepping at the product's times.</param>
    /// <param name="product">The product, one product.</param>
    /// <param name="dataProvider">The basis system or parametric exercise whose values are collected.</param>
    /// <param name="rebate">The exercise value.</param>
    /// <param name="control">The control.</param>
    /// <param name="numberOfPaths">The number of paths.</param>
    /// <returns>The node data: the exercises plus one rows of one entry a path.</returns>
    /// <exception cref="ArgumentException">
    /// The product isn't one product, or the evolver's, provider's, rebate's or control's times don't fit as above.
    /// </exception>
    public static NodeData[][] CollectNodeData(
        MarketModelEvolver evolver,
        MarketModelMultiProduct product,
        MarketModelNodeDataProvider dataProvider,
        MarketModelExerciseValue rebate,
        MarketModelExerciseValue control,
        int numberOfPaths)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(dataProvider);
        ArgumentNullException.ThrowIfNull(rebate);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfPaths);

        if (product.NumberOfProducts != 1)
        {
            throw new ArgumentException("a single product is required", nameof(product));
        }

        int[] numberCashFlowsThisStep = new int[1];
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated =
            [new MarketModelMultiProductCashFlow[product.MaxNumberOfCashFlowsPerProductPerStep]];

        EvolutionDescription evolution = product.Evolution;
        ImmutableArray<double> rateTimes = evolution.RateTimes;

        ImmutableArray<double> cashFlowTimes = product.PossibleCashFlowTimes();
        ImmutableArray<double> rebateTimes = rebate.PossibleCashFlowTimes();
        ImmutableArray<double> controlTimes = control.PossibleCashFlowTimes();

        MarketModelDiscounter[] productDiscounters = [.. cashFlowTimes.Select(t => new MarketModelDiscounter(t, rateTimes.AsSpan()))];
        MarketModelDiscounter[] rebateDiscounters = [.. rebateTimes.Select(t => new MarketModelDiscounter(t, rateTimes.AsSpan()))];
        MarketModelDiscounter[] controlDiscounters = [.. controlTimes.Select(t => new MarketModelDiscounter(t, rateTimes.AsSpan()))];

        ImmutableArray<int> numeraires = evolver.Numeraires;
        ImmutableArray<double> evolutionTimes = evolution.EvolutionTimes;
        if (numeraires.Length != evolutionTimes.Length)
        {
            throw new ArgumentException(
                $"{SubsetsTodo}: the evolver steps {numeraires.Length} times, the product {evolutionTimes.Length}; collect on an "
                + "evolver of the product's evolution, such as the call-specified product's with its callability disabled",
                nameof(evolver));
        }

        CheckSubset(evolutionTimes, dataProvider.Evolution.EvolutionTimes, "data provider", nameof(dataProvider));
        CheckSubset(evolutionTimes, rebate.Evolution.EvolutionTimes, "rebate", nameof(rebate));
        CheckSubset(evolutionTimes, control.Evolution.EvolutionTimes, "control", nameof(control));

        ImmutableArray<bool> isProductTime = UtilitiesFunctions.IsInSubset(evolutionTimes.AsSpan(), product.Evolution.EvolutionTimes.AsSpan());
        ImmutableArray<bool> isRebateTime = UtilitiesFunctions.IsInSubset(evolutionTimes.AsSpan(), rebate.Evolution.EvolutionTimes.AsSpan());
        ImmutableArray<bool> isControlTime = UtilitiesFunctions.IsInSubset(evolutionTimes.AsSpan(), control.Evolution.EvolutionTimes.AsSpan());
        ImmutableArray<bool> isBasisTime = UtilitiesFunctions.IsInSubset(evolutionTimes.AsSpan(), dataProvider.Evolution.EvolutionTimes.AsSpan());
        bool[] isExerciseTime = new bool[evolutionTimes.Length];
        ImmutableArray<bool> v = rebate.IsExerciseTime();
        int exercises = 0, idx = 0;
        for (int i = 0; i < evolutionTimes.Length; ++i)
        {
            if (isRebateTime[i] && v[idx++])
            {
                isExerciseTime[i] = true;
                exercises++;
            }
        }

        CheckExerciseTimes(evolutionTimes, isExerciseTime, dataProvider, control);

        ImmutableArray<int> numberOfData = dataProvider.NumberOfData();
        var collectedData = new NodeData[exercises + 1][];
        for (int i = 0; i < collectedData.Length; ++i)
        {
            collectedData[i] = new NodeData[numberOfPaths];
            for (int j = 0; j < numberOfPaths; ++j)
            {
                collectedData[i][j] = new NodeData();
            }
        }

        for (int i = 0; i < numberOfPaths; ++i)
        {
            evolver.StartNewPath();
            product.Reset();
            rebate.Reset();
            control.Reset();
            dataProvider.Reset();
            double principalInNumerairePortfolio = 1.0;

            bool done = false;
            int nextExercise = 0;
            collectedData[0][i].CumulatedCashFlows = 0.0;
            do
            {
                int currentStep = evolver.CurrentStep;
                evolver.AdvanceStep();
                CurveState currentState = evolver.CurrentState;
                int numeraire = numeraires[currentStep];

                if (isRebateTime[currentStep])
                {
                    rebate.NextStep(currentState);
                }

                if (isControlTime[currentStep])
                {
                    control.NextStep(currentState);
                }

                if (isBasisTime[currentStep])
                {
                    dataProvider.NextStep(currentState);
                }

                if (isExerciseTime[currentStep])
                {
                    NodeData data = collectedData[nextExercise + 1][i];

                    MarketModelMultiProductCashFlow exerciseValue = rebate.Value(currentState);
                    data.ExerciseValue = exerciseValue.Amount
                        * rebateDiscounters[exerciseValue.TimeIndex].NumeraireBonds(currentState, numeraire)
                        / principalInNumerairePortfolio;

                    data.Values = new double[numberOfData[nextExercise]];
                    dataProvider.Values(currentState, data.Values);

                    MarketModelMultiProductCashFlow controlValue = control.Value(currentState);
                    data.ControlValue = controlValue.Amount
                        * controlDiscounters[controlValue.TimeIndex].NumeraireBonds(currentState, numeraire)
                        / principalInNumerairePortfolio;

                    data.CumulatedCashFlows = 0.0;

                    data.IsValid = true;

                    ++nextExercise;
                }

                if (isProductTime[currentStep])
                {
                    done = product.NextTimeStep(currentState, numberCashFlowsThisStep, cashFlowsGenerated);

                    for (int j = 0; j < numberCashFlowsThisStep[0]; ++j)
                    {
                        MarketModelMultiProductCashFlow cf = cashFlowsGenerated[0][j];
                        collectedData[nextExercise][i].CumulatedCashFlows +=
                            cf.Amount * productDiscounters[cf.TimeIndex].NumeraireBonds(currentState, numeraire)
                            / principalInNumerairePortfolio;
                    }
                }

                if (!done)
                {
                    int nextNumeraire = numeraires[currentStep + 1];
                    principalInNumerairePortfolio *= currentState.DiscountRatio(numeraire, nextNumeraire);
                }
            }
            while (!done);

            // fill the remaining (un)collected data with nulls
            for (int j = nextExercise; j < exercises; ++j)
            {
                NodeData data = collectedData[j + 1][i];
                data.ExerciseValue = data.ControlValue = 0.0;
                data.CumulatedCashFlows = 0.0;
                data.IsValid = false;
            }
        }

        return collectedData;
    }

    private static void CheckSubset(ImmutableArray<double> productTimes, ImmutableArray<double> times, string what, string paramName)
    {
        foreach (double t in times)
        {
            if (!productTimes.Contains(t))
            {
                throw new ArgumentException(
                    $"{SubsetsTodo}: the {what}'s time {t} isn't the product's; collect on a product whose evolution holds it, "
                    + "such as a call-specified product, whose evolution holds its rebate's and its strategy's times",
                    paramName);
            }
        }
    }

    private static void CheckExerciseTimes(
        ImmutableArray<double> evolutionTimes,
        ReadOnlySpan<bool> isExerciseTime,
        MarketModelNodeDataProvider dataProvider,
        MarketModelExerciseValue control)
    {
        var rebateExercises = new List<double>();
        for (int i = 0; i < evolutionTimes.Length; ++i)
        {
            if (isExerciseTime[i])
            {
                rebateExercises.Add(evolutionTimes[i]);
            }
        }

        ImmutableArray<double> providerTimes = dataProvider.Evolution.EvolutionTimes;
        ImmutableArray<bool> providerFlags = dataProvider.IsExerciseTime();
        var providerExercises = new List<double>();
        for (int i = 0; i < providerTimes.Length; ++i)
        {
            if (providerFlags[i])
            {
                providerExercises.Add(providerTimes[i]);
            }
        }

        if (!providerExercises.SequenceEqual(rebateExercises))
        {
            throw new ArgumentException(
                $"{ExerciseTimesTodo}: the data provider's exercise times aren't the rebate's; give a provider exercised at "
                + "the rebate's exercise times",
                nameof(dataProvider));
        }

        ImmutableArray<double> controlTimes = control.Evolution.EvolutionTimes;
        foreach (double t in rebateExercises)
        {
            if (!controlTimes.Contains(t))
            {
                throw new ArgumentException(
                    $"{ExerciseTimesTodo}: the control's evolution misses the rebate's exercise time {t}; give a control "
                    + "whose evolution holds the rebate's exercise times",
                    nameof(control));
            }
        }
    }
}
