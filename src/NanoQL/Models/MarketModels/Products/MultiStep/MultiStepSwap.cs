// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepswap.hpp ql/models/marketmodels/products/multistep/multistepswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A swap on the forward rates: at each rate's reset, the fixed coupon −K τᶠⁱˣᵢ and the floating coupon fᵢ τᶠˡᵒᵃᵗᵢ,
/// each paid at the period's payment time, signed for a payer (−1 on the floating leg for a receiver).
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)). Fewer accruals or payment times than rates are refused, where QuantLib
/// reads past them (MMPR4's refusals).
/// </remarks>
public sealed class MultiStepSwap : MultiProductMultiStep
{
    private readonly ImmutableArray<double> fixedAccruals;
    private readonly ImmutableArray<double> floatingAccruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly double fixedRate;
    private readonly double multiplier;
    private readonly int lastIndex;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepSwap"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="fixedAccruals">The fixed leg's accruals, one a rate at least.</param>
    /// <param name="floatingAccruals">The floating leg's accruals, one a rate at least.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate at least.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="payer">Whether the swap pays the fixed leg.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs are shorter than the rates.</exception>
    public MultiStepSwap(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> fixedAccruals,
        ReadOnlySpan<double> floatingAccruals,
        ReadOnlySpan<double> paymentTimes,
        double fixedRate,
        bool payer = true)
        : base(rateTimes)
    {
        this.fixedAccruals = [.. fixedAccruals];
        this.floatingAccruals = [.. floatingAccruals];
        this.paymentTimes = [.. paymentTimes];
        this.fixedRate = fixedRate;
        multiplier = payer ? 1.0 : -1.0;
        lastIndex = rateTimes.Length - 1;
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        MultiStepProductChecks.AtLeast(fixedAccruals.Length, lastIndex, nameof(fixedAccruals));
        MultiStepProductChecks.AtLeast(floatingAccruals.Length, lastIndex, nameof(floatingAccruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, lastIndex, nameof(paymentTimes));
    }

    private MultiStepSwap(MultiStepSwap other)
        : base(other)
    {
        fixedAccruals = other.fixedAccruals;
        floatingAccruals = other.floatingAccruals;
        paymentTimes = other.paymentTimes;
        fixedRate = other.fixedRate;
        multiplier = other.multiplier;
        lastIndex = other.lastIndex;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets 1.</summary>
    public override int NumberOfProducts => 1;

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
        MarketModelMultiProductCashFlow[] flows = cashFlowsGenerated[0];

        flows[0].TimeIndex = currentIndex;
        flows[0].Amount = -multiplier * fixedRate * fixedAccruals[currentIndex];

        flows[1].TimeIndex = currentIndex;
        flows[1].Amount = multiplier * liborRate * floatingAccruals[currentIndex];

        numberCashFlowsThisStep[0] = 2;

        ++currentIndex;

        return currentIndex == lastIndex;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepSwap(this);
}
