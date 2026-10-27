// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductcashrebate.hpp ql/models/marketmodels/products/pathwise/pathwiseproductcashrebate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The receipt of a fixed cash amount, once, for the pathwise engines: each step pays each product its amount for that
/// step, its derivatives 0, and finishes the path.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)); the amounts are copied at construction. A rebate stepped more often than
/// its payment times is refused, where QuantLib reads past its amounts (MMPR4's refusals).
/// </remarks>
public sealed class MarketModelPathwiseCashRebate : MarketModelPathwiseMultiProduct
{
    private readonly EvolutionDescription evolution;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly Matrix amounts;
    private readonly int numberOfProducts;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseCashRebate"/> class.</summary>
    /// <param name="evolution">The evolution, one evolution time per payment time.</param>
    /// <param name="paymentTimes">The payment times, increasing.</param>
    /// <param name="amounts">The amounts, a row per product and a column per payment time.</param>
    /// <param name="numberOfProducts">The number of products.</param>
    /// <exception cref="ArgumentException">
    /// The payment times aren't increasing, or the amounts or the evolution don't fit them and the products.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfProducts"/> is negative.</exception>
    public MarketModelPathwiseCashRebate(
        EvolutionDescription evolution, ReadOnlySpan<double> paymentTimes, Matrix amounts, int numberOfProducts)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfProducts);
        this.evolution = evolution;
        this.paymentTimes = [.. paymentTimes];
        this.amounts = amounts.Clone();
        this.numberOfProducts = numberOfProducts;

        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);

        if (amounts.Rows != numberOfProducts)
        {
            throw new ArgumentException(
                "the number of rows in the matrix must equal the number of products", nameof(amounts));
        }

        if (amounts.Columns != paymentTimes.Length)
        {
            throw new ArgumentException(
                "the number of columns in the matrix must equal the number of payment times", nameof(amounts));
        }

        if (evolution.EvolutionTimes.Length != paymentTimes.Length)
        {
            throw new ArgumentException(
                "the number of evolution times must equal the number of payment times", nameof(evolution));
        }
    }

    private MarketModelPathwiseCashRebate(MarketModelPathwiseCashRebate other)
    {
        evolution = other.evolution;
        paymentTimes = other.paymentTimes;
        amounts = other.amounts;
        numberOfProducts = other.numberOfProducts;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <inheritdoc/>
    public override int NumberOfProducts => numberOfProducts;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>Gets <see langword="false"/>.</summary>
    public override bool AlreadyDeflated => false;

    /// <summary>Refused, as QuantLib's: a rebate suggests no numeraire.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override ImmutableArray<int> SuggestedNumeraires() => throw new NanoQLException("not implemented (yet?)");

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => paymentTimes;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The rebate is stepped past its payment times.</exception>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        if (currentIndex >= paymentTimes.Length)
        {
            throw new NanoQLException("the rebate is stepped past its payment times");
        }

        for (int i = 0; i < numberOfProducts; ++i)
        {
            numberCashFlowsThisStep[i] = 1;
            MarketModelPathwiseMultiProductCashFlow flow = cashFlowsGenerated[i][0];
            Span<double> amount = flow.Amount;
            flow.TimeIndex = currentIndex;
            amount[0] = amounts[i, currentIndex];
            amount[1..(evolution.NumberOfRates + 1)].Clear();
        }

        ++currentIndex;
        return true;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseCashRebate(this);
}
