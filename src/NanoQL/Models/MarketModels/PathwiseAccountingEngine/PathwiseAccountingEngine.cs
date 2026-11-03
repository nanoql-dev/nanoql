// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwiseaccountingengine.hpp ql/models/marketmodels/pathwiseaccountingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The engine that values pathwise market-model products and their deltas along a log-normal Euler evolver's paths,
/// by Giles and Glasserman's smoking adjoints: on each path it gathers the products' cash flows and their derivatives,
/// then carries the derivatives back to the initial forward rates through the Euler step's Jacobian.
/// </summary>
/// <remarks>
/// <para>
/// Each path adds the products' values, then each product's deltas, to the caller's statistics: product i's delta to
/// rate j at P + i·R + j, P products and R rates, in units of the initial numeraire, the path's weight in the values.
/// It reads the model's pseudo-roots and displacements, and works in the discretely compounding money-market account.
/// </para>
/// <para>
/// A working type: it holds the caller's evolver, as QuantLib's <c>shared_ptr</c>, and steps it, so an evolver serves
/// one engine at a time; it holds its own clone of the product and a copy of each step's pseudo-root. Its buffers are
/// sized at construction, so a path allocates nothing after a warm-up path.
/// </para>
/// <para>
/// Three departures from QuantLib (documented divergences; <c>Divergences/PathwiseDeltaTests.cs</c>): the Euler drift's
/// derivative reads the step's start forwards, where QuantLib's reads the step's end state
/// (<c>pathwiseaccountingengine.cpp:167</c>); the rates' ratios and the drift term carry the displacement, f + d, and
/// the drift's derivative its factor 1 − τd, where QuantLib's read f (<c>:169-170</c>, <c>:285</c>); and product i's
/// deltas sit at P + i·R + j, where QuantLib's (i + 1)·P + j overwrites another product's deltas when P ≠ R and writes
/// past the values when P &gt; R (<c>:306</c>). The prices are QuantLib's.
/// </para>
/// <para>
/// Refused: an evolver not in the money-market measure, which QuantLib requires and doesn't check (<c>:99</c>): the
/// adjoint recursion is the spot measure's. Refused too, where QuantLib reads past its buffers: a product whose rates
/// or evolution aren't the model's, and a product not finished at the evolver's last step.
/// </para>
/// </remarks>
[WorkingType]
public sealed class PathwiseAccountingEngine
{
    private readonly LogNormalFwdRateEuler evolver;
    private readonly MarketModelPathwiseMultiProduct product;
    private readonly double initialNumeraireValue;
    private readonly int numberProducts;
    private readonly int numberRates;
    private readonly int numberCashFlowTimes;
    private readonly int numberSteps;
    private readonly int factors;
    private readonly bool doDeflation;
    private readonly double[] initialForwards;
    private readonly double[] taus;
    private readonly double[] displacements;
    private readonly Matrix[] pseudoRoots;

    // workspace
    private readonly double[] currentForwards;
    private readonly double[] lastForwards;
    private readonly double[] numerairesHeld;
    private readonly int[] numberCashFlowsThisStep;
    private readonly MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated;
    private readonly MarketModelPathwiseDiscounter[] discounters;

    // one V for each product, with components for each time step and rate
    private readonly Matrix[] v;

    // dimensions are step and rate number
    private readonly Matrix liborRatios;

    // dimensions are step and rate number, goes from 0 to n. P(t_0, t_j)
    private readonly Matrix discounts;

    // dimensions are step and rate number
    private readonly Matrix stepsDiscountsSquared;

    // dimensions are step and rate number: the displaced rates
    private readonly Matrix liborRates;

    // dimensions are factor and rate
    private readonly Matrix partials;

    private readonly double[] deflatorAndDerivatives;
    private readonly int[][] numberCashFlowsThisIndex;

    // need product cross times cross which sensitivity
    private readonly Matrix[] totalCashFlowsThisIndex;

    private readonly int[][] cashFlowIndicesThisStep;
    private readonly double[] values;

