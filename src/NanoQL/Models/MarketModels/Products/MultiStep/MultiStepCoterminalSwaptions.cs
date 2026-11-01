// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepcoterminalswaptions.hpp ql/models/marketmodels/products/multistep/multistepcoterminalswaptions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.Models;

/// <summary>
/// The coterminal swaptions, one product a rate: swaption i pays, at rate i's reset, its payoff of the coterminal swap
/// rate from rate i times the swap's annuity in its first bond, at its payment time.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)); the payoffs are shared, immutable. Fewer payoffs or payment times than
/// rates are refused, where QuantLib reads past them (MMPR4's refusals).
/// </remarks>
[WorkingType]
public sealed class MultiStepCoterminalSwaptions : MultiProductMultiStep
{
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<StrikedTypePayoff> payoffs;
    private readonly int lastIndex;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepCoterminalSwaptions"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate at least.</param>
    /// <param name="payoffs">The payoffs of the swap rates, one a rate at least.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs are shorter than the rates.</exception>
    /// <exception cref="ArgumentNullException">A payoff is null.</exception>
    public MultiStepCoterminalSwaptions(
        ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> paymentTimes, ReadOnlySpan<StrikedTypePayoff> payoffs)
        : base(rateTimes)
    {
        foreach (StrikedTypePayoff p in payoffs)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(payoffs));
        }

        this.paymentTimes = [.. paymentTimes];
        this.payoffs = [.. payoffs];
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        lastIndex = rateTimes.Length - 1;
        MultiStepProductChecks.AtLeast(payoffs.Length, lastIndex, nameof(payoffs));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, lastIndex, nameof(paymentTimes));
    }

    private MultiStepCoterminalSwaptions(MultiStepCoterminalSwaptions other)
        : base(other)
    {
        paymentTimes = other.paymentTimes;
        payoffs = other.payoffs;
        lastIndex = other.lastIndex;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets the number of rates: a swaption into the swap from each.</summary>
    public override int NumberOfProducts => lastIndex;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => paymentTimes;

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        cashFlowsGenerated[currentIndex][0].TimeIndex = currentIndex;

        double swapRate = currentState.CoterminalSwapRate(currentIndex);
        double annuity = currentState.CoterminalSwapAnnuity(currentIndex, currentIndex);
        cashFlowsGenerated[currentIndex][0].Amount = payoffs[currentIndex].Evaluate(swapRate) * annuity;
        numberCashFlowsThisStep.Clear();
        numberCashFlowsThisStep[currentIndex] = 1;
        ++currentIndex;
        return currentIndex == lastIndex;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepCoterminalSwaptions(this);
}
