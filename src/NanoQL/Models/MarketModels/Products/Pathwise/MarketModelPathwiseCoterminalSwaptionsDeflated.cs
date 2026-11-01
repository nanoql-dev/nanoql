// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductswaption.hpp ql/models/marketmodels/products/pathwise/pathwiseproductswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The coterminal payer swaptions for the pathwise engines, one product a rate: swaption i pays, at rate i's reset,
/// (Sᵢ − Kᵢ) Aᵢ in its first bond where positive, Sᵢ the coterminal swap rate and Aᵢ its annuity, with its analytic
/// derivatives with respect to the forwards from fᵢ.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)). Its cash-flow times are the rate times, as QuantLib's, and its name is
/// QuantLib's, though it reports itself not deflated.
/// </remarks>
[WorkingType]
public sealed class MarketModelPathwiseCoterminalSwaptionsDeflated : MarketModelPathwiseMultiProduct
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> strikes;
    private readonly int numberRates;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseCoterminalSwaptionsDeflated"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two, increasing.</param>
    /// <param name="strikes">The strikes, one a rate.</param>
    /// <exception cref="ArgumentException">The rate times aren't increasing, or the strikes aren't one a rate.</exception>
    public MarketModelPathwiseCoterminalSwaptionsDeflated(ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> strikes)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        this.rateTimes = [.. rateTimes];
        this.strikes = [.. strikes];
        numberRates = rateTimes.Length - 1;
        if (strikes.Length != numberRates)
        {
            throw new ArgumentException("strikes.size()<> numberOfRates", nameof(strikes));
        }

        evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }

    private MarketModelPathwiseCoterminalSwaptionsDeflated(MarketModelPathwiseCoterminalSwaptionsDeflated other)
    {
        rateTimes = other.rateTimes;
        strikes = other.strikes;
        numberRates = other.numberRates;
        evolution = other.evolution;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>Gets the number of rates: a swaption into the swap from each.</summary>
    public override int NumberOfProducts => numberRates;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>Gets <see langword="false"/>, as QuantLib's.</summary>
    public override bool AlreadyDeflated => false;

    /// <summary>The money-market measure's numeraires.</summary>
    /// <returns>The numeraires' indices.</returns>
    public override ImmutableArray<int> SuggestedNumeraires() => [.. Enumerable.Range(0, numberRates)];

    /// <summary>The rate times.</summary>
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
        double swapRate = currentState.CoterminalSwapRate(currentIndex);
        MarketModelPathwiseMultiProductCashFlow flow = cashFlowsGenerated[currentIndex][0];
        Span<double> amount = flow.Amount;
        flow.TimeIndex = currentIndex;

        double annuity = currentState.CoterminalSwapAnnuity(currentIndex, currentIndex);
        amount[0] = (swapRate - strikes[currentIndex]) * annuity;

        numberCashFlowsThisStep.Clear();

        if (amount[0] > 0)
        {
            numberCashFlowsThisStep[currentIndex] = 1;
            amount[1..(numberRates + 1)].Clear();

            for (int k = currentIndex; k < numberRates; ++k)
            {
                amount[k + 1] = (rateTimes[k + 1] - rateTimes[k]) * currentState.DiscountRatio(k + 1, currentIndex);

                double multiplier = -(rateTimes[k + 1] - rateTimes[k]) * currentState.DiscountRatio(k + 1, k);

                for (int l = k; l < numberRates; ++l)
                {
                    amount[k + 1] += (currentState.ForwardRate(l) - strikes[currentIndex]) * (rateTimes[l + 1] - rateTimes[l])
                        * multiplier * currentState.DiscountRatio(l + 1, currentIndex);
                }
            }
        }

        ++currentIndex;
        return currentIndex == strikes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseCoterminalSwaptionsDeflated(this);
}
