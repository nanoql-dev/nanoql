// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwiseaccountingengine.hpp ql/models/marketmodels/pathwiseaccountingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The engine that values pathwise market-model products, their deltas and their vegas along given bumps of the
/// pseudo-roots, along a log-normal Euler evolver's paths, by Giles and Glasserman's smoking adjoints: the delta engine's
/// recursion, with each step's rates differentiated along the bumps as the path goes and combined with the
/// derivatives as early as possible.
/// </summary>
/// <remarks>
/// <para>
/// The bumps: one list per step, each of the same number of bumps, each bump a matrix of the pseudo-root's size; a
/// vega is the change along a bump given at every step. Each product's results are its value, its R deltas and its
/// vegas, in units of the initial numeraire. It reads the model's pseudo-roots and displacements, and works in the
/// discretely compounding money-market account.
/// </para>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): it holds the caller's evolver and steps it, so an evolver serves one
/// engine at a time; it holds its own clone of the product and copies of the pseudo-roots and bumps (MMPR1 (iv)). Its
/// buffers are sized at construction, so its valuation allocates nothing after a warm-up (MMPR1 (v)), and it writes its
/// means and errors into the caller's spans, QuantLib's caller vectors.
/// </para>
/// <para>
/// Departures from QuantLib (MMPR4 (i), (ii), (iv), (v), documented divergences; <c>Divergences/PathwiseVegaTests.cs</c>):
/// the delta engine's recursion (<see cref="PathwiseAccountingEngine"/>); <see cref="RatePseudoRootJacobian"/>'s
/// derivative; and the Jacobian takes the one-step discount factors at the step's start, where QuantLib's hands it the
/// step's end (<c>pathwiseaccountingengine.cpp:494</c>) and its own test the start (<c>marketmodel.cpp:2899-2900</c>).
/// The prices are QuantLib's. Refused (MMPR5 (a)): an evolver not in the money-market measure; and, where QuantLib
/// reads past its buffers, a product whose rates or evolution aren't the model's, a product not finished at the
/// evolver's last step.
/// </para>
/// </remarks>
[WorkingType]
public sealed class PathwiseVegasAccountingEngine
{
    private readonly LogNormalFwdRateEuler evolver;
    private readonly MarketModelPathwiseMultiProduct product;
    private readonly double initialNumeraireValue;
    private readonly int numberProducts;
    private readonly int numberRates;
    private readonly int numberCashFlowTimes;
    private readonly int numberSteps;
    private readonly int numberBumps;
    private readonly int factors;
    private readonly bool doDeflation;
    private readonly double[] initialForwards;
    private readonly double[] taus;
    private readonly double[] displacements;
    private readonly Matrix[] pseudoRoots;
    private readonly RatePseudoRootJacobian[] jacobianComputers;

    // workspace
    private readonly double[] currentForwards;
    private readonly double[] lastForwards;
    private readonly double[] numerairesHeld;
    private readonly int[] numberCashFlowsThisStep;
    private readonly MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated;
    private readonly MarketModelPathwiseDiscounter[] discounters;

    // one V for each product, with components for each time step and rate
    private readonly Matrix[] v;

    private readonly Matrix liborRatios;
    private readonly Matrix discounts;
    private readonly Matrix stepsDiscountsSquared;
    private readonly double[] stepsDiscounts;
    private readonly Matrix liborRates;
    private readonly Matrix partials;

    // dimensions are product and which vega
    private readonly Matrix vegasThisPath;

    // dimensions are step, bump and rate
    private readonly Matrix[] jacobiansThisPaths;

    private readonly double[] deflatorAndDerivatives;
    private readonly double[] fullDerivatives;
    private readonly int[][] numberCashFlowsThisIndex;
    private readonly Matrix[] totalCashFlowsThisIndex;
    private readonly int[][] cashFlowIndicesThisStep;
    private readonly double[] values;
    private readonly double[] sums;
    private readonly double[] sumsqs;

