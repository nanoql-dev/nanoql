// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A constant year-on-year optionlet volatility, with no strike or time dependence, over a strike range.</summary>
/// <remarks>
/// QuantLib's two constructors, a volatility and a quote, collapse into one, since a quote is a <see cref="double"/>;
/// both take settlement days, so the reference date moves with the evaluation date, and the structure is built by
/// <see cref="Create"/>, which fixes it.
/// </remarks>
public sealed class ConstantYoYOptionletVolatility : YoYOptionletVolatilitySurface
{
    private readonly double volatility;
    private readonly double minStrike;
    private readonly double maxStrike;

    private ConstantYoYOptionletVolatility(
        double v,
        Date referenceDate,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period observationLag,
        Frequency frequency,
        bool indexIsInterpolated,
        double minStrike,
        double maxStrike,
        VolatilityType volType,
        double displacement)
        : base(
            referenceDate,
            settlementDays,
            cal,
            bdc,
            dc,
            observationLag,
            frequency,
            indexIsInterpolated,
            volType,
            displacement)
    {
        volatility = v;
        this.minStrike = minStrike;
        this.maxStrike = maxStrike;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>
    /// A constant year-on-year optionlet volatility whose reference date is the given number of business days after the
    /// evaluation date.
    /// </summary>
    /// <param name="v">The volatility.</param>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="observationLag">The lag the structure observes the index with.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="indexIsInterpolated">Whether the index is observed with linear interpolation.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="minStrike">The minimum strike, -100% by default.</param>
    /// <param name="maxStrike">The maximum strike, +10,000% by default.</param>
    /// <param name="volType">The volatility type.</param>
    /// <param name="displacement">The displacement of a shifted lognormal volatility, 0 or 1.</param>
    /// <returns>The structure, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cal"/>, <paramref name="dc"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="displacement"/> isn't 0 or 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/>, <paramref name="frequency"/> or <paramref name="volType"/> isn't a member of its
    /// type.</exception>
    public static ConstantYoYOptionletVolatility Create(
        double v,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period observationLag,
        Frequency frequency,
        bool indexIsInterpolated,
        EvaluationContext context,
        double minStrike = -1.0,
        double maxStrike = 100.0,
        VolatilityType volType = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(cal);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new ConstantYoYOptionletVolatility(
            v,
            referenceDate,
            settlementDays,
            cal,
            bdc,
            dc,
            observationLag,
            frequency,
            indexIsInterpolated,
            minStrike,
            maxStrike,
            volType,
            displacement);
    }

    /// <inheritdoc/>
    public override double MinStrike() => minStrike;

    /// <inheritdoc/>
    public override double MaxStrike() => maxStrike;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double length, double strike) => volatility;
}
