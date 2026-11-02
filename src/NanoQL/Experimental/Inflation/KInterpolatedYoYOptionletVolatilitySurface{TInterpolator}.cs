// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/kinterpolatedyoyoptionletvolatilitysurface.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: KInterpolatedYoYOptionletVolatilitySurface::volatilityImpl(const Date&, Rate), never ported: only its time form calls it, and the time form reads the stripped curves by time (XRR5 (xiii))

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A year-on-year optionlet volatility surface stripped along each strike of a cap and floor price surface and
/// interpolated across the strikes: QuantLib's <c>KInterpolatedYoYOptionletVolatilitySurface</c>.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator across the strikes.</typeparam>
/// <remarks>
/// <para>
/// The stripper strips the price surface at construction (<see cref="Create"/>), in the context given, and the surface
/// holds the result; a volatility is the strikes' interpolation of the stripped curves' volatilities at the time,
/// extrapolated only when the surface allows it. The index is observed flat, at the start of its inflation period, as
/// QuantLib's. QuantLib's memo of the last date's slice isn't ported (the S-xrt plan's XRD12): each query reads its
/// slice.
/// </para>
/// <para>
/// A volatility reads the stripped curves at the surface's time for the observed date (the S-xrt plan's XRR5 (xiii);
/// <c>Divergences/KInterpolatedYoYVolatilityTests.cs</c>): each strike's stripped curve at that date when the surface
/// is on the stripper's clock, the price surface's reference date and day counter, and on another clock the curves
/// read early or late by the clocks' difference (on QuantLib's euro case, up to 2.7e-5 at 2 settlement days and 4.2e-5
/// on Actual/360, where QuantLib's lag error on the stripper's clock is 6.1e-4). QuantLib's turns the time back into a date, its whole years
/// and the fraction's days at 365 a year, and reads each curve at that date through the curve's own lag, so the curves
/// are read a lag early, and off Actual/365 a day or so further. QuantLib's case reads <see cref="Dslice"/>, the
/// curves' own date route, which is QuantLib's here; its protected date form, which only its time form calls, isn't
/// ported.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class KInterpolatedYoYOptionletVolatilitySurface<TInterpolator> : YoYOptionletVolatilitySurface
    where TInterpolator : struct, IInterpolator
{
    private readonly YoYCapFloorTermPriceSurface capFloorPrices;
    private readonly TInterpolator factory1D;

    private protected KInterpolatedYoYOptionletVolatilitySurface(
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
        TInterpolator interpolator,
        VolatilityType volType,
        double displacement,
        EvaluationContext context)
        : base(
            referenceDate,
            settlementDays,
            cal,
            bdc,
            dc,
            lag,
            capFloorPrices.YoYIndex.Frequency,
            false,
            volType,
            displacement)
    {
        this.capFloorPrices = capFloorPrices;
        factory1D = interpolator;

        // slope is the assumption on the initial caplet volatility change
        Stripped = yoyOptionletStripper.Strip(capFloorPrices, pricer, slope, context);
    }

    /// <inheritdoc/>
    /// <remarks>The reference date plus the price surface's last maturity.</remarks>
    public override Date MaxDate => ReferenceDate + capFloorPrices.Maturities[^1];

    /// <summary>Gets the stripped volatilities the surface interpolates.</summary>
    public StrippedYoYOptionlets Stripped { get; }

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
    public static KInterpolatedYoYOptionletVolatilitySurface<TInterpolator> Create(
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
        TInterpolator interpolator = default,
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
        return new KInterpolatedYoYOptionletVolatilitySurface<TInterpolator>(
            referenceDate, settlementDays, cal, bdc, dc, lag, capFloorPrices, pricer, yoyOptionletStripper, slope,
            interpolator, volType, displacement, context);
    }

    /// <inheritdoc/>
    /// <remarks>The price surface's lowest strike.</remarks>
    public override double MinStrike() => capFloorPrices.Strikes[0];

    /// <inheritdoc/>
    /// <remarks>The price surface's highest strike.</remarks>
    public override double MaxStrike() => capFloorPrices.Strikes[^1];

    /// <summary>The stripped volatilities at a date, each strike's curve read at the date: QuantLib's
    /// <c>Dslice</c>.</summary>
    /// <param name="d">The date, observed through each curve's own lag.</param>
    /// <returns>The strikes and the volatilities.</returns>
    /// <exception cref="NanoQLException">A curve can't give a volatility at the date.</exception>
    public (ImmutableArray<double> Strikes, ImmutableArray<double> Volatilities) Dslice(Date d) => Stripped.Slice(d);

    /// <inheritdoc/>
    /// <remarks>The strikes' interpolation of the stripped curves' volatilities at the time (XRR5 (xiii)).</remarks>
    protected override double VolatilityImpl(double length, double strike)
    {
        (ImmutableArray<double> strikes, ImmutableArray<double> vols) = Stripped.Slice(length);
        Interpolation tempKinterpolation = factory1D.Interpolate(strikes.AsSpan(), vols.AsSpan());
        return tempKinterpolation.Evaluate(strike, AllowsExtrapolation);
    }
}
