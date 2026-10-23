// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/atmsmilesection.hpp ql/termstructures/volatility/atmsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A smile section with an at-the-money level of its own: another section's volatilities, its options priced on the
/// given level.
/// </summary>
/// <remarks>
/// Every inspector but <see cref="AtmLevel"/> answers the source section's, and so do the volatility and the variance;
/// the option prices are the base's formulas on the given level. QuantLib copies the source's base into its own; here
/// the base is given the source's exercise time, day counter, type and shift, so its converted volatility
/// (<see cref="SmileSection.Volatility(double, VolatilityType, double)"/>) compares the source's type, as QuantLib's
/// copy does.
/// </remarks>
public sealed class AtmSmileSection : SmileSection
{
    private readonly SmileSection source;
    private readonly double? f;

    /// <summary>Initializes a new instance of the <see cref="AtmSmileSection"/> class.</summary>
    /// <param name="source">The section whose volatilities it reads.</param>
    /// <param name="atm">The at-the-money level; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for the
    /// source's own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public AtmSmileSection(SmileSection source, double? atm = null)
        : base(Source(source).ExerciseTime, source.DayCounter, source.VolatilityType, source.Shift)
    {
        this.source = source;
        f = atm ?? source.AtmLevel();
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
    protected override double VolatilityImpl(double strike) => source.Volatility(strike);

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike) => source.Variance(strike);

    private static SmileSection Source(SmileSection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }
}
