// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/onestep/onestepcoterminalswaps.hpp ql/models/marketmodels/products/onestep/onestepcoterminalswaps.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The coterminal payer swaps valued in one step: at the last rate's reset, swap i, from rate i to the last, pays each
/// of its periods' fixed coupon −K τᶠⁱˣ and floating coupon f τᶠˡᵒᵃᵗ at the period's payment time.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)). Fewer accruals or payment times than rates are refused, where QuantLib
/// reads past them (MMPR4's refusals).
/// </remarks>
[WorkingType]
public sealed class OneStepCoterminalSwaps : MultiProductOneStep
{
    private readonly ImmutableArray<double> fixedAccruals;
    private readonly ImmutableArray<double> floatingAccruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly double fixedRate;
    private readonly int lastIndex;

    /// <summary>Initializes a new instance of the <see cref="OneStepCoterminalSwaps"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="fixedAccruals">The fixed leg's accruals, one a rate at least.</param>
    /// <param name="floatingAccruals">The floating leg's accruals, one a rate at least.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate at least.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs are shorter than the rates.</exception>
    public OneStepCoterminalSwaps(
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

    private OneStepCoterminalSwaps(OneStepCoterminalSwaps other)
        : base(other)
    {
        fixedAccruals = other.fixedAccruals;
        floatingAccruals = other.floatingAccruals;
        paymentTimes = other.paymentTimes;
        fixedRate = other.fixedRate;
        lastIndex = other.lastIndex;
    }

    /// <summary>Gets the number of rates: a swap from each.</summary>
    public override int NumberOfProducts => lastIndex;

    /// <summary>Gets twice the number of rates: both coupons of every period.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 2 * lastIndex;

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

        for (int indexOfTime = 0; indexOfTime < lastIndex; indexOfTime++)
        {
            double liborRate = currentState.ForwardRate(indexOfTime);

            for (int i = 0; i <= indexOfTime; i++)
            {
                MarketModelMultiProductCashFlow[] flows = cashFlowsGenerated[i];
                flows[(indexOfTime - i) * 2].TimeIndex = indexOfTime;
                flows[(indexOfTime - i) * 2].Amount = -fixedRate * fixedAccruals[indexOfTime];

                flows[((indexOfTime - i) * 2) + 1].TimeIndex = indexOfTime;
                flows[((indexOfTime - i) * 2) + 1].Amount = liborRate * floatingAccruals[indexOfTime];

                numberCashFlowsThisStep[i] += 2;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new OneStepCoterminalSwaps(this);
}
