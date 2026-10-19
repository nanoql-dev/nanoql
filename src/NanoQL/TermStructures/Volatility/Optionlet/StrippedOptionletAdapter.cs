// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp ql/termstructures/volatility/optionlet/strippedoptionletadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// An optionlet volatility structure on a <see cref="StrippedOptionletBase"/>: linear in strike at each fixing date,
/// then linear in time.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib builds the adapter on a reference date that moves with the evaluation date, the stripper's settlement days
/// after it: here that is <see cref="Create"/>, which takes the context and fixes the date. A stripper without
/// settlement days, on a surface built on a fixed date, is refused, as QuantLib refuses it.
/// </para>
/// <para>
/// The strike interpolation at each fixing date is built with the adapter, where QuantLib builds it at its first use;
/// the time interpolation and the smile sections are built at each call, as QuantLib's are. A smile section is a cubic
/// spline over the first fixing date's strikes, with a Lagrange boundary from four strikes and a natural one below.
/// </para>
/// </remarks>
public sealed class StrippedOptionletAdapter : OptionletVolatilityStructure
{
    private readonly StrippedOptionletBase optionletStripper;
    private readonly ImmutableArray<Interpolation> strikeInterpolations;

    private StrippedOptionletAdapter(
        Date referenceDate, StrippedOptionletBase s, int settlementDays, ImmutableArray<Interpolation> strikeInterpolations)
        : base(referenceDate, s.Calendar, s.BusinessDayConvention, s.DayCounter, settlementDays)
    {
        optionletStripper = s;
        this.strikeInterpolations = strikeInterpolations;
    }

    /// <summary>Gets the stripper, when it is an <see cref="TermStructures.OptionletStripper"/>.</summary>
    public OptionletStripper? OptionletStripper => optionletStripper as OptionletStripper;

    /// <summary>Gets the last fixing date.</summary>
    public override Date MaxDate => optionletStripper.OptionletFixingDates[^1];

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => optionletStripper.VolatilityType;

    /// <inheritdoc/>
    public override double Displacement => optionletStripper.Displacement;

    /// <summary>
    /// An adapter on a stripper, whose reference date is the stripper's settlement days after the evaluation date.
    /// </summary>
    /// <param name="stripper">The stripped optionlet volatilities.</param>
    /// <param name="context">The evaluation context, whose evaluation date sets the reference date.</param>
    /// <returns>The adapter, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stripper"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The stripper has no settlement days, or a fixing date has fewer than two
    /// strikes to interpolate, which QuantLib refuses at its first calculation.</exception>
    /// <exception cref="NanoQLException">The stripper has no calendar to move the evaluation date by its settlement
    /// days.</exception>
    public static StrippedOptionletAdapter Create(StrippedOptionletBase stripper, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(stripper);
        ArgumentNullException.ThrowIfNull(context);
        int settlementDays = stripper.SettlementDays
            ?? throw new ArgumentException("settlement days not provided for this instance", nameof(stripper));
        Calendar calendar = stripper.Calendar ?? EmptyCalendar.Instance;
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new StrippedOptionletAdapter(referenceDate, stripper, settlementDays, StrikeInterpolations(stripper));
    }

    /// <summary>The first fixing date's lowest strike.</summary>
    /// <returns>The strike.</returns>
    /// <exception cref="NanoQLException">The stripper has no fixing dates, where QuantLib reads past the first date's
    /// strikes.</exception>
    public override double MinStrike()
    {
        ImmutableArray<double> strikes = optionletStripper.OptionletStrikes(0);
        return strikes.IsEmpty ? throw new NanoQLException("no strikes given") : strikes[0];
    }

    /// <summary>The first fixing date's highest strike.</summary>
    /// <returns>The strike.</returns>
    /// <exception cref="NanoQLException">The stripper has no fixing dates, where QuantLib reads past the first date's
    /// strikes.</exception>
    public override double MaxStrike()
    {
        ImmutableArray<double> strikes = optionletStripper.OptionletStrikes(0);
        return strikes.IsEmpty ? throw new NanoQLException("no strikes given") : strikes[^1];
    }

    /// <summary>
    /// The smile section at an option time: a cubic spline over the first fixing date's strikes, through the
    /// structure's standard deviations at the time.
    /// </summary>
    /// <param name="optionTime">The option time.</param>
    /// <returns>The smile section, with no at-the-money level, on Actual/365 (Fixed).</returns>
    protected override SmileSection SmileSectionImpl(double optionTime)
    {
        // strikes are the same for all times ?!
        ImmutableArray<double> optionletStrikes = optionletStripper.OptionletStrikes(0);
        double[] stddevs = new double[optionletStrikes.Length];
        for (int i = 0; i < optionletStrikes.Length; ++i)
        {
            stddevs[i] = VolatilityImpl(optionTime, optionletStrikes[i]) * Math.Sqrt(optionTime);
        }

        // Extrapolation may be a problem with splines, but since minStrike() and maxStrike() are set, we assume that no
        // one will use stddevs for strikes outside these strikes
        CubicInterpolationBoundaryCondition bc = optionletStrikes.Length >= 4
            ? CubicInterpolationBoundaryCondition.Lagrange
            : CubicInterpolationBoundaryCondition.SecondDerivative;
        return new InterpolatedSmileSection<Cubic>(
            optionTime,
            optionletStrikes.AsSpan(),
            stddevs,
            null,
            new Cubic(CubicInterpolationDerivativeApprox.Spline, false, bc, 0.0, bc, 0.0),
            new Actual365Fixed(),
            VolatilityType,
            Displacement);
    }

    // QuantLib's performCalculations, at construction: a linear interpolation over each fixing date's strikes.
    private static ImmutableArray<Interpolation> StrikeInterpolations(StrippedOptionletBase s)
    {
        int nInterpolations = s.OptionletMaturities;
        ImmutableArray<Interpolation>.Builder interpolations = ImmutableArray.CreateBuilder<Interpolation>(nInterpolations);
        for (int i = 0; i < nInterpolations; ++i)
        {
            ImmutableArray<double> optionletStrikes = s.OptionletStrikes(i);
            ImmutableArray<double> optionletVolatilities = s.OptionletVolatilities(i);
            try
            {
                interpolations.Add(new Linear().Interpolate(optionletStrikes.AsSpan(), optionletVolatilities.AsSpan()));
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(DiscountingSwapEngine.QuantLibText(e), "stripper", e);
            }
        }

        return interpolations.MoveToImmutable();
    }

    /// <summary>
    /// The volatility at an option time and a strike: each fixing date's volatility at the strike, interpolated
    /// linearly and extrapolated, then interpolated linearly in time and extrapolated.
    /// </summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double VolatilityImpl(double optionTime, double strike)
    {
        double[] vol = new double[strikeInterpolations.Length];
        for (int i = 0; i < strikeInterpolations.Length; ++i)
        {
            vol[i] = strikeInterpolations[i].Evaluate(strike, true);
        }

        ImmutableArray<double> optionletTimes = optionletStripper.OptionletFixingTimes;
        Interpolation timeInterpolator = new Linear().Interpolate(optionletTimes.AsSpan(), vol);
        return timeInterpolator.Evaluate(optionTime, true);
    }
}
