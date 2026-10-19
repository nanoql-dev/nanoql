// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/interpolatedsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>QuantLib-Python's <c>MonotonicCubicInterpolatedSmileSection</c>: an <see cref="InterpolatedSmileSection{TInterpolator}"/> with QuantLib-Python's <c>MonotonicCubic</c>, <see cref="Cubic"/> with the spline's derivatives and Hyman's monotonicity filter.</summary>
public sealed class MonotonicCubicInterpolatedSmileSection : InterpolatedSmileSection<Cubic>
{
    // QuantLib-Python's preset.
    private static readonly Cubic Preset = new(CubicInterpolationDerivativeApprox.Spline, true);

    /// <summary>Initializes a new instance of the <see cref="MonotonicCubicInterpolatedSmileSection"/> class for an exercise time.</summary>
    /// <param name="expiryTime">The exercise time.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public MonotonicCubicInterpolatedSmileSection(
        double expiryTime,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        DayCounter? dc = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
        : base(expiryTime, strikes, stdDevs, atmLevel, Preset, dc, type, shift, flatStrikeExtrapolation)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MonotonicCubicInterpolatedSmileSection"/> class for an exercise date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public MonotonicCubicInterpolatedSmileSection(
        Date d,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        DayCounter? dc,
        Date referenceDate,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
        : base(d, strikes, stdDevs, atmLevel, dc, Preset, referenceDate, type, shift, flatStrikeExtrapolation)
    {
    }

    /// <summary>A section for an exercise date, whose reference date is the evaluation date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static MonotonicCubicInterpolatedSmileSection Create(
        Date d,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        EvaluationContext context,
        DayCounter? dc = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MonotonicCubicInterpolatedSmileSection(
            d, strikes, stdDevs, atmLevel, dc, context.EvaluationDate, type, shift, flatStrikeExtrapolation);
    }
}
