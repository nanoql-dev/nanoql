// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/spreadedsmilesection.hpp ql/termstructures/volatility/spreadedsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A smile section shifted by a volatility spread: another section's volatilities plus the spread.</summary>
/// <remarks>
/// Every inspector answers the underlying section's. QuantLib's spread is a quote; here it is its value, and a new
/// spread is a new section. QuantLib default-constructs its base, so its converted volatility
/// (<see cref="SmileSection.Volatility(double, VolatilityType, double)"/>) compares the type wanted with an
/// uninitialized one; here the base is given the underlying's exercise time, day counter, type and shift, so it
/// compares the underlying's type.
/// </remarks>
public sealed class SpreadedSmileSection : SmileSection
{
    private readonly SmileSection underlyingSection;
    private readonly double spread;

    /// <summary>Initializes a new instance of the <see cref="SpreadedSmileSection"/> class.</summary>
    /// <param name="underlyingSection">The section spreaded.</param>
    /// <param name="spread">The volatility spread.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlyingSection"/> is null.</exception>
    public SpreadedSmileSection(SmileSection underlyingSection, double spread)
        : base(
            Underlying(underlyingSection).ExerciseTime,
            underlyingSection.DayCounter,
            underlyingSection.VolatilityType,
            underlyingSection.Shift)
    {
        this.underlyingSection = underlyingSection;
        this.spread = spread;
    }

    /// <inheritdoc/>
    public override Date? ExerciseDate => underlyingSection.ExerciseDate;

    /// <inheritdoc/>
    public override double ExerciseTime => underlyingSection.ExerciseTime;

    /// <inheritdoc/>
    public override DayCounter? DayCounter => underlyingSection.DayCounter;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => underlyingSection.VolatilityType;

    /// <inheritdoc/>
    public override double Shift => underlyingSection.Shift;

    /// <inheritdoc/>
    public override Date ReferenceDate() => underlyingSection.ReferenceDate();

    /// <inheritdoc/>
    public override double MinStrike() => underlyingSection.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => underlyingSection.MaxStrike();

    /// <inheritdoc/>
    public override double? AtmLevel() => underlyingSection.AtmLevel();

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => underlyingSection.Volatility(strike) + spread;

    private static SmileSection Underlying(SmileSection underlyingSection)
    {
        ArgumentNullException.ThrowIfNull(underlyingSection);
        return underlyingSection;
    }
}
