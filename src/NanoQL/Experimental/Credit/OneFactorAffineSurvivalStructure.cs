// SPDX-FileCopyrightText: Copyright (C) 2015 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/onefactoraffinesurvival.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (model, dc, jumps), which only the interpolated affine curve's constructor of the same form calls, and nothing calls that

using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The survival probability term structure of a one-factor stochastic model of the default intensity: the model's
/// bond price at its initial intensity, conditional on survival to a prior time and the intensity realized there.
/// </summary>
/// <remarks>
/// <para>
/// While deriving from the hazard rate structure, its hazard rate is the deterministic part of the model alone, which
/// derived classes give where there is one (none here); the probabilities depend on it and the stochastic part. The
/// density is that deterministic hazard rate times the survival over the model's bond price, QuantLib's, kept: 0 here
/// where −dS/dt is 0.0187 (the S-xcr plan's XR8 (xxxviii)). The model's initial intensity is its dynamics' short rate at
/// the process's x0. QuantLib's reference-date constructor drops its calendar argument; here it is kept. The structure
/// whose reference date follows the evaluation date is built by <see cref="Create"/> (D15).
/// </para>
/// <para>
/// Curve extrapolation means something else on different curves: on curves matched to the market the credit curves
/// would be asked to extrapolate; on affine models over a static term structure it is this one. To be done, as in
/// QuantLib: the jumps' treatment in the conditional probabilities, and the remaining constructors.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class OneFactorAffineSurvivalStructure : HazardRateStructure
{
    /// <summary>Initializes a new instance of the <see cref="OneFactorAffineSurvivalStructure"/> class with a fixed
    /// reference date.</summary>
    /// <param name="model">The model of the default intensity.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date is <c>default(Date)</c>, or the jumps and their dates
    /// differ in number.</exception>
    public OneFactorAffineSurvivalStructure(
        OneFactorAffineModel model,
        Date referenceDate,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        int? settlementDays = null)
        : base(referenceDate, calendar, dayCounter, settlementDays, jumps, jumpDates)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <summary>Gets the model of the default intensity.</summary>
    public OneFactorAffineModel Model { get; }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>Builds the structure, its reference date the evaluation date advanced by the settlement days.</summary>
    /// <param name="model">The model of the default intensity.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates.</param>
    /// <returns>The structure.</returns>
    /// <exception cref="ArgumentNullException">An argument but the jumps is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public static OneFactorAffineSurvivalStructure Create(
        OneFactorAffineModel model,
        int settlementDays,
        Calendar calendar,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new OneFactorAffineSurvivalStructure(model, referenceDate, dayCounter, calendar, jumps, jumpDates, settlementDays);
    }

    /// <summary>The survival probability at a target date, conditional on survival to a prior date and on the
    /// intensity realized there.</summary>
    /// <param name="dFwd">The prior date, of the survival and the intensity's realization.</param>
    /// <param name="dTgt">The target date.</param>
    /// <param name="yVal">The intensity realized at the prior date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the structure's range.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">A date is out of the structure's range.</exception>
    public double ConditionalSurvivalProbability(Date dFwd, Date dTgt, double yVal, bool extrapolate = false) =>
        ConditionalSurvivalProbability(TimeFromReference(dFwd), TimeFromReference(dTgt), yVal, extrapolate);

    /// <summary>The survival probability at a target time, conditional on survival to a prior time and on the
    /// intensity realized there.</summary>
    /// <param name="tFwd">The prior time.</param>
    /// <param name="tgt">The target time.</param>
    /// <param name="yVal">The intensity realized at the prior time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the structure's range.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">A time is out of the structure's range.</exception>
    public double ConditionalSurvivalProbability(double tFwd, double tgt, double yVal, bool extrapolate = false)
    {
        CheckRange(tFwd, extrapolate);
        CheckRange(tgt, extrapolate);

        // \todo ADD JUMPS TREATMENT
        return ConditionalSurvivalProbabilityImpl(tFwd, tgt, yVal);
    }

    /// <summary>The model's initial intensity: its dynamics' short rate at the process's initial value.</summary>
    /// <returns>The intensity.</returns>
    protected double InitialIntensity()
    {
        OneFactorModelShortRateDynamics dynamics = Model.Dynamics();
        return dynamics.ShortRate(0.0, dynamics.Process.X0);
    }

    /// <inheritdoc/>
    protected override double SurvivalProbabilityImpl(double t) => Model.DiscountBond(0.0, t, InitialIntensity());

    /// <inheritdoc/>
    protected override double DefaultDensityImpl(double t) =>
        HazardRateImpl(t) * SurvivalProbabilityImpl(t) / Model.DiscountBond(0.0, t, InitialIntensity());

    /// <summary>The conditional survival probability, the model's bond price from the prior time at the realized
    /// intensity.</summary>
    /// <param name="tFwd">The prior time.</param>
    /// <param name="tgt">The target time.</param>
    /// <param name="yVal">The intensity realized at the prior time.</param>
    /// <returns>The probability.</returns>
    protected virtual double ConditionalSurvivalProbabilityImpl(double tFwd, double tgt, double yVal) =>
        Model.DiscountBond(tFwd, tgt, yVal);

    /// <summary>exp(−∫ h) of the deterministic part of the hazard rate, the rate the structure reports: 1, as it has
    /// none; null on a derived class giving its own, as QuantLib's header asks.</summary>
    /// <param name="t">The time.</param>
    /// <returns>1, or null.</returns>
    internal override double? HazardRateSurvival(double t) => GetType() == typeof(OneFactorAffineSurvivalStructure) ? 1.0 : null;

    /// <summary>The deterministic part of the hazard rate: none.</summary>
    /// <param name="t">The time.</param>
    /// <returns>0.</returns>
    protected override double HazardRateImpl(double t) => 0.0;
}
