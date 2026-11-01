// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/onestep/onestepforwards.hpp ql/models/marketmodels/products/onestep/onestepforwards.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Forward-rate agreements valued in one step: at the last rate's reset, product i pays (fᵢ − Kᵢ) τᵢ at its payment time.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)). It refuses inputs QuantLib reads past (MMPR4's refusals): no strike,
/// more strikes than rates, and fewer accruals or payment times than strikes.
/// </remarks>
[WorkingType]
public sealed class OneStepForwards : MultiProductOneStep
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<double> strikes;

    /// <summary>Initializes a new instance of the <see cref="OneStepForwards"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="accruals">The accrual of each forward, at least as many as the strikes.</param>
    /// <param name="paymentTimes">The payment time of each forward, increasing, at least as many as the strikes.</param>
    /// <param name="strikes">The strike of each forward, at most one a rate; none for a product that pays nothing.</param>
    /// <exception cref="ArgumentException">
    /// The rate times or the payment times aren't increasing, or the inputs' sizes don't fit.
    /// </exception>
    public OneStepForwards(
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
        MultiStepProductChecks.AtMost(rateTimes.Length - 1, strikes.Length, nameof(strikes));
        MultiStepProductChecks.AtLeast(accruals.Length, strikes.Length, nameof(accruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, strikes.Length, nameof(paymentTimes));
    }

    private OneStepForwards(OneStepForwards other)
        : base(other)
    {
        accruals = other.accruals;
        paymentTimes = other.paymentTimes;
        strikes = other.strikes;
    }

    /// <summary>Gets the number of forwards.</summary>
    public override int NumberOfProducts => strikes.Length;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => paymentTimes;

    /// <summary>Nothing: the product has no state.</summary>
    public override void Reset()
    {
    }

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        for (int i = 0; i < strikes.Length; ++i)
        {
            double liborRate = currentState.ForwardRate(i);
            cashFlowsGenerated[i][0].TimeIndex = i;
            cashFlowsGenerated[i][0].Amount = (liborRate - strikes[i]) * accruals[i];
        }

        numberCashFlowsThisStep.Fill(1);
        return true;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new OneStepForwards(this);
}
