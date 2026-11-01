// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/proxygreekengine.hpp ql/models/marketmodels/proxygreekengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The engine that values market-model products and their Greeks by Fries and Joshi's partial proxy simulation: each
/// path is valued on the original evolver, which records each step's constrained rate, then on each bumped constrained
/// evolver pinned to those rates, its weights the likelihood ratio; finite-difference weights combine the original and
/// the bumped values into each Greek's path value.
/// </summary>
/// <remarks>
/// <para>
/// The Greeks: groups of constrained evolvers, each group with its lists of weights, one per Greek, the first weight
/// the original value's and the next ones the group's evolvers'. Each path adds the original values to the caller's
/// statistics and each Greek's values to its own, in units of the initial numeraire.
/// </para>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): it holds the caller's evolvers, as QuantLib's <c>shared_ptr</c>, and
/// steps them, so an evolver serves one engine at a time; it holds its own clone of the product. Its buffers are sized
/// at construction, so a path, the original's and the constrained evolvers', allocates nothing after a warm-up path
/// (MMPR1 (v)).
/// </para>
/// <para>
/// The constrained evolvers' weights are S-mm's evolver's, which since MMPR3 carry the reference model's transition
/// density: a volatility-bumped evolver given the original model as its reference yields vegas within their standard
/// errors of Black's, where QuantLib's are of the wrong sign (<c>Divergences/ProxyGreekVegaTests.cs</c>).
/// </para>
/// <para>
/// Refused, where QuantLib reads past its buffers: a group of weights without its evolvers, a list of weights longer
/// than its group's evolvers and the original, constraints fewer than the product's steps, statistics fewer than the
/// weights, and a product not finished at the evolver's last step (MMPR4's refusals).
/// </para>
/// </remarks>
[WorkingType]
public sealed class ProxyGreekEngine
{
    private readonly MarketModelEvolver originalEvolver;
    private readonly ConstrainedEvolver[][] constrainedEvolvers;
    private readonly double[][][] diffWeights;
    private readonly int[] startIndexOfConstraint;
    private readonly int[] endIndexOfConstraint;
    private readonly MarketModelMultiProduct product;
    private readonly double initialNumeraireValue;
    private readonly int numberProducts;

    // workspace
    private readonly double[] constraints;
    private readonly bool[] constraintsActive;
    private readonly double[] numerairesHeld;
    private readonly int[] numberCashFlowsThisStep;
    private readonly MarketModelMultiProductCashFlow[][] cashFlowsGenerated;
    private readonly MarketModelDiscounter[] discounters;
    private readonly double[] values;
    private readonly double[][][] modifiedValues;
    private readonly double[] results;

    /// <summary>Initializes a new instance of the <see cref="ProxyGreekEngine"/> class.</summary>
    /// <param name="evolver">The original evolver, held and stepped by the engine.</param>
    /// <param name="constrainedEvolvers">The groups of bumped constrained evolvers, their constraints' type set, held
    /// and stepped by the engine.</param>
    /// <param name="diffWeights">For each group, a list of weights per Greek: the original value's, then one per
    /// evolver of the group at most; copied.</param>
    /// <param name="startIndexOfConstraint">Each step's constrained rate's start, at least one per product step.</param>
    /// <param name="endIndexOfConstraint">Each step's constrained rate's end, at least one per product step.</param>
    /// <param name="product">The product, cloned.</param>
    /// <param name="initialNumeraireValue">The value of the initial numeraire bond today.</param>
    /// <exception cref="ArgumentException">
    /// A group of weights has no group of evolvers, a list of weights is empty or longer than its group's evolvers and
    /// the original, or the constraints are fewer than the product's steps.
    /// </exception>
    public ProxyGreekEngine(
        MarketModelEvolver evolver,
        IReadOnlyList<IReadOnlyList<ConstrainedEvolver>> constrainedEvolvers,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> diffWeights,
        ReadOnlySpan<int> startIndexOfConstraint,
        ReadOnlySpan<int> endIndexOfConstraint,
        MarketModelMultiProduct product,
        double initialNumeraireValue)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(constrainedEvolvers);
        ArgumentNullException.ThrowIfNull(diffWeights);
        ArgumentNullException.ThrowIfNull(product);
        originalEvolver = evolver;
        this.constrainedEvolvers = [.. constrainedEvolvers.Select(g => g.ToArray())];
        this.diffWeights = [.. diffWeights.Select(g => g.Select(w => w.ToArray()).ToArray())];
        this.startIndexOfConstraint = startIndexOfConstraint.ToArray();
        this.endIndexOfConstraint = endIndexOfConstraint.ToArray();
        this.product = product.Clone();
        this.initialNumeraireValue = initialNumeraireValue;
        numberProducts = this.product.NumberOfProducts;

