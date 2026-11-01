// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepoptionlets.hpp ql/models/marketmodels/products/multistep/multistepoptionlets.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Optionlets, one product a rate: product i pays its payoff of the forward fᵢ times τᵢ at its payment time, fixed at
/// the rate's reset.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)); the payoffs are shared, immutable. It refuses inputs QuantLib reads
/// past (MMPR4's refusals): no payoff, more payoffs than rates, and fewer accruals or payment times than payoffs.
/// </remarks>
[WorkingType]
public sealed class MultiStepOptionlets : MultiProductMultiStep
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<Payoff> payoffs;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepOptionlets"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="accruals">The accrual of each optionlet, at least as many as the payoffs.</param>
    /// <param name="paymentTimes">The payment time of each optionlet, increasing, at least as many as the
    /// payoffs.</param>
    /// <param name="payoffs">The payoff of each optionlet, one to the number of rates.</param>
    /// <exception cref="ArgumentException">
    /// The rate times or the payment times aren't increasing, or the inputs' sizes don't fit.
    /// </exception>
    /// <exception cref="ArgumentNullException">A payoff is null.</exception>
    public MultiStepOptionlets(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        ReadOnlySpan<Payoff> payoffs)
        : base(rateTimes)
    {
        foreach (Payoff p in payoffs)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(payoffs));
        }

        this.accruals = [.. accruals];
        this.paymentTimes = [.. paymentTimes];
        this.payoffs = [.. payoffs];
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        MultiStepProductChecks.Sizes(rateTimes.Length - 1, payoffs.Length, nameof(payoffs));
        MultiStepProductChecks.AtLeast(accruals.Length, payoffs.Length, nameof(accruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, payoffs.Length, nameof(paymentTimes));
    }

    private MultiStepOptionlets(MultiStepOptionlets other)
        : base(other)
    {
        accruals = other.accruals;
        paymentTimes = other.paymentTimes;
        payoffs = other.payoffs;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets the number of optionlets.</summary>
    public override int NumberOfProducts => payoffs.Length;

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
        double liborRate = currentState.ForwardRate(currentIndex);
        cashFlowsGenerated[currentIndex][0].TimeIndex = currentIndex;
        cashFlowsGenerated[currentIndex][0].Amount = payoffs[currentIndex].Evaluate(liborRate) * accruals[currentIndex];
        numberCashFlowsThisStep.Clear();
        numberCashFlowsThisStep[currentIndex] = 1;
        ++currentIndex;
        return currentIndex == payoffs.Length;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepOptionlets(this);
}
