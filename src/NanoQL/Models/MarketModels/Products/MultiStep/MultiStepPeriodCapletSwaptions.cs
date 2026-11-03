// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepperiodcapletswaptions.hpp ql/models/marketmodels/products/multistep/multistepperiodcapletswaptions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.Models;

/// <summary>
/// The caplets and coterminal swaptions on the long forwards made of a period of rates each, from an offset: at each
/// long forward's reset, a caplet on the long forward and a swaption on the long coterminal swap from it, the first
/// half of the products the caplets and the second the swaptions.
/// </summary>
/// <remarks>
/// <para>A working type; the payoffs are shared, immutable.</para>
/// <para>
/// QuantLib's design, kept and documented: each value is paid at its long forward's start, its end's discount inside
/// the value (the caplet's τ times the period's discount ratio), and a value of 0 pays nothing.
/// </para>
/// </remarks>
[WorkingType]
public sealed class MultiStepPeriodCapletSwaptions : MultiProductMultiStep
{
    private readonly ImmutableArray<double> paymentTimes;
    private readonly ImmutableArray<StrikedTypePayoff> forwardPayOffs;
    private readonly ImmutableArray<StrikedTypePayoff> swapPayOffs;
    private readonly int period;
    private readonly int offset;
    private readonly int numberBigFRAs;
    private int currentIndex;
    private int productIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepPeriodCapletSwaptions"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <param name="forwardOptionPaymentTimes">The caplets' payment times, increasing, one a long forward.</param>
    /// <param name="swaptionPaymentTimes">The swaptions' payment times, increasing, one a long forward.</param>
    /// <param name="forwardPayOffs">The caplets' payoffs, one a long forward.</param>
    /// <param name="swapPayOffs">The swaptions' payoffs, one a long forward.</param>
    /// <param name="period">The number of rates in a long forward.</param>
    /// <param name="offset">The rates before the first long forward, less than the period.</param>
    /// <exception cref="ArgumentException">
    /// The times aren't increasing, the offset isn't less than the period, no long forward fits, or an input isn't one a
    /// long forward.
    /// </exception>
    public MultiStepPeriodCapletSwaptions(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> forwardOptionPaymentTimes,
        ReadOnlySpan<double> swaptionPaymentTimes,
        ReadOnlySpan<StrikedTypePayoff> forwardPayOffs,
        ReadOnlySpan<StrikedTypePayoff> swapPayOffs,
        int period,
        int offset)
        : base(rateTimes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(period);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        foreach (StrikedTypePayoff p in forwardPayOffs)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(forwardPayOffs));
        }

        foreach (StrikedTypePayoff p in swapPayOffs)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(swapPayOffs));
        }

        UtilitiesFunctions.CheckIncreasingTimes(forwardOptionPaymentTimes);
        UtilitiesFunctions.CheckIncreasingTimes(swaptionPaymentTimes);
        paymentTimes = [.. forwardOptionPaymentTimes, .. swaptionPaymentTimes];
        this.forwardPayOffs = [.. forwardPayOffs];
        this.swapPayOffs = [.. swapPayOffs];
        this.period = period;
        this.offset = offset;

        if (!(offset < period))
        {
            throw new ArgumentException(
                "the offset must be less then the period in MultiStepPeriodCapletSwaptions ", nameof(offset));
        }

        int numberFRAs = rateTimes.Length - 1;
        numberBigFRAs = (numberFRAs - offset) / period;

        if (!(numberBigFRAs > 0))
        {
            throw new ArgumentException(
                "we must have at least one FRA after the periodizing in  MultiStepPeriodCapletSwaptions ", nameof(period));
        }

        if (forwardOptionPaymentTimes.Length != numberBigFRAs)
        {
            throw new ArgumentException(
                "we must have precisely one payment time for each forward option  MultiStepPeriodCapletSwaptions ",
                nameof(forwardOptionPaymentTimes));
        }

        if (forwardPayOffs.Length != numberBigFRAs)
        {
            throw new ArgumentException(
                "we must have precisely one payoff  for each forward option  MultiStepPeriodCapletSwaptions ",
                nameof(forwardPayOffs));
        }

        if (swaptionPaymentTimes.Length != numberBigFRAs)
        {
            throw new ArgumentException(
                "we must have precisely one payment time for each swaption in MultiStepPeriodCapletSwaptions ",
                nameof(swaptionPaymentTimes));
        }

        if (swapPayOffs.Length != numberBigFRAs)
        {
            throw new ArgumentException(
                "we must have precisely one payoff  for each swaption in  MultiStepPeriodCapletSwaptions ", nameof(swapPayOffs));
        }
    }

    private MultiStepPeriodCapletSwaptions(MultiStepPeriodCapletSwaptions other)
        : base(other)
    {
        paymentTimes = other.paymentTimes;
        forwardPayOffs = other.forwardPayOffs;
        swapPayOffs = other.swapPayOffs;
        period = other.period;
        offset = other.offset;
        numberBigFRAs = other.numberBigFRAs;
        currentIndex = other.currentIndex;
        productIndex = other.productIndex;
    }

    /// <summary>Gets twice the number of long forwards: a caplet and a swaption on each.</summary>
    public override int NumberOfProducts => numberBigFRAs * 2;

    /// <summary>Gets 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 1;

    /// <summary>The caplets' payment times, then the swaptions'.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => paymentTimes;

    /// <summary>Puts the product at a path's start.</summary>
    public override void Reset()
    {
        currentIndex = 0;
        productIndex = 0;
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

        if (currentIndex >= offset && (currentIndex - offset) % period == 0)
        {
            // caplet first
            double df = currentState.DiscountRatio(currentIndex + period, currentIndex);
            double tau = RateTimes[currentIndex + period] - RateTimes[currentIndex];
            double forward = ((1.0 / df) - 1.0) / tau;
            double value = forwardPayOffs[productIndex].Evaluate(forward);
            value *= tau * currentState.DiscountRatio(currentIndex + period, currentIndex);

            if (value > 0)
            {
                numberCashFlowsThisStep[productIndex] = 1;
                cashFlowsGenerated[productIndex][0].Amount = value;
                cashFlowsGenerated[productIndex][0].TimeIndex = productIndex;
            }

            // now swaption
            int numberPeriods = numberBigFRAs - productIndex;
            double b = 0.0;
            double p0 = 1.0; // i.e currentState.discountRatio(currentIndex_,currentIndex_);
            double pn = currentState.DiscountRatio(currentIndex + (numberPeriods * period), currentIndex);
            for (int i = 0; i < numberPeriods; ++i)
            {
                double t = RateTimes[currentIndex + ((i + 1) * period)] - RateTimes[currentIndex + (i * period)];
                b += t * currentState.DiscountRatio(currentIndex + ((i + 1) * period), currentIndex);
            }

            double swapRate = (p0 - pn) / b;

            double swaptionValue = swapPayOffs[productIndex].Evaluate(swapRate);
            swaptionValue *= b;

            if (swaptionValue > 0)
            {
                numberCashFlowsThisStep[productIndex + numberBigFRAs] = 1;
                cashFlowsGenerated[productIndex + numberBigFRAs][0].Amount = swaptionValue;
                cashFlowsGenerated[productIndex + numberBigFRAs][0].TimeIndex = productIndex + numberBigFRAs;
            }

            ++productIndex;
        }

        ++currentIndex;

        return productIndex >= numberBigFRAs;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepPeriodCapletSwaptions(this);
}
