// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductswap.hpp ql/models/marketmodels/products/pathwise/pathwiseproductswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A swap for the pathwise engines: at each rate's reset, (fᵢ − Kᵢ) τᵢ times the multiplier, paid at the period's end,
/// with its derivative τᵢ times the multiplier with respect to fᵢ.
/// </summary>
/// <remarks>
/// <para>
/// A working type. Its cash-flow times are the rate times, the first unused, as QuantLib's.
/// </para>
/// <para>
/// <b>One strike for every rate</b> (a documented divergence): the strikes' check reads the strikes expanded from one,
/// as its text offers ("does not equal numberOfRates or 1"). QuantLib's reads the parameter
/// (<c>pathwiseproductswap.cpp:59</c>), so it refuses the one strike it has just expanded
/// (<c>Divergences/MarketModelPathwiseSwapStrikeTests.cs</c>). With a strike a rate, QuantLib's swap.
/// </para>
/// </remarks>
[WorkingType]
public sealed class MarketModelPathwiseSwap : MarketModelPathwiseMultiProduct
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> strikes;
    private readonly int numberRates;
    private readonly double multiplier;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseSwap"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two, increasing.</param>
    /// <param name="accruals">The accruals, one a rate or one for every rate.</param>
    /// <param name="strikes">The strikes, one a rate or one for every rate.</param>
    /// <param name="multiplier">The multiplier: 1 a payer of the strike, −1 a receiver.</param>
    /// <exception cref="ArgumentException">
    /// The rate times aren't increasing, or the accruals or strikes aren't one a rate or one.
    /// </exception>
    public MarketModelPathwiseSwap(
        ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> accruals, ReadOnlySpan<double> strikes, double multiplier = 1.0)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        this.rateTimes = [.. rateTimes];
        numberRates = rateTimes.Length - 1;
        this.multiplier = multiplier;

        this.strikes = strikes.Length == 1 ? [.. Enumerable.Repeat(strikes[0], numberRates)] : [.. strikes];
        this.accruals = accruals.Length == 1 ? [.. Enumerable.Repeat(accruals[0], numberRates)] : [.. accruals];

        if (this.accruals.Length != numberRates)
        {
            throw new ArgumentException("accruals.size() does not equal numberOfRates or 1", nameof(accruals));
        }

        if (this.strikes.Length != numberRates)
        {
            throw new ArgumentException("strikes.size() does not equal numberOfRates or 1", nameof(strikes));
        }

        evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }

    private MarketModelPathwiseSwap(MarketModelPathwiseSwap other)
    {
        rateTimes = other.rateTimes;
        accruals = other.accruals;
        strikes = other.strikes;
        numberRates = other.numberRates;
        multiplier = other.multiplier;
        evolution = other.evolution;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>Gets 1.</summary>
    public override int NumberOfProducts => 1;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>Gets <see langword="false"/>.</summary>
    public override bool AlreadyDeflated => false;

    /// <summary>The money-market measure's numeraires.</summary>
    /// <returns>The numeraires' indices.</returns>
    public override ImmutableArray<int> SuggestedNumeraires() => [.. Enumerable.Range(0, numberRates)];

    /// <summary>The rate times, the first not a cash-flow time: QuantLib keeps it to keep the indices simple.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => rateTimes;

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
        MarketModelPathwiseMultiProductCashFlow flow = cashFlowsGenerated[0][0];
        Span<double> amount = flow.Amount;
        flow.TimeIndex = currentIndex + 1;

        amount[0] = (liborRate - strikes[currentIndex]) * accruals[currentIndex] * multiplier;

        numberCashFlowsThisStep[0] = 1;

        amount[1..(numberRates + 1)].Clear();

        amount[currentIndex + 1] = accruals[currentIndex] * multiplier;

        ++currentIndex;
        return currentIndex == strikes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseSwap(this);
}
