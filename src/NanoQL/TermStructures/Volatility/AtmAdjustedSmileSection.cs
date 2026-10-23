// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/atmadjustedsmilesection.hpp ql/termstructures/volatility/atmadjustedsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A smile section with an at-the-money level of its own, the source section's smile optionally recentred on it: the
/// volatility at a strike is the source's at that strike moved by the source's level less the given one.
/// </summary>
/// <remarks>
/// <para>
/// Every inspector but <see cref="AtmLevel"/> answers the source section's. Without recentering, or where either level
/// is missing, the adjustment is 0. The base is given the source's exercise time, day counter, type and shift, as
/// <see cref="AtmSmileSection"/>'s.
/// </para>
/// <para>
/// The option prices, digitals, vega and density are the base's formulas on the section's own level and volatility,
/// as <see cref="AtmSmileSection"/> prices. QuantLib's return the source's at the adjusted strike
/// (<c>atmadjustedsmilesection.hpp:49-69</c>), priced on the source's forward, so without recentering every price
/// ignores the level the section reports, and with it they agree with the section's own level and volatility only
/// under Bachelier's formula, whose price reads the strike and the forward only through their difference.
/// </para>
/// </remarks>
public sealed class AtmAdjustedSmileSection : SmileSection
{
    private readonly SmileSection source;
    private readonly double adjustment;
    private readonly double? f;

    /// <summary>Initializes a new instance of the <see cref="AtmAdjustedSmileSection"/> class.</summary>
    /// <param name="source">The section whose volatilities it reads.</param>
    /// <param name="atm">The at-the-money level; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for the
    /// source's own.</param>
    /// <param name="recenterSmile">Whether the source's smile is moved to the given level.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public AtmAdjustedSmileSection(SmileSection source, double? atm = null, bool recenterSmile = false)
        : base(Source(source).ExerciseTime, source.DayCounter, source.VolatilityType, source.Shift)
    {
        this.source = source;
        f = atm ?? source.AtmLevel();
        double? sourceAtm = source.AtmLevel();
        adjustment = recenterSmile && f is double level && sourceAtm is double s ? s - level : 0.0;
    }

    /// <inheritdoc/>
    public override Date? ExerciseDate => source.ExerciseDate;

    /// <inheritdoc/>
    public override double ExerciseTime => source.ExerciseTime;

    /// <inheritdoc/>
    public override DayCounter? DayCounter => source.DayCounter;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => source.VolatilityType;

    /// <inheritdoc/>
    public override double Shift => source.Shift;

    /// <inheritdoc/>
    public override Date ReferenceDate() => source.ReferenceDate();

    /// <inheritdoc/>
    public override double MinStrike() => source.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => source.MaxStrike();

    /// <inheritdoc/>
    public override double? AtmLevel() => f;

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike) => source.Variance(AdjustedStrike(strike));

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => source.Volatility(AdjustedStrike(strike));

    private static SmileSection Source(SmileSection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }

    private double AdjustedStrike(double strike) => strike + adjustment;
}