    /// <summary>Initializes a new instance of the <see cref="PathwiseAccountingEngine"/> class.</summary>
    /// <param name="evolver">The log-normal Euler evolver, in the money-market measure, held and stepped by the
    /// engine.</param>
    /// <param name="product">The product, on the model's rates and evolution, cloned.</param>
    /// <param name="pseudoRootStructure">The model the evolver evolves: its pseudo-roots and displacements.</param>
    /// <param name="initialNumeraireValue">The value of the initial numeraire bond today.</param>
    /// <exception cref="ArgumentException">
    /// The evolver isn't in the money-market measure, or the product's rates or evolution aren't the model's.
    /// </exception>
    public PathwiseAccountingEngine(
        LogNormalFwdRateEuler evolver,
        MarketModelPathwiseMultiProduct product,
        MarketModel pseudoRootStructure,
        double initialNumeraireValue)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(pseudoRootStructure);
        this.evolver = evolver;
        this.product = product.Clone();
        this.initialNumeraireValue = initialNumeraireValue;
        numberProducts = this.product.NumberOfProducts;
        doDeflation = !this.product.AlreadyDeflated;
        numberRates = pseudoRootStructure.NumberOfRates;
        numberSteps = pseudoRootStructure.NumberOfSteps;
        factors = pseudoRootStructure.NumberOfFactors;
        EvolutionDescription evolution = pseudoRootStructure.Evolution;
        PathwiseEngineFunctions.CheckMoneyMarket(evolution, evolver.Numeraires.AsSpan());
        PathwiseEngineFunctions.CheckEvolution(this.product.Evolution, evolution);

        initialForwards = [.. pseudoRootStructure.InitialRates];
        taus = [.. evolution.RateTaus];
        displacements = [.. pseudoRootStructure.Displacements];
        pseudoRoots = new Matrix[numberSteps];
        for (int k = 0; k < numberSteps; ++k)
        {
            pseudoRoots[k] = pseudoRootStructure.PseudoRoot(k);
        }

        currentForwards = new double[numberRates];
        lastForwards = new double[numberRates];
        numerairesHeld = new double[numberProducts];
        numberCashFlowsThisStep = new int[numberProducts];
        deflatorAndDerivatives = new double[numberRates + 1];
        values = new double[numberProducts * (numberRates + 1)];

        discounts = new Matrix(numberSteps + 1, numberRates + 1);
        for (int i = 0; i <= numberSteps; ++i)
        {
            discounts[i, 0] = 1.0;
        }

        double[] cashFlowTimes = [.. this.product.PossibleCashFlowTimes()];
        numberCashFlowTimes = cashFlowTimes.Length;

        cashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[numberProducts][];
        numberCashFlowsThisIndex = new int[numberProducts][];
        v = new Matrix[numberProducts];
        totalCashFlowsThisIndex = new Matrix[numberProducts];
        for (int i = 0; i < numberProducts; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[this.product.MaxNumberOfCashFlowsPerProductPerStep];
            for (int j = 0; j < cashFlowsGenerated[i].Length; ++j)
            {
                cashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(numberRates + 1);
            }

            numberCashFlowsThisIndex[i] = new int[numberCashFlowTimes];
            v[i] = new Matrix(numberSteps + 1, numberRates);
            totalCashFlowsThisIndex[i] = new Matrix(numberCashFlowTimes, numberRates + 1);
        }

        liborRatios = new Matrix(numberSteps + 1, numberRates);
        stepsDiscountsSquared = new Matrix(numberSteps + 1, numberRates);
        liborRates = new Matrix(numberSteps + 1, numberRates);

        discounters = PathwiseEngineFunctions.Discounters(cashFlowTimes, this.product.Evolution.RateTimes.AsSpan());

        // we need to allocate cash-flow times to steps, i.e. what is the last step completed before a flow occurs
        // what we really need is for each step, what cash flow time indices to look at
        cashFlowIndicesThisStep = PathwiseEngineFunctions.CashFlowIndicesThisStep(
            cashFlowTimes, this.product.Evolution.EvolutionTimes.AsSpan(), numberSteps);

