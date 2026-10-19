// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/interpolatedsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A smile section interpolated between volatilities at given strikes, from the standard deviations given there.
/// </summary>
/// <typeparam name="TInterpolator">The interpolation's factory.</typeparam>
/// <remarks>
/// <para>
/// The volatilities are the standard deviations over the square root of the exercise time, and the interpolation over
/// them is built when the section is built. QuantLib's quote forms are the value forms here, a quote being its value: a
/// new value is a new section. Outside the strikes the interpolation extrapolates, floored at 0; with flat strike
/// extrapolation it takes its value at the nearest end strike instead. A standard deviation beyond the strikes' count
/// is ignored, as QuantLib ignores it, and fewer standard deviations than strikes are refused, where QuantLib reads
/// past them. QuantLib's date forms without a reference date, which float with the evaluation date, are the
/// <c>Create</c> factories, which take the evaluation context and fix the date.
/// </para>
/// <para>
/// Unsealed for its named classes, QuantLib-Python's (<see cref="LinearInterpolatedSmileSection"/> and the three
/// cubic ones). Its constructor reads the exercise time, so <see cref="ExerciseTime"/> is sealed here.
/// </para>
/// </remarks>
public class InterpolatedSmileSection<TInterpolator> : SmileSection
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<double> strikes;
    private readonly double? atmLevel;
    private readonly bool flatStrikeExtrapolation;
    private readonly Interpolation interpolation;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedSmileSection{TInterpolator}"/> class for an
    /// exercise time.</summary>
    /// <param name="expiryTime">The exercise time.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="interpolator">The interpolation's factory.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <exception cref="ArgumentException">The time is negative, the strikes aren't sorted, there are fewer standard
    /// deviations than strikes, or the interpolation refuses the points.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public InterpolatedSmileSection(
        double expiryTime,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        TInterpolator interpolator = default,
        DayCounter? dc = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
        : base(expiryTime, dc ?? new Actual365Fixed(), type, shift)
    {
        this.strikes = [.. strikes];
        this.atmLevel = atmLevel;
        this.flatStrikeExtrapolation = flatStrikeExtrapolation;
        interpolation = Interpolate(strikes, stdDevs, Math.Sqrt(base.ExerciseTime), interpolator);
    }

    /// <summary>Initializes a new instance of the <see cref="InterpolatedSmileSection{TInterpolator}"/> class for an
    /// exercise date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="interpolator">The interpolation's factory.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, the exercise date is before the reference
    /// date, the strikes aren't sorted, there are fewer standard deviations than strikes, or the interpolation refuses
    /// the points.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public InterpolatedSmileSection(
        Date d,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        DayCounter? dc,
        TInterpolator interpolator,
        Date referenceDate,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
        : base(d, dc ?? new Actual365Fixed(), referenceDate, type, shift)
    {
        this.strikes = [.. strikes];
        this.atmLevel = atmLevel;
        this.flatStrikeExtrapolation = flatStrikeExtrapolation;
        interpolation = Interpolate(strikes, stdDevs, Math.Sqrt(base.ExerciseTime), interpolator);
    }

    /// <summary>Gets the exercise time.</summary>
    /// <remarks>Sealed: the constructor reads it.</remarks>
    public sealed override double ExerciseTime => base.ExerciseTime;

    /// <summary>
    /// An interpolated smile section for an exercise date, whose reference date is the evaluation date.
    /// </summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="interpolator">The interpolation's factory.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static InterpolatedSmileSection<TInterpolator> Create(
        Date d,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        EvaluationContext context,
        DayCounter? dc = null,
        TInterpolator interpolator = default,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new InterpolatedSmileSection<TInterpolator>(
            d, strikes, stdDevs, atmLevel, dc, interpolator, context.EvaluationDate, type, shift, flatStrikeExtrapolation);
    }

    /// <summary>The minimum strike: the first.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => strikes[0];

    /// <summary>The maximum strike: the last.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => strikes[^1];

    /// <summary>The at-the-money level, as given.</summary>
    /// <returns>The level; <see langword="null"/> where none was given.</returns>
    public override double? AtmLevel() => atmLevel;

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike)
    {
        double v = VolatilityImpl(strike);
        return v * v * ExerciseTime;
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike)
    {
        if (flatStrikeExtrapolation)
        {
            if (strike < MinStrike())
            {
                return interpolation.Evaluate(MinStrike(), true);
            }
            else if (strike > MaxStrike())
            {
                return interpolation.Evaluate(MaxStrike(), true);
            }
        }

        double v = interpolation.Evaluate(strike, true);
        return v < 0.0 ? 0.0 : v;
    }

    // QuantLib's checkStrikes and the interpolation over the strikes' count of volatilities, which QuantLib's
    // performCalculations fills before its first use.
    private static Interpolation Interpolate(
        ReadOnlySpan<double> strikes, ReadOnlySpan<double> stdDevs, double exerciseTimeSquareRoot, TInterpolator interpolator)
    {
        for (int i = 1; i < strikes.Length; ++i)
        {
            if (strikes[i] < strikes[i - 1])
            {
                throw new ArgumentException("Strikes have to be sorted in ascending order", nameof(strikes));
            }
        }

        // QuantLib's interpolation reads one volatility per strike, past the given ones when they are fewer.
        if (!(strikes.Length <= stdDevs.Length))
        {
            throw new ArgumentException(
                $"{strikes.Length} strikes given for {stdDevs.Length} standard deviations", nameof(strikes));
        }

        double[] vols = new double[stdDevs.Length];
        for (int i = 0; i < stdDevs.Length; ++i)
        {
            vols[i] = stdDevs[i] / exerciseTimeSquareRoot;
        }

        return interpolator.Interpolate(strikes, vols.AsSpan(0, strikes.Length));
    }
}
