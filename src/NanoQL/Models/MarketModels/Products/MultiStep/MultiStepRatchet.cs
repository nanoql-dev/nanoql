// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepratchet.hpp ql/models/marketmodels/products/multistep/multistepratchet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A full ratchet: at each rate's reset, the coupon max(g_floor · floor + s_floor, g_fixing · f + s_fixing) times the
/// accrual, the floor then the coupon, from the initial floor.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)). Fewer accruals or payment times than rates are refused, where QuantLib
/// reads past them (MMPR4's refusals).
/// </para>
/// <para>
/// QuantLib's sign, kept and documented (MMPR4): a <c>payer</c> ratchet receives the coupon (its multiplier is +1),
/// where QuantLib's swap and inverse floater pay theirs.
/// </para>
/// </remarks>
public sealed class MultiStepRatchet : MultiProductMultiStep
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> paymentTimes;
    private readonly double gearingOfFloor;
    private readonly double gearingOfFixing;
    private readonly double spreadOfFloor;
    private readonly double spreadOfFixing;
    private readonly double multiplier;
    private readonly int lastIndex;
    private readonly double initialFloor;
    private double floor;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepRatchet"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="accruals">The accruals, one a rate at least.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate at least.</param>
    /// <param name="gearingOfFloor">The floor's gearing.</param>
    /// <param name="gearingOfFixing">The fixing's gearing.</param>
    /// <param name="spreadOfFloor">The floor's spread.</param>
    /// <param name="spreadOfFixing">The fixing's spread.</param>
    /// <param name="initialFloor">The initial floor.</param>
    /// <param name="payer">QuantLib's flag: true receives the coupon.</param>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs are shorter than the rates.</exception>
    public MultiStepRatchet(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        double gearingOfFloor,
        double gearingOfFixing,
        double spreadOfFloor,
        double spreadOfFixing,
        double initialFloor,
        bool payer = true)
        : base(rateTimes)
    {
        this.accruals = [.. accruals];
        this.paymentTimes = [.. paymentTimes];
        this.gearingOfFloor = gearingOfFloor;
        this.gearingOfFixing = gearingOfFixing;
        this.spreadOfFloor = spreadOfFloor;
        this.spreadOfFixing = spreadOfFixing;
        multiplier = payer ? 1.0 : -1.0;
        lastIndex = rateTimes.Length - 1;
        this.initialFloor = initialFloor;
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        MultiStepProductChecks.AtLeast(accruals.Length, lastIndex, nameof(accruals));
        MultiStepProductChecks.AtLeast(paymentTimes.Length, lastIndex, nameof(paymentTimes));
    }

    private MultiStepRatchet(MultiStepRatchet other)
        : base(other)
    {
        accruals = other.accruals;
        paymentTimes = other.paymentTimes;
        gearingOfFloor = other.gearingOfFloor;
        gearingOfFixing = other.gearingOfFixing;
        spreadOfFloor = other.spreadOfFloor;
        spreadOfFixing = other.spreadOfFixing;
        multiplier = other.multiplier;
        lastIndex = other.lastIndex;
        initialFloor = other.initialFloor;
        floor = other.floor;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets 1.</summary>
    public override int NumberOfProducts => 1;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => paymentTimes;

    /// <summary>Puts the ratchet at a path's start, its floor the initial floor.</summary>
    public override void Reset()
    {
        currentIndex = 0;
        floor = initialFloor;
    }

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        double liborRate = currentState.ForwardRate(currentIndex);
        double currentCoupon = Math.Max(
            (gearingOfFloor * floor) + spreadOfFloor, (gearingOfFixing * liborRate) + spreadOfFixing);

        cashFlowsGenerated[0][0].TimeIndex = currentIndex;
        cashFlowsGenerated[0][0].Amount = multiplier * accruals[currentIndex] * currentCoupon;

        floor = currentCoupon; // FullRatchet
        numberCashFlowsThisStep[0] = 1;

        ++currentIndex;

        return currentIndex == lastIndex;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepRatchet(this);
}