        partials = new Matrix(factors, numberRates);
    }

    /// <summary>
    /// Values the products and their deltas along paths, adding each path's values, then each product's deltas, to the
    /// statistics.
    /// </summary>
    /// <param name="stats">The statistics, of dimension P·(1 + R) or of dimension 0.</param>
    /// <param name="numberOfPaths">The number of paths.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfPaths"/> is negative.</exception>
    /// <exception cref="NanoQLException">The product isn't finished at the evolver's last step.</exception>
    public void MultiplePathValues(SequenceStatisticsInc stats, int numberOfPaths)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfPaths);
        for (int i = 0; i < numberOfPaths; ++i)
        {
            double weight = SinglePathValues(values);
            stats.Add(values, weight);
        }
    }

    private double SinglePathValues(Span<double> pathValues)
    {
        initialForwards.CopyTo(currentForwards, 0);

        // clear accumulation variables
        Array.Clear(numerairesHeld);
        for (int i = 0; i < numberProducts; ++i)
        {
            Array.Clear(numberCashFlowsThisIndex[i]);
            totalCashFlowsThisIndex[i].AsSpan().Clear();
            v[i].AsSpan().Clear();
        }

        double weight = evolver.StartNewPath();
        product.Reset();

        int thisStep;

        bool done;
        do
        {
            thisStep = evolver.CurrentStep;
            int storeStep = thisStep + 1;
            weight *= evolver.AdvanceStep();
            CurveState state = evolver.CurrentState;

            done = product.NextTimeStep(state, numberCashFlowsThisStep, cashFlowsGenerated);

            currentForwards.CopyTo(lastForwards, 0);
            state.ForwardRates().CopyTo(currentForwards);

            for (int i = 0; i < numberRates; ++i)
            {
                // the drift's derivative at the step's start, the rates displaced
                double x = 1.0 / (1.0 + (taus[i] * lastForwards[i]));
                stepsDiscountsSquared[storeStep, i] = x * x;

                double d = displacements[i];
                liborRatios[storeStep, i] = (currentForwards[i] + d) / (lastForwards[i] + d);
                liborRates[storeStep, i] = currentForwards[i] + d;
                discounts[storeStep, i + 1] = state.DiscountRatio(i + 1, 0);
            }

            // for each product...
            for (int i = 0; i < numberProducts; ++i)
            {
                // ...and each cash flow...
                for (int j = 0; j < numberCashFlowsThisStep[i]; ++j)
                {
                    MarketModelPathwiseMultiProductCashFlow cashFlow = cashFlowsGenerated[i][j];
                    int k = cashFlow.TimeIndex;
                    ++numberCashFlowsThisIndex[i][k];

                    Span<double> total = totalCashFlowsThisIndex[i].Row(k);
                    ReadOnlySpan<double> amount = cashFlow.Amount;
                    for (int l = 0; l <= numberRates; ++l)
                    {
                        total[l] += amount[l] * weight;
                    }
                }
            }

            PathwiseEngineFunctions.CheckNotFinished(done, thisStep, numberSteps);
        }
        while (!done);

        // ok we've gathered cash-flows, still have to backwards computation
        bool flowsFound = false;

        int finalStepDone = thisStep;

        for (int currentStep = numberSteps - 1; currentStep >= 0; --currentStep)
        {
            int stepToUse = Math.Min(currentStep, finalStepDone) + 1;

            foreach (int cashFlowIndex in cashFlowIndicesThisStep[currentStep])
            {
                // first check to see if anything actually happened before spending time on computing stuff
                bool noFlows = true;
                for (int l = 0; l < numberProducts && noFlows; ++l)
                {
                    noFlows = noFlows && (numberCashFlowsThisIndex[l][cashFlowIndex] == 0);
                }

                flowsFound = flowsFound || !noFlows;

                if (!noFlows)
                {
                    if (doDeflation)
                    {
                        // get amount to discount cash flow by and amount to multiply its derivatives by
                        discounters[cashFlowIndex].GetFactors(liborRates, discounts, stepToUse, deflatorAndDerivatives);
                    }

                    for (int j = 0; j < numberProducts; ++j)
                    {
                        if (numberCashFlowsThisIndex[j][cashFlowIndex] > 0)
                        {
                            ReadOnlySpan<double> total = totalCashFlowsThisIndex[j].Row(cashFlowIndex);
                            double deflatedCashFlow = total[0];
                            if (doDeflation)
                            {
                                deflatedCashFlow *= deflatorAndDerivatives[0];
                            }

                            numerairesHeld[j] += deflatedCashFlow;

                            for (int i = 1; i <= numberRates; ++i)
                            {
                                double thisDerivative = total[i];
                                if (doDeflation)
                                {
                                    thisDerivative *= deflatorAndDerivatives[0];
                                    thisDerivative += total[0] * deflatorAndDerivatives[i];
                                }

                                v[j][stepToUse, i - 1] += thisDerivative; // zeroth row of V is t =0 not t_0
                            }
                        }
                    }
                }
            }

            // need to do backwards updating
            if (flowsFound)
            {
                int nextStepToUse = Math.Min(currentStep - 1, finalStepDone);
                int nextStepIndex = nextStepToUse + 1;
                if (nextStepIndex != stepToUse)
                {
                    // then we need to update V
                    for (int i = 0; i < numberProducts; ++i)
                    {
                        PathwiseEngineFunctions.UpdateV(
                            v[i], pseudoRoots[currentStep], liborRates, liborRatios, stepsDiscountsSquared, partials, taus,
                            displacements, stepToUse, nextStepIndex);
                    }
                }
            }
        }

        // write answer into values: product i's deltas at P + i·R + j
        for (int i = 0; i < numberProducts; ++i)
        {
            pathValues[i] = numerairesHeld[i] * initialNumeraireValue;
            for (int j = 0; j < numberRates; ++j)
            {
                pathValues[numberProducts + (i * numberRates) + j] = v[i][0, j] * initialNumeraireValue;
            }
        }

        // we have put the weight in already, this results in lower variance since weight changes along the path
        return 1.0;
    }
}
