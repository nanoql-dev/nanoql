// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/spreadedoptionletvol.hpp ql/termstructures/volatility/optionlet/spreadedoptionletvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>An optionlet volatility structure that adds a constant spread to another's volatilities.</summary>
/// <remarks>
/// QuantLib's spread quote is its value: a new spread is a new structure. The base's dates, calendar, conventions,
/// strikes, type and displacement are the structure's, and so is its extrapolation flag, read when the structure is
/// built, as QuantLib reads it. The base's volatilities and smile sections are read with extrapolation.
/// </remarks>
public sealed class SpreadedOptionletVolatility : OptionletVolatilityStructure
{
    private readonly OptionletVolatilityStructure baseVol;
    private readonly double spread;

    /// <summary>Initializes a new instance of the <see cref="SpreadedOptionletVolatility"/> class.</summary>
    /// <param name="baseVol">The base structure.</param>
    /// <param name="spread">The spread added to its volatilities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseVol"/> is null.</exception>
    public SpreadedOptionletVolatility(OptionletVolatilityStructure baseVol, double spread)
        : base(
            Checked(baseVol).ReferenceDate,
            baseVol.Calendar,
            baseVol.BusinessDayConvention,
            baseVol.DayCounter,
            baseVol.SettlementDays)
    {
        this.baseVol = baseVol;
        this.spread = spread;
        AllowsExtrapolation = baseVol.AllowsExtrapolation;
    }

    /// <inheritdoc/>
    public override BusinessDayConvention BusinessDayConvention => baseVol.BusinessDayConvention;

    /// <inheritdoc/>
    public override DayCounter DayCounter => baseVol.DayCounter;

    /// <inheritdoc/>
    public override Date MaxDate => baseVol.MaxDate;

    /// <inheritdoc/>
    public override double MaxTime => baseVol.MaxTime;

    /// <inheritdoc/>
    public override Calendar? Calendar => baseVol.Calendar;

    /// <inheritdoc/>
    public override int? SettlementDays => baseVol.SettlementDays;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => baseVol.VolatilityType;

    /// <inheritdoc/>
    public override double Displacement => baseVol.Displacement;

    /// <inheritdoc/>
    public override double MinStrike() => baseVol.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => baseVol.MaxStrike();

    /// <summary>The base's smile section at the date, spreaded.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(Date optionDate) =>
        new SpreadedSmileSection(baseVol.SmileSection(optionDate, true), spread);

    /// <summary>The base's smile section at the time, spreaded.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(double optionTime) =>
        new SpreadedSmileSection(baseVol.SmileSection(optionTime, true), spread);

    /// <summary>The base's volatility plus the spread.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double VolatilityImpl(double optionTime, double strike) =>
        baseVol.Volatility(optionTime, strike, true) + spread;

    private static OptionletVolatilityStructure Checked(OptionletVolatilityStructure baseVol)
    {
        ArgumentNullException.ThrowIfNull(baseVol);
        return baseVol;
    }
}
