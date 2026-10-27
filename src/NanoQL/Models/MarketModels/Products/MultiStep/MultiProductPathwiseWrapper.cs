// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multisteppathwisewrapper.hpp ql/models/marketmodels/products/multistep/multisteppathwisewrapper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A pathwise product as an ordinary one: its cash flows' amounts without their derivatives, so the product is written
/// once for both kinds of engine.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): it holds its own clone of the pathwise product.
/// </para>
/// <para>
/// <b>An already-deflated product</b> (MMPR4 (xvii), a documented divergence): its amounts are in units of the
/// money-market numeraire, so the wrapper divides each by its payment time's money-market deflator on the current
/// state (<see cref="MarketModelDiscounter"/> in bond 0, the factor the pathwise engine skips for it), giving the
/// ordinary engines an amount to discount. QuantLib's wrapper hands them the deflated amount
/// (<c>multisteppathwisewrapper.cpp:69</c>), which the accounting engine discounts again: wrapped deflated caplets 11.8%
/// below <see cref="MultiStepOptionlets"/> on the same paths (<c>Divergences/MultiProductPathwiseWrapperDeflatedTests.cs</c>).
/// A product not deflated gives QuantLib's amounts.
/// </para>
/// </remarks>
public sealed class MultiProductPathwiseWrapper : MarketModelMultiProduct
{
    private readonly MarketModelPathwiseMultiProduct innerProduct;
    private readonly MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated;
    private readonly MarketModelDiscounter[]? deflators;
    private readonly int numberOfProducts;

    /// <summary>Initializes a new instance of the <see cref="MultiProductPathwiseWrapper"/> class.</summary>
    /// <param name="innerProduct">The pathwise product, cloned.</param>
    public MultiProductPathwiseWrapper(MarketModelPathwiseMultiProduct innerProduct)
    {
        ArgumentNullException.ThrowIfNull(innerProduct);
        this.innerProduct = innerProduct.Clone();
        numberOfProducts = innerProduct.NumberOfProducts;
        int rates = innerProduct.Evolution.NumberOfRates;
        int max = innerProduct.MaxNumberOfCashFlowsPerProductPerStep;
        cashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[numberOfProducts][];
        for (int i = 0; i < numberOfProducts; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[max];
            for (int j = 0; j < max; ++j)
            {
                cashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(1 + rates);
            }
        }

        if (innerProduct.AlreadyDeflated)
        {
            ImmutableArray<double> times = innerProduct.PossibleCashFlowTimes();
            ImmutableArray<double> rateTimes = innerProduct.Evolution.RateTimes;
            deflators = new MarketModelDiscounter[times.Length];
            for (int k = 0; k < times.Length; ++k)
            {
                deflators[k] = new MarketModelDiscounter(times[k], rateTimes.AsSpan());
            }
        }
    }

    private MultiProductPathwiseWrapper(MultiProductPathwiseWrapper other)
    {
        innerProduct = other.innerProduct.Clone();
        numberOfProducts = other.numberOfProducts;
        deflators = other.deflators;
        cashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[other.cashFlowsGenerated.Length][];
        for (int i = 0; i < cashFlowsGenerated.Length; ++i)
        {
            cashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[other.cashFlowsGenerated[i].Length];
            for (int j = 0; j < cashFlowsGenerated[i].Length; ++j)
            {
                MarketModelPathwiseMultiProductCashFlow from = other.cashFlowsGenerated[i][j];
                cashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(from.Amount.Length);
                cashFlowsGenerated[i][j].CopyFrom(from);
            }
        }
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => innerProduct.Evolution;

    /// <inheritdoc/>
    public override int NumberOfProducts => innerProduct.NumberOfProducts;

    /// <inheritdoc/>
    public override int MaxNumberOfCashFlowsPerProductPerStep => innerProduct.MaxNumberOfCashFlowsPerProductPerStep;

    /// <inheritdoc/>
    public override ImmutableArray<int> SuggestedNumeraires() => innerProduct.SuggestedNumeraires();

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => innerProduct.PossibleCashFlowTimes();

    /// <inheritdoc/>
    public override void Reset() => innerProduct.Reset();

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        bool done = innerProduct.NextTimeStep(currentState, numberCashFlowsThisStep, this.cashFlowsGenerated);

        for (int i = 0; i < numberOfProducts; ++i)
        {
            for (int j = 0; j < numberCashFlowsThisStep[i]; ++j)
            {
                MarketModelPathwiseMultiProductCashFlow from = this.cashFlowsGenerated[i][j];
                cashFlowsGenerated[i][j].TimeIndex = from.TimeIndex;
                double amount = from.Amount[0];
                if (deflators is not null)
                {
                    amount /= deflators[from.TimeIndex].NumeraireBonds(currentState, 0);
                }

                cashFlowsGenerated[i][j].Amount = amount;
            }
        }

        return done;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiProductPathwiseWrapper(this);
}
