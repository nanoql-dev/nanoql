// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwiseaccountingengine.hpp ql/models/marketmodels/pathwiseaccountingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The engine that values pathwise market-model products, their deltas and their vegas along a log-normal Euler
/// evolver's paths, by Giles and Glasserman's smoking adjoints: <see cref="PathwiseVegasAccountingEngine"/>'s values, with
/// the derivatives taken with respect to every pseudo-root element and combined into the bumps' vegas as late as
/// possible, after the paths' means.
/// </summary>
/// <remarks>
/// <para>
/// Each product's results are its value, its R deltas and its vegas, in units of the initial numeraire;
/// <see cref="MultiplePathValuesElementary"/> gives the vegas with respect to every pseudo-root element, step by step,
/// rate by rate, factor by factor. The bumps' vegas are combinations of the elements' means, so they carry no standard
/// error: theirs are written as 0, as in QuantLib.
/// </para>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): it holds the caller's evolver and steps it, so an evolver serves one
/// engine at a time; it holds its own clone of the product and copies of the pseudo-roots and bumps (MMPR1 (iv)). Its
/// buffers are sized at construction, so its valuation allocates nothing after a warm-up (MMPR1 (v)), and it writes its
/// means and errors into the caller's spans, QuantLib's caller vectors.
/// </para>
/// <para>
/// Departures from QuantLib: <see cref="PathwiseVegasAccountingEngine"/>'s (MMPR4 (i), (ii), (iv), (v), MMPR5 (a);
/// <c>pathwiseaccountingengine.cpp:910-911</c>, <c>:913-914</c>, <c>:1043</c>), with
/// <see cref="RatePseudoRootJacobianAllElements"/>'s derivative.
/// </para>
/// </remarks>
[WorkingType]
public sealed class PathwiseVegasOuterAccountingEngine
{
    private readonly LogNormalFwdRateEuler evolver;
    private readonly MarketModelPathwiseMultiProduct product;
    private readonly Matrix[][] vegaBumps;
    private readonly double initialNumeraireValue;
    private readonly int numberProducts;
    private readonly int numberRates;
    private readonly int numberCashFlowTimes;
    private readonly int numberSteps;
    private readonly int factors;
    private readonly int numberBumps;
    private readonly int numberElementaryVegas;
    private readonly bool doDeflation;
    private readonly double[] initialForwards;
    private readonly double[] taus;
    private readonly double[] displacements;
    private readonly Matrix[] pseudoRoots;
    private readonly RatePseudoRootJacobianAllElements[] jacobianComputers;

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

    // dimensions are product, step, rate and factor
    private readonly Matrix[][] elementaryVegasThisPath;

    // dimensions are step, rate, rate and factor
    private readonly Matrix[][] jacobiansThisPaths;

    private readonly double[] deflatorAndDerivatives;
    private readonly int[][] numberCashFlowsThisIndex;
    private readonly Matrix[] totalCashFlowsThisIndex;
    private readonly int[][] cashFlowIndicesThisStep;
    private readonly double[] values;
    private readonly double[] sums;
    private readonly double[] sumsqs;
    private readonly double[] allMeans;
    private readonly double[] allErrors;

    /// <summary>Initializes a new instance of the <see cref="PathwiseVegasOuterAccountingEngine"/> class.</summary>
    /// <param name="evolver">The log-normal Euler evolver, in the money-market measure, held and stepped by the
    /// engine.</param>
    /// <param name="product">The product, on the model's rates and evolution, cloned.</param>
    /// <param name="pseudoRootStructure">The model the evolver evolves: its pseudo-roots and displacements.</param>
    /// <param name="vegaBumps">The bumps: a list per step, each of the same number of bumps of the pseudo-root's size;
    /// copied.</param>
    /// <param name="initialNumeraireValue">The value of the initial numeraire bond today.</param>
    /// <exception cref="ArgumentException">
    /// The evolver isn't in the money-market measure, the product's rates or evolution aren't the model's, or the bumps
    /// aren't a list per step of one size, each of the pseudo-root's.
    /// </exception>
    public PathwiseVegasOuterAccountingEngine(
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
            throw new ArgumentException("we need precisely one vector of vega bumps for each step.", nameof(vegaBumps));
        }

        numberBumps = vegaBumps[0].Count;

