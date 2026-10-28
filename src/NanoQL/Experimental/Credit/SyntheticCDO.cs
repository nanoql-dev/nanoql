// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/syntheticcdo.hpp ql/experimental/credit/syntheticcdo.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A synthetic collateralized debt obligation: protection on a basket's tranche between its attachment D₁ and
/// detachment D₂, against a running premium on the tranche's notional, reduced by the expected protection payments, and
/// an upfront.
/// </summary>
/// <remarks>
/// <para>
/// For purchased protection the value is the protection leg's V₁ = Σ (Eᵢ − Eᵢ₋₁)·dᵢ less the premium leg's V₂ = m·Σ (D₂
/// − D₁ − Eᵢ)·Δᵢ₋₁,ᵢ·dᵢ, Eᵢ the expected tranche loss at tᵢ. The CDO holds its basket, bound to a loss model (the S-xcr
/// plan's XR1 (c)), and is priced by <see cref="MidPointCDOEngine"/> or <see cref="IntegralCDOEngine"/>. A notional
/// above the basket's tranche notional levers the CDO by their ratio.
/// </para>
/// <para>
/// <see cref="ImplicitCorrelation"/> binds a Gaussian LHP model to a copy of the basket at each trial correlation and
/// leaves the CDO's basket untouched, where QuantLib's sets the model on the user's basket and leaves it there (XR1
/// (d)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class SyntheticCDO : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="SyntheticCDO"/> class.</summary>
    /// <param name="basket">The basket, bound to its loss model.</param>
    /// <param name="side">The protection side.</param>
    /// <param name="schedule">The premium schedule.</param>
    /// <param name="upfrontRate">The upfront rate, on the tranche's notional.</param>
    /// <param name="runningRate">The running premium rate.</param>
    /// <param name="dayCounter">The premium's day counter.</param>
    /// <param name="paymentConvention">The premium's payment convention.</param>
    /// <param name="notional">The CDO's notional; by default the basket's tranche notional. A larger one levers the
    /// CDO by the ratio.</param>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The basket is empty ("basket is empty"), or its inception follows the
    /// schedule's start ("Basket did not exist before contract start.").</exception>
    public SyntheticCDO(
        Basket basket,
        ProtectionSide side,
        Schedule schedule,
        double upfrontRate,
        double runningRate,
        DayCounter dayCounter,
        BusinessDayConvention paymentConvention,
        double? notional = null)
        : this(
            basket,
            side,
            schedule,
            upfrontRate,
            runningRate,
            dayCounter,
            paymentConvention,
            notional.HasValue ? notional.Value / (basket ?? throw new ArgumentNullException(nameof(basket))).TrancheNotional : 1.0)
    {
    }

    private SyntheticCDO(
        Basket basket,
        ProtectionSide side,
        Schedule schedule,
        double upfrontRate,
        double runningRate,
        DayCounter dayCounter,
        BusinessDayConvention paymentConvention,
        double leverageFactor)
    {
        ArgumentNullException.ThrowIfNull(basket);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (basket.Names.IsEmpty)
        {
            throw new ArgumentException("basket is empty", nameof(basket));
        }

        // Basket inception must lie before contract protection start.
        if (!(basket.RefDate <= schedule.StartDate()))
        {
            // using the start date of the schedule might be wrong, think of the CDS rule
            throw new ArgumentException("Basket did not exist before contract start.", nameof(basket));
        }

        Basket = basket;
        Side = side;
        Schedule = schedule;
        UpfrontRate = upfrontRate;
        RunningRate = runningRate;
        LeverageFactor = leverageFactor;
        DayCounter = dayCounter;
        PaymentConvention = paymentConvention;

        // Notice the notional is that of the basket at basket inception, some
        //   names might have defaulted in between
        NormalizedLeg = new FixedRateLeg(schedule)
            .WithNotionals(basket.TrancheNotional * LeverageFactor)
            .WithCouponRates(runningRate, dayCounter)
            .WithPaymentAdjustment(paymentConvention)
            .ToLeg();
    }

    /// <summary>Gets the basket, bound to its loss model.</summary>
    public Basket Basket { get; }

    /// <summary>Gets the protection side.</summary>
    public ProtectionSide Side { get; }

    /// <summary>Gets the premium schedule.</summary>
    public Schedule Schedule { get; }

    /// <summary>Gets the upfront rate.</summary>
    public double UpfrontRate { get; }

    /// <summary>Gets the running premium rate.</summary>
    public double RunningRate { get; }

    /// <summary>Gets the number of times the contract holds the basket's tranched notional.</summary>
    public double LeverageFactor { get; }

    /// <summary>Gets the premium's day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the premium's payment convention.</summary>
    public BusinessDayConvention PaymentConvention { get; }

    /// <summary>Gets the premium leg, on the levered tranche notional at the basket's inception.</summary>
    public ImmutableArray<CashFlow> NormalizedLeg { get; }

    /// <summary>Gets the last protection date, the last coupon's accrual end.</summary>
    public Date Maturity => ((FixedRateCoupon)NormalizedLeg[^1]).AccrualEndDate;

    /// <summary>Whether the last premium payment has occurred.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> if the CDO has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // it could have also expired (knocked out) because theres
        //   no remaining tranche notional.
        return new SimpleEvent(NormalizedLeg[^1].Date).HasOccurred(context);
    }

    /// <inheritdoc/>
    public override InstrumentResults SetupExpired() => new SyntheticCDOResults
    {
        NPV = 0.0,
        ErrorEstimate = 0.0,
        ValuationDate = null,
        PremiumValue = 0.0,
        ProtectionValue = 0.0,
        UpfrontPremiumValue = 0.0,
        RemainingNotional = 1.0,
        ExpectedTrancheLoss = [],
        FairPremium = null,
        FairUpfrontPremium = 0.0,
        PremiumLegNPV = 0.0,
        ProtectionLegNPV = 0.0,
    };

    /// <summary>
    /// The flat Gaussian copula LHP correlation that gives the CDO a target value on the mid-point engine: Brent's
    /// root on (ε, 1 − ε) from a guess of 0.001, an LHP model bound to a copy of the basket at each trial.
    /// </summary>
    /// <param name="recoveries">The names' recoveries for the LHP model.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context, of the basket's evaluation date.</param>
    /// <param name="targetNPV">The target value.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <returns>The correlation.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The solver finds no root, or the engine refuses the CDO.</exception>
    public double ImplicitCorrelation(
        IReadOnlyList<double> recoveries, YieldTermStructure discountCurve, EvaluationContext context, double targetNPV = 0.0, double accuracy = 1.0e-3)
    {
        ArgumentNullException.ThrowIfNull(recoveries);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(context);
        double ObjectiveFunction(double guess)
        {
            SyntheticCDO trial = WithBasket(Basket.SetLossModel(new GaussianLHPLossModel(guess, recoveries)));
            return MidPointCDOEngine.Calculate(trial, context, discountCurve).NPV!.Value - targetNPV;
        }

        const double guess = 0.001;
        return new Brent().Solve(ObjectiveFunction, accuracy, guess, NumericLimits.QL_EPSILON, 1.0 - NumericLimits.QL_EPSILON);
    }

    /// <summary>The CDO on another basket of the same terms.</summary>
    /// <param name="basket">The basket.</param>
    /// <returns>The CDO.</returns>
    internal SyntheticCDO WithBasket(Basket basket) =>
        new(basket, Side, Schedule, UpfrontRate, RunningRate, DayCounter, PaymentConvention, LeverageFactor);
}
