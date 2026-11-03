// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Caplets for the pathwise engines, already deflated: caplet i pays (fᵢ − Kᵢ) aᵢ P(Tᵢ₊₁)/P(T₀), in units of the
/// money-market numeraire, where positive, with its derivatives with respect to each forward up to fᵢ.
/// </summary>
/// <remarks>
/// <para>A working type.</para>
/// <para>
/// <b>The deflator's derivative</b> (a documented divergence): the derivative of P(Tᵢ₊₁)/P(T₀) with respect to fⱼ is
/// −τⱼ P(Tⱼ₊₁)/P(Tⱼ) times it, τⱼ the rate's own period. QuantLib multiplies by the caplet's accrual aⱼ
/// (<c>pathwiseproductcaplet.cpp:233</c>), right only where the accruals are the rates' periods: with accruals of 0.5 ×
/// 365/360 its deltas differ from the undeflated caplet's by 3.2e-5 on the same paths
/// (<c>Divergences/MarketModelPathwiseDeflatedCapletTests.cs</c>). Where the accruals are the periods, QuantLib's
/// amounts.
/// </para>
/// </remarks>
[WorkingType]
public sealed class MarketModelPathwiseMultiDeflatedCaplet : MarketModelPathwiseMultiProduct
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<double> strikes;
    private readonly int numberRates;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseMultiDeflatedCaplet"/> class.</summary>
    /// <param name="rateTimes">The rate times, increasing, one more than the accruals.</param>
    /// <param name="accruals">The accruals, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <param name="strikes">The strikes, one a rate.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs aren't one a rate.</exception>
    public MarketModelPathwiseMultiDeflatedCaplet(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        ReadOnlySpan<double> strikes)
    {
        this.rateTimes = [.. rateTimes];
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketModelPathwiseMultiDeflatedCaplet"/> class, every caplet on one
    /// strike.
    /// </summary>
    /// <param name="rateTimes">The rate times, increasing, one more than the accruals.</param>
    /// <param name="accruals">The accruals, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <param name="strike">The strike.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs aren't one a rate.</exception>
    public MarketModelPathwiseMultiDeflatedCaplet(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        double strike)
    {
        this.rateTimes = [.. rateTimes];
        this.accruals = [.. accruals];
        this.paymentTimes = [.. paymentTimes];
        numberRates = accruals.Length;
        strikes = [.. Enumerable.Repeat(strike, numberRates)];
        evolution = PathwiseCapletChecks.Evolution(rateTimes, accruals, paymentTimes);
    }

    private MarketModelPathwiseMultiDeflatedCaplet(MarketModelPathwiseMultiDeflatedCaplet other)
    {
        rateTimes = other.rateTimes;
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

    /// <summary>Gets <see langword="true"/>: the amounts are in units of the money-market numeraire.</summary>
    public override bool AlreadyDeflated => true;

    /// <summary>The money-market measure's numeraires.</summary>
    /// <returns>The numeraires' indices.</returns>
    public override ImmutableArray<int> SuggestedNumeraires() => [.. Enumerable.Range(0, numberRates)];

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
        amount[0] = (liborRate - strikes[currentIndex]) * accruals[currentIndex]
            * currentState.DiscountRatio(currentIndex + 1, 0);

        numberCashFlowsThisStep.Clear();

        if (amount[0] > 0)
        {
            numberCashFlowsThisStep[currentIndex] = 1;
            amount[1..(numberRates + 1)].Clear();

            amount[currentIndex + 1] = accruals[currentIndex] * currentState.DiscountRatio(currentIndex + 1, 0);

            for (int i = 0; i <= currentIndex; ++i)
            {
                double stepDF = currentState.DiscountRatio(i + 1, i);
                double tau = rateTimes[i + 1] - rateTimes[i];
                amount[i + 1] -= tau * stepDF * amount[0];
            }
        }

        ++currentIndex;
        return currentIndex == strikes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseMultiDeflatedCaplet(this);
}
