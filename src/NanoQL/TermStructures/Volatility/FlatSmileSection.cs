// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/flatsmilesection.hpp ql/termstructures/volatility/flatsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A flat smile section: one volatility at every strike.</summary>
/// <remarks>
/// QuantLib's date constructor without a reference date, whose section floats with the evaluation date, is
/// <see cref="Create"/>, which takes the evaluation context and fixes the date. The date forms need a day counter for
/// the exercise time, which QuantLib's empty one can't give.
/// </remarks>
public sealed class FlatSmileSection : SmileSection
{
    private readonly double vol;
    private readonly double? atmLevel;

    /// <summary>Initializes a new instance of the <see cref="FlatSmileSection"/> class for an exercise date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="dc">The day counter of the exercise time.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/> for none, which leaves the section
    /// unable to price options.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the exercise date is before the
    /// reference date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public FlatSmileSection(
        Date d,
        double vol,
        DayCounter dc,
        Date referenceDate,
        double? atmLevel = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
        : base(d, dc, referenceDate, type, shift)
    {
        this.vol = vol;
        this.atmLevel = atmLevel;
    }

    /// <summary>Initializes a new instance of the <see cref="FlatSmileSection"/> class for an exercise time.</summary>
    /// <param name="exerciseTime">The exercise time.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="dc">The day counter, or <see langword="null"/>, QuantLib's empty one; the section only returns
    /// it.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/> for none.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentException"><paramref name="exerciseTime"/> is negative or NaN.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public FlatSmileSection(
        double exerciseTime,
        double vol,
        DayCounter? dc,
        double? atmLevel = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
        : base(exerciseTime, dc, type, shift)
    {
        this.vol = vol;
        this.atmLevel = atmLevel;
    }

    /// <summary>A flat smile section for an exercise date, whose reference date is the evaluation date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="dc">The day counter of the exercise time.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="atmLevel">The at-the-money level, or <see langword="null"/> for none.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c> or before the evaluation
    /// date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static FlatSmileSection Create(
        Date d,
        double vol,
        DayCounter dc,
        EvaluationContext context,
        double? atmLevel = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new FlatSmileSection(d, vol, dc, context.EvaluationDate, atmLevel, type, shift);
    }

    /// <summary>The minimum strike: the lowest real less the shift.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL - Shift;

    /// <summary>The maximum strike: the largest real.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    public override double? AtmLevel() => atmLevel;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => vol;
}
