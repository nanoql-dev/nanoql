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

/// <summary>QuantLib-Python's <c>CubicInterpolatedSmileSection</c>: an <see cref="InterpolatedSmileSection{TInterpolator}"/> with cubic interpolation, QuantLib's default <see cref="Cubic"/> unless another is given.</summary>
public sealed class CubicInterpolatedSmileSection : InterpolatedSmileSection<Cubic>
{
    /// <summary>Initializes a new instance of the <see cref="CubicInterpolatedSmileSection"/> class for an exercise time.</summary>
    /// <param name="expiryTime">The exercise time.</param>
    /// <param name="strikes">The strikes, sorted.</param>
    /// <param name="stdDevs">The standard deviations, at least one per strike.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// none, which leaves the section unable to price options.</param>
    /// <param name="interpolator">The interpolation's factory.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public CubicInterpolatedSmileSection(
        double expiryTime,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        Cubic interpolator = default,
        DayCounter? dc = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
        : base(expiryTime, strikes, stdDevs, atmLevel, interpolator, dc, type, shift, flatStrikeExtrapolation)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CubicInterpolatedSmileSection"/> class for an exercise date.</summary>
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
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public CubicInterpolatedSmileSection(
        Date d,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        DayCounter? dc,
        Cubic interpolator,
        Date referenceDate,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
        : base(d, strikes, stdDevs, atmLevel, dc, interpolator, referenceDate, type, shift, flatStrikeExtrapolation)
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
    /// <param name="interpolator">The interpolation's factory.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="flatStrikeExtrapolation">Whether the volatility outside the strikes is the nearest end's.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static new CubicInterpolatedSmileSection Create(
        Date d,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> stdDevs,
        double? atmLevel,
        EvaluationContext context,
        DayCounter? dc = null,
        Cubic interpolator = default,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0,
        bool flatStrikeExtrapolation = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new CubicInterpolatedSmileSection(
            d, strikes, stdDevs, atmLevel, dc, interpolator, context.EvaluationDate, type, shift, flatStrikeExtrapolation);
    }
}