        // QuantLib reads a group's evolvers' values by the weights' indices, and each step's constraint, unchecked
        if (this.diffWeights.Length > this.constrainedEvolvers.Length)
        {
            throw new ArgumentException(
                $"{this.diffWeights.Length} groups of weights given, more than the {this.constrainedEvolvers.Length} groups of evolvers",
                nameof(diffWeights));
        }

        for (int j = 0; j < this.diffWeights.Length; ++j)
        {
            foreach (double[] weights in this.diffWeights[j])
            {
                if (!(weights.Length >= 1 && weights.Length <= this.constrainedEvolvers[j].Length + 1))
                {
                    throw new ArgumentException(
                        $"group {j} has a list of {weights.Length} weights, not 1 to its {this.constrainedEvolvers[j].Length} evolvers plus 1",
                        nameof(diffWeights));
                }
            }
        }

        int steps = this.product.Evolution.NumberOfSteps;
        if (this.startIndexOfConstraint.Length < steps || this.endIndexOfConstraint.Length < steps)
        {
            throw new ArgumentException(
                $"the constraints' {this.startIndexOfConstraint.Length} starts and {this.endIndexOfConstraint.Length} ends are"
                + $" fewer than the product's {steps} steps",
                nameof(startIndexOfConstraint));
        }

        numerairesHeld = new double[numberProducts];
        numberCashFlowsThisStep = new int[numberProducts];
        cashFlowsGenerated = new MarketModelMultiProductCashFlow[numberProducts][];
        for (int i = 0; i < numberProducts; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelMultiProductCashFlow[this.product.MaxNumberOfCashFlowsPerProductPerStep];
        }

        ReadOnlySpan<double> cashFlowTimes = this.product.PossibleCashFlowTimes().AsSpan();
        ReadOnlySpan<double> rateTimes = this.product.Evolution.RateTimes.AsSpan();
        discounters = new MarketModelDiscounter[cashFlowTimes.Length];
        for (int j = 0; j < cashFlowTimes.Length; ++j)
        {
            discounters[j] = new MarketModelDiscounter(cashFlowTimes[j], rateTimes);
        }

        constraints = new double[steps];
        constraintsActive = new bool[steps];

