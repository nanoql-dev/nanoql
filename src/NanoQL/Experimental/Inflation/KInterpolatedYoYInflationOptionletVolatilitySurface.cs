// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/kinterpolatedyoyoptionletvolatilitysurface.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The year-on-year optionlet volatility surface interpolating its stripped volatilities linearly in strike:
/// QuantLib-Python's <c>KInterpolatedYoYInflationOptionletVolatilitySurface</c>.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="KInterpolatedYoYOptionletVolatilitySurface{TInterpolator}"/>
/// on <see cref="Linear"/>: its own <see cref="Create"/> returns it.
/// </remarks>
[Experimental("NQL0001")]
public sealed class KInterpolatedYoYInflationOptionletVolatilitySurface : KInterpolatedYoYOptionletVolatilitySurface<Linear>
{
    private KInterpolatedYoYInflationOptionletVolatilitySurface(
        Date referenceDate,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period lag,
        YoYCapFloorTermPriceSurface capFloorPrices,
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> pricer,
        YoYOptionletStripper yoyOptionletStripper,
        double slope,
        Linear interpolator,
        VolatilityType volType,
        double displacement,
        EvaluationContext context)
        : base(referenceDate, settlementDays, cal, bdc, dc, lag, capFloorPrices, pricer, yoyOptionletStripper, slope, interpolator, volType, displacement, context)
    {
    }

    /// <summary>
    /// Builds the surface on the reference date the settlement days give after the evaluation date, stripping the price
    /// surface in the context.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="lag">The observation lag.</param>
    /// <param name="capFloorPrices">The cap and floor price surface.</param>
    /// <param name="pricer">The cap and floor engine, a function of the instrument, the context and the trial
    /// volatility.</param>
    /// <param name="yoyOptionletStripper">The stripper.</param>
    /// <param name="slope">The assumption on the initial optionlet volatilities' change over time, in proportion to
    /// their level.</param>
    /// <param name="context">The evaluation context: its evaluation date, and the market the stripping prices
    /// in.</param>
    /// <param name="interpolator">The interpolator across the strikes.</param>
    /// <param name="volType">The volatility type.</param>
    /// <param name="displacement">The displacement of a shifted lognormal volatility, 0 or 1.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="displacement"/> isn't 0 or 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The stripping fails.</exception>
    public static new KInterpolatedYoYInflationOptionletVolatilitySurface Create(
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period lag,
        YoYCapFloorTermPriceSurface capFloorPrices,
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> pricer,
        YoYOptionletStripper yoyOptionletStripper,
        double slope,
        EvaluationContext context,
        Linear interpolator = default,
        VolatilityType volType = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(capFloorPrices);
        ArgumentNullException.ThrowIfNull(pricer);
        ArgumentNullException.ThrowIfNull(yoyOptionletStripper);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new KInterpolatedYoYInflationOptionletVolatilitySurface(
            referenceDate, settlementDays, cal, bdc, dc, lag, capFloorPrices, pricer, yoyOptionletStripper, slope,
            interpolator, volType, displacement, context);
    }
}
