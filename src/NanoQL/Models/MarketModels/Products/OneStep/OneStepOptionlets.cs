// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/onestep/onestepoptionlets.hpp ql/models/marketmodels/products/onestep/onestepoptionlets.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Optionlets valued in one step: at the last rate's reset, product i pays its payoff of fᵢ times τᵢ at its payment time
/// where the payoff is positive.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)); the payoffs are shared, immutable. It refuses inputs QuantLib reads
/// past (MMPR4's refusals): no payoff, more payoffs than rates, and fewer accruals or payment times than payoffs.
/// </remarks>
public sealed class OneStepOptionlets : MultiProductOneStep
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<Payoff> payoffs;

    /// <summary>Initializes a new instance of the <see cref="OneStepOptionlets"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="accruals">The accrual of each optionlet, at least as many as the payoffs.</param>
    /// <param name="paymentTimes">The payment time of each optionlet, increasing, at least as many as the payoffs.</param>
    /// <param name="payoffs">The payoff of each optionlet, at most one a rate; none for a product that pays nothing.</param>
    /// <exception cref="ArgumentException">
    /// The rate times or the payment times aren't increasing, or the inputs' sizes don't fit.
    /// </exception>
    /// <exception cref="ArgumentNullException">A payoff is null.</exception>
    public OneStepOptionlets(
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
        MultiStepProductChecks.AtMost(rateTimes.Length - 1, payoffs.Length, nameof(payoffs));
        MultiStepProductChecks.AtLeast(accruals.Length, payoffs.Length, nameof(accruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, payoffs.Length, nameof(paymentTimes));
    }

    private OneStepOptionlets(OneStepOptionlets other)
        : base(other)
    {
        accruals = other.accruals;
        paymentTimes = other.paymentTimes;
        payoffs = other.payoffs;
    }

    /// <summary>Gets the number of optionlets.</summary>
    public override int NumberOfProducts => payoffs.Length;

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
        numberCashFlowsThisStep.Clear();
        for (int i = 0; i < payoffs.Length; ++i)
        {
            double liborRate = currentState.ForwardRate(i);
            double payoff = payoffs[i].Evaluate(liborRate);
            if (payoff > 0.0)
            {
                numberCashFlowsThisStep[i] = 1;
                cashFlowsGenerated[i][0].TimeIndex = i;
                cashFlowsGenerated[i][0].Amount = payoff * accruals[i];
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new OneStepOptionlets(this);
}
