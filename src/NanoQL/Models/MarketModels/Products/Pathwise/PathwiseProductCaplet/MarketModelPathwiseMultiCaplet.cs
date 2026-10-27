// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Caplets for the pathwise engines, one product a rate: caplet i pays (fᵢ − Kᵢ) aᵢ at its payment time where positive,
/// with its derivative aᵢ with respect to fᵢ; mainly a test product.
/// </summary>
/// <remarks>A working type (the S-mmp plan's MMPR1 (i)).</remarks>
public sealed class MarketModelPathwiseMultiCaplet : MarketModelPathwiseMultiProduct
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<double> strikes;
    private readonly int numberRates;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseMultiCaplet"/> class.</summary>
    /// <param name="rateTimes">The rate times, increasing, one more than the accruals.</param>
    /// <param name="accruals">The accruals, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <param name="strikes">The strikes, one a rate.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs aren't one a rate.</exception>
    public MarketModelPathwiseMultiCaplet(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        ReadOnlySpan<double> strikes)
    {
        this.accruals = [.. accruals];
        this.paymentTimes = [.. paymentTimes];
        this.strikes = [.. strikes];
        numberRates = accruals.Length;
        evolution = PathwiseCapletChecks.Evolution(rateTimes, accruals, paymentTimes);
        if (strikes.Length != numberRates)
        {
            throw new ArgumentException("strikes.size()<> numberOfRates", nameof(strikes));
        }
    }

    private MarketModelPathwiseMultiCaplet(MarketModelPathwiseMultiCaplet other)
    {
        accruals = other.accruals;
        paymentTimes = other.paymentTimes;
        strikes = other.strikes;
        numberRates = other.numberRates;
        evolution = other.evolution;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>Gets the number of rates: a caplet on each.</summary>
    public override int NumberOfProducts => numberRates;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>Gets <see langword="false"/>.</summary>
    public override bool AlreadyDeflated => false;

    /// <summary>The money-market measure's numeraires, offset by one.</summary>
    /// <returns>The numeraires' indices.</returns>
    public override ImmutableArray<int> SuggestedNumeraires() => [.. Enumerable.Range(1, numberRates)];

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => paymentTimes;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        double liborRate = currentState.ForwardRate(currentIndex);
        MarketModelPathwiseMultiProductCashFlow flow = cashFlowsGenerated[currentIndex][0];
        Span<double> amount = flow.Amount;
        flow.TimeIndex = currentIndex;
        amount[0] = (liborRate - strikes[currentIndex]) * accruals[currentIndex];

        numberCashFlowsThisStep.Clear();

        if (amount[0] > 0)
        {
            numberCashFlowsThisStep[currentIndex] = 1;
            amount[1..(numberRates + 1)].Clear();
            amount[currentIndex + 1] = accruals[currentIndex];
        }

        ++currentIndex;
        return currentIndex == strikes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseMultiCaplet(this);
}