        // QuantLib reads every step's bumps at the bumps' count and the pseudo-root's size, unchecked
        this.vegaBumps = new Matrix[numberSteps][];
        for (int i = 0; i < numberSteps; ++i)
        {
            if (!(vegaBumps[i].Count == numberBumps))
            {
                throw new ArgumentException(
                    "We must have precisely the same number of bumps for each step.", nameof(vegaBumps));
            }

            this.vegaBumps[i] = new Matrix[numberBumps];
            for (int b = 0; b < numberBumps; ++b)
            {
                Matrix bump = vegaBumps[i][b];
                if (!(bump.Rows == numberRates && bump.Columns == factors))
                {
                    throw new ArgumentException(
                        $"step {i}'s bump {b} is {bump.Rows} by {bump.Columns}, not the pseudo-root's {numberRates} by {factors}",
                        nameof(vegaBumps));
                }

                this.vegaBumps[i][b] = bump.Clone();
            }
        }

        jacobianComputers = new RatePseudoRootJacobianAllElements[numberSteps];
        jacobiansThisPaths = new Matrix[numberSteps][];
        for (int i = 0; i < numberSteps; ++i)
        {
            jacobianComputers[i] = new RatePseudoRootJacobianAllElements(
                pseudoRoots[i], evolution.FirstAliveRate[i], numeraires[i], taus, displacements);

            // vector of vector of matrices to store jacobians of rates with respect to pseudo-root elements
            jacobiansThisPaths[i] = new Matrix[numberRates];
            for (int r = 0; r < numberRates; ++r)
            {
                jacobiansThisPaths[i][r] = new Matrix(numberRates, factors);
            }
        }