    /// <summary>Initializes a new instance of the <see cref="PathwiseVegasAccountingEngine"/> class.</summary>
    /// <param name="evolver">The log-normal Euler evolver, in the money-market measure, held and stepped by the
    /// engine.</param>
    /// <param name="product">The product, on the model's rates and evolution, cloned.</param>
    /// <param name="pseudoRootStructure">The model the evolver evolves: its pseudo-roots and displacements.</param>
    /// <param name="vegaBumps">The bumps: a list per step, each of the same number of bumps of the pseudo-root's size;
    /// copied.</param>
    /// <param name="initialNumeraireValue">The value of the initial numeraire bond today.</param>
    /// <exception cref="ArgumentException">
    /// The evolver isn't in the money-market measure, the product's rates or evolution aren't the model's, or the bumps
    /// aren't a list per step of one size.
    /// </exception>
    public PathwiseVegasAccountingEngine(
        LogNormalFwdRateEuler evolver,
        MarketModelPathwiseMultiProduct product,
        MarketModel pseudoRootStructure,
        IReadOnlyList<IReadOnlyList<Matrix>> vegaBumps,
        double initialNumeraireValue)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(pseudoRootStructure);
        ArgumentNullException.ThrowIfNull(vegaBumps);
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

        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(evolution)];

        if (!(vegaBumps.Count == numberSteps))
        {
            throw new ArgumentException("we need one vector of vega bumps for each step.", nameof(vegaBumps));
        }

        numberBumps = vegaBumps[0].Count;

        jacobianComputers = new RatePseudoRootJacobian[numberSteps];
        jacobiansThisPaths = new Matrix[numberSteps];
        for (int i = 0; i < numberSteps; ++i)
        {
            int thisSize = vegaBumps[i].Count;
            if (!(thisSize == numberBumps))
            {
                throw new ArgumentException(
                    "We must have precisely the same number of bumps for each step.", nameof(vegaBumps));
            }

            jacobianComputers[i] = new RatePseudoRootJacobian(
                pseudoRoots[i], evolution.FirstAliveRate[i], numeraires[i], taus, vegaBumps[i], displacements);

            jacobiansThisPaths[i] = new Matrix(numberBumps, numberRates);
        }

        currentForwards = new double[numberRates];
        lastForwards = new double[numberRates];
        numerairesHeld = new double[numberProducts];
        numberCashFlowsThisStep = new int[numberProducts];
        deflatorAndDerivatives = new double[numberRates + 1];
        fullDerivatives = new double[numberRates];
        stepsDiscounts = new double[numberRates + 1];
        stepsDiscounts[0] = 1.0;
        vegasThisPath = new Matrix(numberProducts, numberBumps);
        values = new double[numberProducts * (1 + numberRates + numberBumps)];
        sums = new double[values.Length];
        sumsqs = new double[values.Length];

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
        cashFlowIndicesThisStep = PathwiseEngineFunctions.CashFlowIndicesThisStep(
            cashFlowTimes, this.product.Evolution.EvolutionTimes.AsSpan(), numberSteps);

        partials = new Matrix(factors, numberRates);
    }

    /// <summary>Gets the number of results: per product, its value, its deltas and its vegas.</summary>
    public int NumberOfResults => values.Length;

    /// <summary>
    /// Values the products, their deltas and their vegas along paths, and writes their means and the means' standard
    /// errors: per product, its value, its R deltas, then its vega along each bump.
    /// </summary>
    /// <param name="means">The means, <see cref="NumberOfResults"/> entries, written.</param>
    /// <param name="errors">The means' standard errors, <see cref="NumberOfResults"/> entries, written.</param>
    /// <param name="numberOfPaths">The number of paths.</param>
    /// <exception cref="ArgumentException"><paramref name="means"/> or <paramref name="errors"/> holds fewer than
    /// <see cref="NumberOfResults"/> entries.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfPaths"/> is negative.</exception>
    /// <exception cref="NanoQLException">The product isn't finished at the evolver's last step.</exception>
    public void MultiplePathValues(Span<double> means, Span<double> errors, int numberOfPaths)
    {
        PathwiseEngineFunctions.CheckResults(means, errors, values.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfPaths);
        Array.Clear(sums);
        Array.Clear(sumsqs);

        for (int i = 0; i < numberOfPaths; ++i)
        {
            SinglePathValues(values);
            for (int j = 0; j < values.Length; ++j)
            {
                sums[j] += values[j];
                sumsqs[j] += values[j] * values[j];
            }
        }

        PathwiseEngineFunctions.MeansAndErrors(sums, sumsqs, numberOfPaths, means, errors);
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

        vegasThisPath.AsSpan().Clear();

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
                // the one-step discount factors at the step's start (MMPR4 (i), (v)), the rates displaced (MMPR4 (ii))
                double x = 1.0 / (1.0 + (taus[i] * lastForwards[i]));
                stepsDiscounts[i + 1] = x;
                stepsDiscountsSquared[storeStep, i] = x * x;

                double d = displacements[i];
                liborRatios[storeStep, i] = (currentForwards[i] + d) / (lastForwards[i] + d);
                liborRates[storeStep, i] = currentForwards[i] + d;
                discounts[storeStep, i + 1] = state.DiscountRatio(i + 1, 0);
            }

            jacobianComputers[thisStep].GetBumps(
                lastForwards, stepsDiscounts, currentForwards, evolver.BrowniansThisStep(), jacobiansThisPaths[thisStep]);

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

                                fullDerivatives[i - 1] = thisDerivative;

                                v[j][stepToUse, i - 1] += thisDerivative; // zeroth row of V is t =0 not t_0
                            }

                            // ok we've got the derivatives and stored them, now add them to vegas
                            // this corresponds to the \frac{\partial F_n}[\partial theta} term
                            // we add the indirect terms later
                            Matrix jacobian = jacobiansThisPaths[stepToUse - 1];
                            for (int k = 0; k < numberBumps; ++k)
                            {
                                for (int i = 0; i < numberRates; ++i)
                                {
                                    vegasThisPath[j, k] += fullDerivatives[i] * jacobian[k, i];
                                }
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

                        // we've done the Vs now the vegas
                        if (nextStepIndex > 0)
                        {
                            Matrix jacobian = jacobiansThisPaths[nextStepIndex - 1];
                            for (int l = 0; l < numberBumps; ++l)
                            {
                                for (int j = 0; j < numberRates; ++j)
                                {
                                    vegasThisPath[i, l] += v[i][nextStepIndex, j] * jacobian[l, j];
                                }
                            }
                        }
                    }
                }
            }
        }

        // write answer into values
        int entriesPerProduct = 1 + numberRates + numberBumps;

        for (int i = 0; i < numberProducts; ++i)
        {
            pathValues[i * entriesPerProduct] = numerairesHeld[i] * initialNumeraireValue;
            for (int j = 0; j < numberRates; ++j)
            {
                pathValues[(i * entriesPerProduct) + 1 + j] = v[i][0, j] * initialNumeraireValue;
            }

            for (int k = 0; k < numberBumps; ++k)
            {
                pathValues[(i * entriesPerProduct) + numberRates + k + 1] = vegasThisPath[i, k] * initialNumeraireValue;
            }
        }

        // we have put the weight in already, this results in lower variance since weight changes along the path
        return 1.0;
    }
}
