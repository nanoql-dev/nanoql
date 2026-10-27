// SPDX-FileCopyrightText: Copyright (C) 2010 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multisteptarn.hpp ql/models/marketmodels/products/multistep/multisteptarn.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A targeted redemption note: at each rate's reset it receives the floating coupon (f + s) τᶠˡᵒᵃᵗ and pays the
/// inverse floating coupon max(K − m f, 0) τ, until the coupons paid reach the total coupon or the last rate, when it
/// pays what is left of the total and ends.
/// </summary>
/// <remarks>
/// <para>A working type (the S-mmp plan's MMPR1 (i)).</para>
/// <para>
/// The floating coupons' payment times are the floating schedule's (MMPR4 (x), a documented divergence): QuantLib
/// appends the fixed payment times again (<c>multisteptarn.cpp:50</c>, <c>paymentTimes[i]</c>), so the floating
/// coupons are discounted at the fixed leg's times (<c>Divergences/MultiStepTarnPaymentTimesTests.cs</c>). Where the
/// two schedules coincide, QuantLib's value.
/// </para>
/// </remarks>
public sealed class MultiStepTarn : MultiProductMultiStep
{
    private readonly ImmutableArray<double> accruals;
    private readonly ImmutableArray<double> accrualsFloating;
    private readonly ImmutableArray<double> allPaymentTimes;
    private readonly double totalCoupon;
    private readonly ImmutableArray<double> strikes;
    private readonly ImmutableArray<double> multipliers;
    private readonly ImmutableArray<double> floatingSpreads;
    private readonly int lastIndex;
    private double couponPaid;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepTarn"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="accruals">The inverse floating coupons' accruals, one a rate.</param>
    /// <param name="accrualsFloating">The floating coupons' accruals, one a rate.</param>
    /// <param name="paymentTimes">The inverse floating coupons' payment times, one a rate.</param>
    /// <param name="paymentTimesFloating">The floating coupons' payment times, one a rate.</param>
    /// <param name="totalCoupon">The total coupon.</param>
    /// <param name="strikes">The inverse floating coupons' strikes, one a rate.</param>
    /// <param name="multipliers">The inverse floating coupons' multipliers, one a rate.</param>
    /// <param name="floatingSpreads">The floating coupons' spreads, one a rate.</param>
    /// <exception cref="ArgumentException">An input isn't one a rate, or the multipliers are fewer.</exception>
    public MultiStepTarn(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> accrualsFloating,
        ReadOnlySpan<double> paymentTimes,
        ReadOnlySpan<double> paymentTimesFloating,
        double totalCoupon,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> multipliers,
        ReadOnlySpan<double> floatingSpreads)
        : base(rateTimes)
    {
        Check(accruals.Length, rateTimes.Length, "missized accruals in MultiStepTARN", nameof(accruals));
        Check(accrualsFloating.Length, rateTimes.Length, "missized accrualsFloating in MultiStepTARN", nameof(accrualsFloating));
        Check(paymentTimes.Length, rateTimes.Length, "missized paymentTimes in MultiStepTARN", nameof(paymentTimes));
        Check(paymentTimesFloating.Length, rateTimes.Length, "missized paymentTimesFloating in MultiStepTARN", nameof(paymentTimesFloating));
        Check(strikes.Length, rateTimes.Length, "missized strikes in MultiStepTARN", nameof(strikes));
        Check(floatingSpreads.Length, rateTimes.Length, "missized floatingSpreads in MultiStepTARN", nameof(floatingSpreads));
        MultiStepProductChecks.AtLeast(multipliers.Length, rateTimes.Length - 1, nameof(multipliers));

        this.accruals = [.. accruals];
        this.accrualsFloating = [.. accrualsFloating];
        this.totalCoupon = totalCoupon;
        this.strikes = [.. strikes];
        this.multipliers = [.. multipliers];
        this.floatingSpreads = [.. floatingSpreads];
        lastIndex = accruals.Length;

        // the fixed payment times, then the floating ones
        allPaymentTimes = [.. paymentTimes, .. paymentTimesFloating];
    }

    private MultiStepTarn(MultiStepTarn other)
        : base(other)
    {
        accruals = other.accruals;
        accrualsFloating = other.accrualsFloating;
        allPaymentTimes = other.allPaymentTimes;
        totalCoupon = other.totalCoupon;
        strikes = other.strikes;
        multipliers = other.multipliers;
        floatingSpreads = other.floatingSpreads;
        lastIndex = other.lastIndex;
        couponPaid = other.couponPaid;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets 1.</summary>
    public override int NumberOfProducts => 1;

    /// <summary>Gets 2: the floating and the inverse floating coupon.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 2;

    /// <summary>The inverse floating coupons' payment times, then the floating coupons'.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => allPaymentTimes;

    /// <summary>Puts the note at a path's start, no coupon paid.</summary>
    public override void Reset()
    {
        currentIndex = 0;
        couponPaid = 0.0;
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
        MarketModelMultiProductCashFlow[] flows = cashFlowsGenerated[0];

        numberCashFlowsThisStep[0] = 2;

        flows[0].Amount = (liborRate + floatingSpreads[currentIndex]) * accrualsFloating[currentIndex];
        flows[0].TimeIndex = lastIndex + currentIndex;

        flows[1].TimeIndex = currentIndex;

        double obviousCoupon =
            Math.Max(strikes[currentIndex] - (multipliers[currentIndex] * liborRate), 0.0) * accruals[currentIndex];

        couponPaid += obviousCoupon;

        ++currentIndex;

        if (couponPaid < totalCoupon && currentIndex < lastIndex)
        {
            flows[1].Amount = -obviousCoupon;
            return false;
        }

        double coupon = obviousCoupon + (totalCoupon - couponPaid);
        flows[1].Amount = -coupon;

        return true;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepTarn(this);

    private static void Check(int given, int rateTimes, string message, string paramName)
    {
        if (given + 1 != rateTimes)
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
