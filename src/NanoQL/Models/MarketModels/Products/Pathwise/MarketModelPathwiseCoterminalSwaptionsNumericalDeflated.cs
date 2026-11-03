// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductswaption.hpp ql/models/marketmodels/products/pathwise/pathwiseproductswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The coterminal payer swaptions for the pathwise engines with numerical derivatives: as
/// <see cref="MarketModelPathwiseCoterminalSwaptionsDeflated"/>, each derivative a central difference of the swaption's
/// value on the state's forwards bumped up and down by the bump size.
/// </summary>
/// <remarks>
/// A working type: it holds its two bumped curve states and a forwards buffer, QuantLib's members, so a step allocates
/// nothing.
/// </remarks>
[WorkingType]
public sealed class MarketModelPathwiseCoterminalSwaptionsNumericalDeflated : MarketModelPathwiseMultiProduct
{
    private readonly ImmutableArray<double> rateTimes;
    private readonly ImmutableArray<double> strikes;
    private readonly int numberRates;
    private readonly double bumpSize;
    private readonly EvolutionDescription evolution;
    private readonly LMMCurveState up;
    private readonly LMMCurveState down;
    private readonly double[] forwards;
    private int currentIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketModelPathwiseCoterminalSwaptionsNumericalDeflated"/> class.
    /// </summary>
    /// <param name="rateTimes">The rate times, at least two, increasing.</param>
    /// <param name="strikes">The strikes, one a rate.</param>
    /// <param name="bumpSize">The forwards' bump.</param>
    /// <exception cref="ArgumentException">The rate times aren't increasing, or the strikes aren't one a rate.</exception>
    public MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(
        ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> strikes, double bumpSize)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        this.rateTimes = [.. rateTimes];
        this.strikes = [.. strikes];
        numberRates = rateTimes.Length - 1;
        this.bumpSize = bumpSize;
        if (strikes.Length != numberRates)
        {
            throw new ArgumentException("strikes.size()<> numberOfRates", nameof(strikes));
        }

        up = new LMMCurveState(rateTimes);
        down = new LMMCurveState(rateTimes);
        forwards = new double[numberRates];
        evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }

    private MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(
        MarketModelPathwiseCoterminalSwaptionsNumericalDeflated other)
    {
        rateTimes = other.rateTimes;
        strikes = other.strikes;
        numberRates = other.numberRates;
        bumpSize = other.bumpSize;
        evolution = other.evolution;
        up = (LMMCurveState)other.up.Clone();
        down = (LMMCurveState)other.down.Clone();
        forwards = (double[])other.forwards.Clone();
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
                currentState.ForwardRates().CopyTo(forwards);
                forwards[k] += bumpSize;
                up.SetOnForwardRates(forwards);

                forwards[k] -= bumpSize;
                forwards[k] -= bumpSize;
                down.SetOnForwardRates(forwards);

                double upSR = up.CoterminalSwapRate(currentIndex);
                double upAnnuity = up.CoterminalSwapAnnuity(currentIndex, currentIndex);
                double upValue = (upSR - strikes[currentIndex]) * upAnnuity;

                double downSR = down.CoterminalSwapRate(currentIndex);
                double downAnnuity = down.CoterminalSwapAnnuity(currentIndex, currentIndex);
                double downValue = (downSR - strikes[currentIndex]) * downAnnuity;

                double deriv = (upValue - downValue) / (2.0 * bumpSize);

                amount[k + 1] = deriv;
            }
        }

        ++currentIndex;
        return currentIndex == strikes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() =>
        new MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(this);
}
