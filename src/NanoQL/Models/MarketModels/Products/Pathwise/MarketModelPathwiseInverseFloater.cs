// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductinversefloater.hpp ql/models/marketmodels/products/pathwise/pathwiseproductinversefloater.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// An inverse floater for the pathwise engines: <see cref="MultiStepInverseFloater"/>'s cash flows, with each coupon's
/// derivative with respect to its rate's forward.
/// </summary>
/// <remarks>A working type (the S-mmp plan's MMPR1 (i)).</remarks>
public sealed class MarketModelPathwiseInverseFloater : MarketModelPathwiseMultiProduct
{
    private readonly ImmutableArray<double> fixedAccruals;
    private readonly ImmutableArray<double> floatingAccruals;
    private readonly ImmutableArray<double> fixedStrikes;
    private readonly ImmutableArray<double> fixedMultipliers;
    private readonly ImmutableArray<double> floatingSpreads;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly double multiplier;
    private readonly int lastIndex;
    private readonly EvolutionDescription evolution;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseInverseFloater"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two, increasing.</param>
    /// <param name="fixedAccruals">The inverse floating leg's accruals, one a rate.</param>
    /// <param name="floatingAccruals">The floating leg's accruals, one a rate.</param>
    /// <param name="fixedStrikes">The inverse floating coupons' strikes, one a rate.</param>
    /// <param name="fixedMultipliers">The inverse floating coupons' multipliers, one a rate.</param>
    /// <param name="floatingSpreads">The floating coupons' spreads, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <param name="payer">Whether the inverse floater is paid.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or an input isn't one a rate.</exception>
    public MarketModelPathwiseInverseFloater(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> fixedAccruals,
        ReadOnlySpan<double> floatingAccruals,
        ReadOnlySpan<double> fixedStrikes,
        ReadOnlySpan<double> fixedMultipliers,
        ReadOnlySpan<double> floatingSpreads,
        ReadOnlySpan<double> paymentTimes,
        bool payer = true)
    {
        this.fixedAccruals = [.. fixedAccruals];
        this.floatingAccruals = [.. floatingAccruals];
        this.fixedStrikes = [.. fixedStrikes];
        this.fixedMultipliers = [.. fixedMultipliers];
        this.floatingSpreads = [.. floatingSpreads];
        this.paymentTimes = [.. paymentTimes];
        multiplier = payer ? -1.0 : 1.0;
        lastIndex = rateTimes.Length - 1;
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        Check(fixedAccruals.Length, nameof(fixedAccruals));
        Check(floatingAccruals.Length, nameof(floatingAccruals));
        Check(fixedStrikes.Length, nameof(fixedStrikes));
        Check(fixedMultipliers.Length, nameof(fixedMultipliers));
        Check(floatingSpreads.Length, nameof(floatingSpreads));
        Check(paymentTimes.Length, nameof(paymentTimes));

        evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }

    private MarketModelPathwiseInverseFloater(MarketModelPathwiseInverseFloater other)
    {
        fixedAccruals = other.fixedAccruals;
        floatingAccruals = other.floatingAccruals;
        fixedStrikes = other.fixedStrikes;
        fixedMultipliers = other.fixedMultipliers;
        floatingSpreads = other.floatingSpreads;
        paymentTimes = other.paymentTimes;
        multiplier = other.multiplier;
        lastIndex = other.lastIndex;
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
    public override ImmutableArray<int> SuggestedNumeraires() => [.. Enumerable.Range(0, lastIndex)];

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
        numberCashFlowsThisStep[0] = 1;
        MarketModelPathwiseMultiProductCashFlow flow = cashFlowsGenerated[0][0];
        Span<double> amount = flow.Amount;
        amount[1..(lastIndex + 1)].Clear();

        double liborRate = currentState.ForwardRate(currentIndex);
        double inverseFloatingCoupon =
            Math.Max(fixedStrikes[currentIndex] - (fixedMultipliers[currentIndex] * liborRate), 0.0) * fixedAccruals[currentIndex];
        double floatingCoupon = (liborRate + floatingSpreads[currentIndex]) * floatingAccruals[currentIndex];
        flow.TimeIndex = currentIndex;
        amount[0] = multiplier * (inverseFloatingCoupon - floatingCoupon);

        if (inverseFloatingCoupon > 0.0)
        {
            amount[currentIndex + 1] = multiplier
                * ((-fixedMultipliers[currentIndex] * fixedAccruals[currentIndex]) - floatingAccruals[currentIndex]);
        }
        else
        {
            amount[currentIndex + 1] = -multiplier * floatingAccruals[currentIndex];
        }

        ++currentIndex;

        return currentIndex == lastIndex;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseInverseFloater(this);

    private void Check(int given, string name)
    {
        if (given != lastIndex)
        {
            throw new ArgumentException($" Incorrect number of {name} given, should be {lastIndex} not {given}", name);
        }
    }
}
