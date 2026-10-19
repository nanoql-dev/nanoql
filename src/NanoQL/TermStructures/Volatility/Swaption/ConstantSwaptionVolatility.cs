// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/swaptionconstantvol.hpp ql/termstructures/volatility/swaption/swaptionconstantvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A constant swaption volatility, for every option date, swap tenor and strike.</summary>
/// <remarks>
/// QuantLib's four constructors collapse into two: its quote overloads are the <see cref="double"/> ones, and the
/// settlement-days form, whose reference date moves with the evaluation date, is <see cref="Create"/>. Its smile
/// sections are flat, with the structure's volatility type and shift and no at-the-money level: by date, with the
/// structure's reference date, for an option date and a swap tenor, and by time otherwise.
/// </remarks>
public sealed class ConstantSwaptionVolatility : SwaptionVolatilityStructure
{
    private static readonly Period MaxTenor = new(100, TimeUnit.Years);

    private readonly double volatility;
    private readonly VolatilityType volatilityType;
    private readonly double shift;

    /// <summary>Initializes a new instance of the <see cref="ConstantSwaptionVolatility"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar, with
    /// which option dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> or <paramref name="type"/> isn't a member of
    /// its type.</exception>
    public ConstantSwaptionVolatility(
        Date referenceDate,
        Calendar? cal,
        BusinessDayConvention bdc,
        double volatility,
        DayCounter dc,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
        : this(referenceDate, cal, bdc, volatility, dc, type, shift, null)
    {
    }

    private ConstantSwaptionVolatility(
        Date referenceDate,
        Calendar? cal,
        BusinessDayConvention bdc,
        double volatility,
        DayCounter dc,
        VolatilityType type,
        double shift,
        int? settlementDays)
        : base(referenceDate, cal, bdc, dc, settlementDays)
    {
        EnumArguments.Check(type, nameof(type));
        this.volatility = volatility;
        volatilityType = type;
        this.shift = shift;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>Gets the largest swap tenor for which the structure returns volatilities: 100 years.</summary>
    public override Period MaxSwapTenor => MaxTenor;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => volatilityType;

    /// <summary>
    /// A constant swaption volatility whose reference date is the given number of business days after the evaluation
    /// date.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <returns>The structure, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="cal"/> or
    /// <paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> or <paramref name="type"/> isn't a member of its type.</exception>
    public static ConstantSwaptionVolatility Create(
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        double volatility,
        DayCounter dc,
        EvaluationContext context,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(cal);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new ConstantSwaptionVolatility(referenceDate, cal, bdc, volatility, dc, type, shift, settlementDays);
    }

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(Date optionDate, Period swapTenor) =>
        new FlatSmileSection(optionDate, volatility, DayCounter, ReferenceDate, null, volatilityType, shift);

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength) =>
        new FlatSmileSection(optionTime, volatility, DayCounter, null, volatilityType, shift);

    /// <inheritdoc/>
    protected override double VolatilityImpl(Date optionDate, Period swapTenor, double strike) => volatility;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double swapLength, double strike) => volatility;

    /// <summary>The structure's shift, for a shifted lognormal structure only.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The shift.</returns>
    /// <exception cref="NanoQLException">The structure's volatilities are normal.</exception>
    protected override double ShiftImpl(double optionTime, double swapLength)
    {
        // consistency check
        _ = base.ShiftImpl(optionTime, swapLength);
        return shift;
    }
}
