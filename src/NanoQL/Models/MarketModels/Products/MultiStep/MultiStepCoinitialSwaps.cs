// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepcoinitialswaps.hpp ql/models/marketmodels/products/multistep/multistepcoinitialswaps.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The coinitial payer swaps, one product a rate: swap i, from the first rate to rate i, pays at each of its rates'
/// resets the fixed coupon −K τᶠⁱˣ and the floating coupon f τᶠˡᵒᵃᵗ, at the period's payment time.
/// </summary>
/// <remarks>
/// A working type. Fewer accruals or payment times than rates are refused, where QuantLib reads past them.
/// </remarks>
[WorkingType]
public sealed class MultiStepCoinitialSwaps : MultiProductMultiStep
{
    private readonly ImmutableArray<double> fixedAccruals;
    private readonly ImmutableArray<double> floatingAccruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly double fixedRate;
    private readonly int lastIndex;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepCoinitialSwaps"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="fixedAccruals">The fixed leg's accruals, one a rate at least.</param>
    /// <param name="floatingAccruals">The floating leg's accruals, one a rate at least.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate at least.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs are shorter than the rates.</exception>
    public MultiStepCoinitialSwaps(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> fixedAccruals,
        ReadOnlySpan<double> floatingAccruals,
        ReadOnlySpan<double> paymentTimes,
        double fixedRate)
        : base(rateTimes)
    {
        this.fixedAccruals = [.. fixedAccruals];
        this.floatingAccruals = [.. floatingAccruals];
        this.paymentTimes = [.. paymentTimes];
        this.fixedRate = fixedRate;
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        lastIndex = rateTimes.Length - 1;
        MultiStepProductChecks.AtLeast(fixedAccruals.Length, lastIndex, nameof(fixedAccruals));
        MultiStepProductChecks.AtLeast(floatingAccruals.Length, lastIndex, nameof(floatingAccruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, lastIndex, nameof(paymentTimes));
    }

    private MultiStepCoinitialSwaps(MultiStepCoinitialSwaps other)
        : base(other)
    {
        fixedAccruals = other.fixedAccruals;
        floatingAccruals = other.floatingAccruals;
        paymentTimes = other.paymentTimes;
        fixedRate = other.fixedRate;
        lastIndex = other.lastIndex;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets the number of rates: a swap to each.</summary>
    public override int NumberOfProducts => lastIndex;

    /// <summary>Gets 2: the fixed and the floating coupon.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 2;

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
        numberCashFlowsThisStep.Clear();

        for (int i = currentIndex; i < lastIndex; i++)
        {
            MarketModelMultiProductCashFlow[] flows = cashFlowsGenerated[i];
            flows[0].TimeIndex = currentIndex;
            flows[0].Amount = -fixedRate * fixedAccruals[currentIndex];

            flows[1].TimeIndex = currentIndex;
            flows[1].Amount = liborRate * floatingAccruals[currentIndex];

            numberCashFlowsThisStep[i] = 2;
        }

        ++currentIndex;

        return currentIndex == lastIndex;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepCoinitialSwaps(this);
}