        currentForwards = new double[numberRates];
        lastForwards = new double[numberRates];
        numerairesHeld = new double[numberProducts];
        numberCashFlowsThisStep = new int[numberProducts];
        deflatorAndDerivatives = new double[numberRates + 1];
        stepsDiscounts = new double[numberRates + 1];
        stepsDiscounts[0] = 1.0;

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
        elementaryVegasThisPath = new Matrix[numberProducts][];
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
            elementaryVegasThisPath[i] = new Matrix[numberSteps];
            for (int j = 0; j < numberSteps; ++j)
            {
                elementaryVegasThisPath[i][j] = new Matrix(numberRates, factors);
            }
        }

        liborRatios = new Matrix(numberSteps + 1, numberRates);
        stepsDiscountsSquared = new Matrix(numberSteps + 1, numberRates);
        liborRates = new Matrix(numberSteps + 1, numberRates);

        discounters = PathwiseEngineFunctions.Discounters(cashFlowTimes, this.product.Evolution.RateTimes.AsSpan());

        // we need to allocate cash-flow times to steps, i.e. what is the last step completed before a flow occurs
        cashFlowIndicesThisStep = PathwiseEngineFunctions.CashFlowIndicesThisStep(
            cashFlowTimes, this.product.Evolution.EvolutionTimes.AsSpan(), numberSteps);

        partials = new Matrix(factors, numberRates);

        numberElementaryVegas = numberSteps * numberRates * factors;
        values = new double[numberProducts * (1 + numberRates + numberElementaryVegas)];
        sums = new double[values.Length];
        sumsqs = new double[values.Length];
        allMeans = new double[values.Length];
        allErrors = new double[values.Length];
    }

    /// <summary>Gets the number of results: per product, its value, its deltas and its vegas along the bumps.</summary>
    public int NumberOfResults => numberProducts * (1 + numberRates + numberBumps);

    /// <summary>
    /// Gets the number of elementary results: per product, its value, its deltas and its vegas with respect to every
    /// pseudo-root element.
    /// </summary>
    public int NumberOfElementaryResults => values.Length;

    /// <summary>
    /// Values the products, their deltas and their vegas along the bumps, and writes their means and the means'
    /// standard errors: per product, its value, its R deltas, then its vega along each bump, whose error is 0.
    /// </summary>
    /// <param name="means">The means, <see cref="NumberOfResults"/> entries, written.</param>
    /// <param name="errors">The standard errors, <see cref="NumberOfResults"/> entries, written.</param>
    /// <param name="numberOfPaths">The number of paths.</param>
    /// <exception cref="ArgumentException"><paramref name="means"/> or <paramref name="errors"/> holds fewer than
    /// <see cref="NumberOfResults"/> entries.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfPaths"/> is negative.</exception>
    /// <exception cref="NanoQLException">The product isn't finished at the evolver's last step.</exception>
    public void MultiplePathValues(Span<double> means, Span<double> errors, int numberOfPaths)
    {
        PathwiseEngineFunctions.CheckResults(means, errors, NumberOfResults);
        MultiplePathValuesElementary(allMeans, allErrors, numberOfPaths);
        int outDataPerProduct = 1 + numberRates + numberBumps;
        int inDataPerProduct = 1 + numberRates + numberElementaryVegas;

        for (int p = 0; p < numberProducts; ++p)
        {
            for (int i = 0; i < 1 + numberRates; ++i)
            {
                means[i + (p * outDataPerProduct)] = allMeans[i + (p * inDataPerProduct)];
                errors[i + (p * outDataPerProduct)] = allErrors[i + (p * inDataPerProduct)];
            }

            for (int bump = 0; bump < numberBumps; ++bump)
            {
                double thisVega = 0.0;
                for (int t = 0; t < numberSteps; ++t)
                {
                    Matrix b = vegaBumps[t][bump];
                    for (int r = 0; r < numberRates; ++r)
                    {
                        for (int f = 0; f < factors; ++f)
                        {
                            thisVega += b[r, f] * allMeans[(p * inDataPerProduct) + 1 + numberRates + (t * numberRates * factors) + (r * factors) + f];
                        }
                    }
                }

                means[(p * outDataPerProduct) + 1 + numberRates + bump] = thisVega;

                // post linear combinations, errors are not meaningful so don't attempt to compute s.e.s for vegas
                errors[(p * outDataPerProduct) + 1 + numberRates + bump] = 0.0;
            }
        }
    }

    /// <summary>
    /// Values the products, their deltas and their vegas with respect to every pseudo-root element, and writes their
    /// means and the means' standard errors: per product, its value, its R deltas, then its vega for each step, rate
    /// and factor, the factor varying fastest.
    /// </summary>
    /// <param name="means">The means, <see cref="NumberOfElementaryResults"/> entries, written.</param>
    /// <param name="errors">The standard errors, <see cref="NumberOfElementaryResults"/> entries, written.</param>
    /// <param name="numberOfPaths">The number of paths.</param>
    /// <exception cref="ArgumentException"><paramref name="means"/> or <paramref name="errors"/> holds fewer than
    /// <see cref="NumberOfElementaryResults"/> entries.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfPaths"/> is negative.</exception>
    /// <exception cref="NanoQLException">The product isn't finished at the evolver's last step.</exception>
    public void MultiplePathValuesElementary(Span<double> means, Span<double> errors, int numberOfPaths)
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

        // all V matrices computed we now compute the elementary vegas for this path
        for (int i = 0; i < numberProducts; ++i)
        {
            for (int j = 0; j < numberSteps; ++j)
            {
                int nextIndex = j + 1;
                ReadOnlySpan<double> vNext = v[i].Row(nextIndex);
                Matrix[] jacobians = jacobiansThisPaths[j];
                Matrix vegas = elementaryVegasThisPath[i][j];

                // we know V, we need to pair against the senstivity of the rate to the elementary vega
                // note the simplification here arising from the fact that the elementary vega affects the evolution on
                // precisely one step
                for (int k = 0; k < numberRates; ++k)
                {
                    for (int f = 0; f < factors; ++f)
                    {
                        double sensitivity = 0.0;
                        for (int r = 0; r < numberRates; ++r)
                        {
                            sensitivity += vNext[r] * jacobians[r][k, f];
                        }

                        vegas[k, f] = sensitivity;
                    }
                }
            }
        }

        // write answer into values
        int entriesPerProduct = 1 + numberRates + numberElementaryVegas;

        for (int i = 0; i < numberProducts; ++i)
        {
            pathValues[i * entriesPerProduct] = numerairesHeld[i] * initialNumeraireValue;
            for (int j = 0; j < numberRates; ++j)
            {
                pathValues[(i * entriesPerProduct) + 1 + j] = v[i][0, j] * initialNumeraireValue;
            }

            for (int k = 0; k < numberSteps; ++k)
            {
                for (int l = 0; l < numberRates; ++l)
                {
                    for (int m = 0; m < factors; ++m)
                    {
                        pathValues[(i * entriesPerProduct) + numberRates + 1 + m + (l * factors) + (k * numberRates * factors)] =
                            elementaryVegasThisPath[i][k][l, m] * initialNumeraireValue;
                    }
                }
            }
        }

        // we have put the weight in already, this results in lower variance since weight changes along the path
        return 1.0;
    }
}