        values = new double[numberProducts];
        results = new double[numberProducts];
        modifiedValues = new double[this.constrainedEvolvers.Length][][];
        for (int i = 0; i < modifiedValues.Length; ++i)
        {
            modifiedValues[i] = new double[this.constrainedEvolvers[i].Length][];
            for (int j = 0; j < modifiedValues[i].Length; ++j)
            {
                modifiedValues[i][j] = new double[numberProducts];
            }
        }
    }

    /// <summary>
    /// Values the products along paths: each path's original values added to <paramref name="stats"/>, and each Greek's
    /// values, its weights over the original and its group's evolvers' values, to its statistics.
    /// </summary>
    /// <param name="stats">The original values' statistics, of the products' dimension or of dimension 0.</param>
    /// <param name="modifiedStats">Each group's statistics, one per list of weights.</param>
    /// <param name="numberOfPaths">The number of paths.</param>
    /// <exception cref="ArgumentException">The statistics are fewer than the weights.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfPaths"/> is negative.</exception>
    /// <exception cref="NanoQLException">The product isn't finished at an evolver's last step.</exception>
    public void MultiplePathValues(
        SequenceStatisticsInc stats,
        IReadOnlyList<IReadOnlyList<SequenceStatisticsInc>> modifiedStats,
        int numberOfPaths)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(modifiedStats);
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfPaths);
        bool enough = modifiedStats.Count >= diffWeights.Length;
        for (int j = 0; j < diffWeights.Length && enough; ++j)
        {
            enough = modifiedStats[j].Count >= diffWeights[j].Length;
        }

        if (!enough)
        {
            throw new ArgumentException("the statistics are fewer than the weights", nameof(modifiedStats));
        }

        int n = numberProducts;
        for (int i = 0; i < numberOfPaths; ++i)
        {
            SinglePathValues(values, modifiedValues);
            stats.Add(values);

            for (int j = 0; j < diffWeights.Length; ++j)
            {
                for (int k = 0; k < diffWeights[j].Length; ++k)
                {
                    double[] weights = diffWeights[j][k];
                    for (int l = 0; l < n; ++l)
                    {
                        results[l] = weights[0] * values[l];
                        for (int m = 1; m < weights.Length; ++m)
                        {
                            results[l] += weights[m] * modifiedValues[j][m - 1][l];
                        }
                    }

                    modifiedStats[j][k].Add(results);
                }
            }
        }
    }

    /// <summary>
    /// Values the products along one path: on the original evolver, recording each step's constrained rate, then on
    /// each constrained evolver pinned to those rates.
    /// </summary>
    /// <param name="values">The original values, one per product, written.</param>
    /// <param name="modifiedValues">Each group's evolvers' values, one per product, written.</param>
    /// <exception cref="ArgumentException">A buffer is smaller than the products or the evolvers.</exception>
    /// <exception cref="NanoQLException">The product isn't finished at an evolver's last step.</exception>
    public void SinglePathValues(Span<double> values, double[][][] modifiedValues)
    {
        ArgumentNullException.ThrowIfNull(modifiedValues);
        bool fits = values.Length >= numberProducts && modifiedValues.Length >= constrainedEvolvers.Length;
        for (int i = 0; i < constrainedEvolvers.Length && fits; ++i)
        {
            fits = modifiedValues[i].Length >= constrainedEvolvers[i].Length;
            for (int j = 0; j < constrainedEvolvers[i].Length && fits; ++j)
            {
                fits = modifiedValues[i][j].Length >= numberProducts;
            }
        }

        if (!fits)
        {
            throw new ArgumentException("the values' buffers are smaller than the products or the evolvers", nameof(modifiedValues));
        }

        SingleEvolverValues(originalEvolver, values, true);
        for (int i = 0; i < constrainedEvolvers.Length; ++i)
        {
            for (int j = 0; j < constrainedEvolvers[i].Length; ++j)
            {
                constrainedEvolvers[i][j].SetThisConstraint(constraints, constraintsActive);
                SingleEvolverValues(constrainedEvolvers[i][j], modifiedValues[i][j]);
            }
        }
    }

    private void SingleEvolverValues(MarketModelEvolver evolver, Span<double> pathValues, bool storeRates = false)
    {
        Array.Clear(numerairesHeld);
        double weight = evolver.StartNewPath();
        product.Reset();
        double principalInNumerairePortfolio = 1.0;

        if (storeRates)
        {
            Array.Clear(constraintsActive);
        }

        bool done;
        do
        {
            int thisStep = evolver.CurrentStep;
            weight *= evolver.AdvanceStep();
            CurveState state = evolver.CurrentState;
            done = product.NextTimeStep(state, numberCashFlowsThisStep, cashFlowsGenerated);
            if (storeRates)
            {
                constraints[thisStep] = state.SwapRate(startIndexOfConstraint[thisStep], endIndexOfConstraint[thisStep]);
                constraintsActive[thisStep] = true;
            }

            int numeraire = evolver.Numeraires[thisStep];

            // for each product...
            for (int i = 0; i < numberProducts; ++i)
            {
                // ...and each cash flow...
                MarketModelMultiProductCashFlow[] cashflows = cashFlowsGenerated[i];
                for (int j = 0; j < numberCashFlowsThisStep[i]; ++j)
                {
                    // ...convert the cash flow to numeraires. This is done by calculating the number of numeraire
                    // bonds corresponding to such cash flow...
                    MarketModelDiscounter discounter = discounters[cashflows[j].TimeIndex];

                    double bonds = cashflows[j].Amount * discounter.NumeraireBonds(state, numeraire);

                    // ...and adding the newly bought bonds to the number of numeraires held.
                    numerairesHeld[i] += weight * bonds / principalInNumerairePortfolio;
                }
            }

            if (!done)
            {
                if (thisStep + 1 >= evolver.Numeraires.Length)
                {
                    throw new NanoQLException("the product isn't finished at the evolver's last step");
                }

                // The numeraire might change between steps. This implies that we might have to convert the numeraire
                // bonds for this step into a corresponding amount of numeraire bonds for the next step. This can be
                // done by changing the principal of the numeraire and updating the number of bonds in the numeraire
                // portfolio accordingly.
                int nextNumeraire = evolver.Numeraires[thisStep + 1];

                principalInNumerairePortfolio *= state.DiscountRatio(numeraire, nextNumeraire);
            }
        }
        while (!done);

        for (int i = 0; i < numerairesHeld.Length; ++i)
        {
            pathValues[i] = numerairesHeld[i] * initialNumeraireValue;
        }
    }
}
