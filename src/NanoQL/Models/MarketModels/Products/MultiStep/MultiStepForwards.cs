// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepforwards.hpp ql/models/marketmodels/products/multistep/multistepforwards.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Forward-rate agreements, one product a rate: product i pays (fᵢ − Kᵢ) τᵢ at its payment time, fixed at the rate's
/// reset.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)). It refuses inputs QuantLib reads past (MMPR4's refusals): no strike,
/// more strikes than rates, and fewer accruals or payment times than strikes.
/// </remarks>
[WorkingType]
public sealed class MultiStepForwards : MultiProductMultiStep
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<double> strikes;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepForwards"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="accruals">The accrual of each forward, at least as many as the strikes.</param>
    /// <param name="paymentTimes">The payment time of each forward, increasing, at least as many as the
    /// strikes.</param>
    /// <param name="strikes">The strike of each forward, one to the number of rates.</param>
    /// <exception cref="ArgumentException">
    /// The rate times or the payment times aren't increasing, or the inputs' sizes don't fit.
    /// </exception>
    public MultiStepForwards(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        ReadOnlySpan<double> strikes)
        : base(rateTimes)
    {
        this.accruals = [.. accruals];
        this.paymentTimes = [.. paymentTimes];
        this.strikes = [.. strikes];
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        MultiStepProductChecks.Sizes(rateTimes.Length - 1, strikes.Length, nameof(strikes));
        MultiStepProductChecks.AtLeast(accruals.Length, strikes.Length, nameof(accruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, strikes.Length, nameof(paymentTimes));
    }

    private MultiStepForwards(MultiStepForwards other)
        : base(other)
    {
        accruals = other.accruals;
        paymentTimes = other.paymentTimes;
        strikes = other.strikes;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets the number of forwards.</summary>
    public override int NumberOfProducts => strikes.Length;

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
        cashFlowsGenerated[currentIndex][0].Amount = (liborRate - strikes[currentIndex]) * accruals[currentIndex];
        numberCashFlowsThisStep.Clear();
        numberCashFlowsThisStep[currentIndex] = 1;
        ++currentIndex;
        return currentIndex == strikes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepForwards(this);
}
