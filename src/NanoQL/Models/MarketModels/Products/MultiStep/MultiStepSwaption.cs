// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepswaption.hpp ql/models/marketmodels/products/multistep/multistepswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.Models;

/// <summary>
/// A swaption on a contiguous run of the rates: at the swap's start its payoff of the swap rate times the annuity, paid
/// at the start, the path finished. It steps through every rate time up to the swap's start; mainly a test product.
/// </summary>
/// <remarks>A working type; the payoff is shared, immutable.</remarks>
[WorkingType]
public sealed class MultiStepSwaption : MultiProductMultiStep
{
    private readonly int startIndex;
    private readonly int endIndex;
    private readonly StrikedTypePayoff payoff;
    private readonly ImmutableArray<double> paymentTimes;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepSwaption"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="startIndex">The swap's first rate.</param>
    /// <param name="endIndex">The swap's end, after its last rate, at most the number of rates.</param>
    /// <param name="payOff">The payoff of the swap rate.</param>
    /// <exception cref="ArgumentException">The start isn't before the end, or the end is past the rates.</exception>
    public MultiStepSwaption(ReadOnlySpan<double> rateTimes, int startIndex, int endIndex, StrikedTypePayoff payOff)
        : base(rateTimes)
    {
        ArgumentNullException.ThrowIfNull(payOff);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(endIndex);
        this.startIndex = startIndex;
        this.endIndex = endIndex;
        payoff = payOff;
        if (!(startIndex < endIndex))
        {
            throw new ArgumentException(" start index must be before end index", nameof(startIndex));
        }

        if (!(endIndex < rateTimes.Length))
        {
            throw new ArgumentException("end index be before the end of the rates.", nameof(endIndex));
        }

        paymentTimes = [rateTimes[startIndex]];
    }

    private MultiStepSwaption(MultiStepSwaption other)
        : base(other)
    {
        startIndex = other.startIndex;
        endIndex = other.endIndex;
        payoff = other.payoff;
        paymentTimes = other.paymentTimes;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets 1.</summary>
    public override int NumberOfProducts => 1;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>The swap's start, the one payment time.</summary>
    /// <returns>The time.</returns>
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
        if (currentIndex == startIndex)
        {
            cashFlowsGenerated[0][0].TimeIndex = 0;

            double swapRate = currentState.CmSwapRate(startIndex, endIndex - startIndex);
            double annuity = currentState.CmSwapAnnuity(startIndex, startIndex, endIndex - startIndex);

            cashFlowsGenerated[0][0].Amount = payoff.Evaluate(swapRate) * annuity;

            numberCashFlowsThisStep[0] = cashFlowsGenerated[0][0].Amount != 0.0 ? 1 : 0;

            return true;
        }

        numberCashFlowsThisStep[0] = 0;
        ++currentIndex;
        return false;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepSwaption(this);
}
