// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/accountingengine.hpp ql/models/marketmodels/accountingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The engine that values a market-model product along an evolver's paths: on each path it steps the evolver and the
/// product together, converts each cash flow into numeraire bonds at its step, rolls the holding into the next step's
/// numeraire, and adds the products' values, in units of the initial numeraire, to the caller's statistics.
/// </summary>
/// <remarks>
/// <para>
/// A working type: it holds the caller's evolver, as QuantLib's <c>shared_ptr</c>, and steps it, so an evolver serves
/// one engine at a time; it holds its own clone of the product. Its buffers are sized at construction, so a path
/// allocates nothing after a warm-up path (<c>Conventions/MarketModelProductAllocationTests.cs</c>).
/// </para>
/// <para>
/// A product that isn't finished at the evolver's last step is refused, where QuantLib reads the numeraire past the
/// evolver's steps.
/// </para>
/// </remarks>
[WorkingType]
public sealed class AccountingEngine
{
    private readonly MarketModelEvolver evolver;
    private readonly MarketModelMultiProduct product;
    private readonly double initialNumeraireValue;
    private readonly int numberProducts;

    // workspace
    private readonly double[] numerairesHeld;
    private readonly int[] numberCashFlowsThisStep;
    private readonly MarketModelMultiProductCashFlow[][] cashFlowsGenerated;
    private readonly MarketModelDiscounter[] discounters;
    private readonly double[] values;

    /// <summary>Initializes a new instance of the <see cref="AccountingEngine"/> class.</summary>
    /// <param name="evolver">The evolver, held and stepped by the engine.</param>
    /// <param name="product">The product, cloned.</param>
    /// <param name="initialNumeraireValue">The value of the initial numeraire bond today.</param>
    public AccountingEngine(MarketModelEvolver evolver, MarketModelMultiProduct product, double initialNumeraireValue)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(product);
        this.evolver = evolver;
        this.product = product.Clone();
        this.initialNumeraireValue = initialNumeraireValue;
        numberProducts = this.product.NumberOfProducts;
        numerairesHeld = new double[numberProducts];
        numberCashFlowsThisStep = new int[numberProducts];
        values = new double[numberProducts];
        cashFlowsGenerated = new MarketModelMultiProductCashFlow[numberProducts][];
        for (int i = 0; i < numberProducts; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelMultiProductCashFlow[this.product.MaxNumberOfCashFlowsPerProductPerStep];
        }

        ReadOnlySpan<double> cashFlowTimes = this.product.PossibleCashFlowTimes().AsSpan();
        ReadOnlySpan<double> rateTimes = this.product.Evolution.RateTimes.AsSpan();
        discounters = new MarketModelDiscounter[cashFlowTimes.Length];
        for (int i = 0; i < cashFlowTimes.Length; ++i)
        {
            discounters[i] = new MarketModelDiscounter(cashFlowTimes[i], rateTimes);
        }
    }

    /// <summary>Values the products along paths, adding each path's values, with its weight, to the
    /// statistics.</summary>
    /// <param name="stats">The statistics, of the products' dimension or of dimension 0.</param>
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
        Array.Clear(numerairesHeld);
        double weight = evolver.StartNewPath();
        product.Reset();
        double principalInNumerairePortfolio = 1.0;

        bool done;
        do
        {
            int thisStep = evolver.CurrentStep;
            weight *= evolver.AdvanceStep();
            CurveState state = evolver.CurrentState;
            done = product.NextTimeStep(state, numberCashFlowsThisStep, cashFlowsGenerated);
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
                    numerairesHeld[i] += bonds / principalInNumerairePortfolio;
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

        return weight;
    }
}
