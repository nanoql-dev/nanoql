// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/spreadedswaptionvol.hpp ql/termstructures/volatility/swaption/spreadedswaptionvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A swaption volatility structure that adds a constant spread to another's volatilities.</summary>
/// <remarks>
/// QuantLib's spread quote is its value: a new spread is a new structure. The base's dates, calendar, conventions,
/// strikes, maximum swap tenor, type and shifts are the structure's, and so is its extrapolation flag, read when the
/// structure is built, as QuantLib reads it. The base's volatilities, shifts and smile sections are read with
/// extrapolation, and a smile section is a <see cref="SpreadedSmileSection"/> over the base's.
/// </remarks>
public sealed class SpreadedSwaptionVolatility : SwaptionVolatilityStructure
{
    private readonly SwaptionVolatilityStructure baseVol;
    private readonly double spread;

    /// <summary>Initializes a new instance of the <see cref="SpreadedSwaptionVolatility"/> class.</summary>
    /// <param name="baseVol">The base structure.</param>
    /// <param name="spread">The spread added to its volatilities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseVol"/> is null.</exception>
    public SpreadedSwaptionVolatility(SwaptionVolatilityStructure baseVol, double spread)
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
    public override Period MaxSwapTenor => baseVol.MaxSwapTenor;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => baseVol.VolatilityType;

    /// <inheritdoc/>
    public override double MinStrike() => baseVol.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => baseVol.MaxStrike();

    /// <summary>The base's smile section at the date and swap tenor, spreaded.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(Date optionDate, Period swapTenor) =>
        new SpreadedSmileSection(baseVol.SmileSection(optionDate, swapTenor, true), spread);

    /// <summary>The base's smile section at the time and swap length, spreaded.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength) =>
        new SpreadedSmileSection(baseVol.SmileSection(optionTime, swapLength, true), spread);

    /// <summary>The base's volatility plus the spread.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double VolatilityImpl(Date optionDate, Period swapTenor, double strike) =>
        baseVol.Volatility(optionDate, swapTenor, strike, true) + spread;

    /// <summary>The base's volatility plus the spread.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double VolatilityImpl(double optionTime, double swapLength, double strike) =>
        baseVol.Volatility(optionTime, swapLength, strike, true) + spread;

    /// <summary>The base's shift, with extrapolation.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The shift.</returns>
    protected override double ShiftImpl(double optionTime, double swapLength) =>
        baseVol.Shift(optionTime, swapLength, true);

    private static SwaptionVolatilityStructure Checked(SwaptionVolatilityStructure baseVol)
    {
        ArgumentNullException.ThrowIfNull(baseVol);
        return baseVol;
    }
}
