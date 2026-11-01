// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepinversefloater.hpp ql/models/marketmodels/products/multistep/multistepinversefloater.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// An inverse floater: at each rate's reset, the inverse floating coupon max(Kᵢ − mᵢ fᵢ, 0) τᶠⁱˣᵢ less the floating
/// coupon (fᵢ + sᵢ) τᶠˡᵒᵃᵗᵢ, received, or paid by a payer.
/// </summary>
/// <remarks>A working type (the S-mmp plan's MMPR1 (i)).</remarks>
[WorkingType]
public sealed class MultiStepInverseFloater : MultiProductMultiStep
{
    private readonly ImmutableArray<double> fixedAccruals;
    private readonly ImmutableArray<double> floatingAccruals;
    private readonly ImmutableArray<double> fixedStrikes;
    private readonly ImmutableArray<double> fixedMultipliers;
    private readonly ImmutableArray<double> floatingSpreads;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly double multiplier;
    private readonly int lastIndex;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepInverseFloater"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="fixedAccruals">The inverse floating leg's accruals, one a rate.</param>
    /// <param name="floatingAccruals">The floating leg's accruals, one a rate.</param>
    /// <param name="fixedStrikes">The inverse floating coupons' strikes, one a rate.</param>
    /// <param name="fixedMultipliers">The inverse floating coupons' multipliers, one a rate.</param>
    /// <param name="floatingSpreads">The floating coupons' spreads, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <param name="payer">Whether the inverse floater is paid.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or an input isn't one a rate.</exception>
    public MultiStepInverseFloater(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> fixedAccruals,
        ReadOnlySpan<double> floatingAccruals,
        ReadOnlySpan<double> fixedStrikes,
        ReadOnlySpan<double> fixedMultipliers,
        ReadOnlySpan<double> floatingSpreads,
        ReadOnlySpan<double> paymentTimes,
        bool payer = true)
        : base(rateTimes)
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
    }

    private MultiStepInverseFloater(MultiStepInverseFloater other)
        : base(other)
    {
        fixedAccruals = other.fixedAccruals;
        floatingAccruals = other.floatingAccruals;
        fixedStrikes = other.fixedStrikes;
        fixedMultipliers = other.fixedMultipliers;
        floatingSpreads = other.floatingSpreads;
        paymentTimes = other.paymentTimes;
        multiplier = other.multiplier;
        lastIndex = other.lastIndex;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets 1.</summary>
    public override int NumberOfProducts => 1;

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
        double inverseFloatingCoupon =
            Math.Max(fixedStrikes[currentIndex] - (fixedMultipliers[currentIndex] * liborRate), 0.0) * fixedAccruals[currentIndex];
        double floatingCoupon = (liborRate + floatingSpreads[currentIndex]) * floatingAccruals[currentIndex];

        cashFlowsGenerated[0][0].TimeIndex = currentIndex;
        cashFlowsGenerated[0][0].Amount = multiplier * (inverseFloatingCoupon - floatingCoupon);

        numberCashFlowsThisStep[0] = 1;
        ++currentIndex;

        return currentIndex == lastIndex;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepInverseFloater(this);

    private void Check(int given, string name)
    {
        if (given != lastIndex)
        {
            throw new ArgumentException($" Incorrect number of {name} given, should be {lastIndex} not {given}", name);
        }
    }
}
