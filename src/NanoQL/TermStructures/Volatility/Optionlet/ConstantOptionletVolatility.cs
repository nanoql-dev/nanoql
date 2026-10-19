// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/constantoptionletvol.hpp ql/termstructures/volatility/optionlet/constantoptionletvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A constant caplet and floorlet volatility, for every option date and strike.</summary>
/// <remarks>
/// QuantLib's four constructors collapse into two: its quote overloads are the <see cref="double"/> ones, and the
/// settlement-days form, whose reference date moves with the evaluation date, is <see cref="Create"/>.
/// </remarks>
public sealed class ConstantOptionletVolatility : OptionletVolatilityStructure
{
    private readonly double volatility;
    private readonly VolatilityType type;
    private readonly double displacement;

    /// <summary>Initializes a new instance of the <see cref="ConstantOptionletVolatility"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar, with
    /// which option dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="displacement">The displacement of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> or <paramref name="type"/> isn't a member of
    /// its type.</exception>
    public ConstantOptionletVolatility(
        Date referenceDate,
        Calendar? cal,
        BusinessDayConvention bdc,
        double volatility,
        DayCounter dc,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
        : this(referenceDate, cal, bdc, volatility, dc, type, displacement, null)
    {
    }

    private ConstantOptionletVolatility(
        Date referenceDate,
        Calendar? cal,
        BusinessDayConvention bdc,
        double volatility,
        DayCounter dc,
        VolatilityType type,
        double displacement,
        int? settlementDays)
        : base(referenceDate, cal, bdc, dc, settlementDays)
    {
        EnumArguments.Check(type, nameof(type));
        this.volatility = volatility;
        this.type = type;
        this.displacement = displacement;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => type;

    /// <inheritdoc/>
    public override double Displacement => displacement;

    /// <summary>
    /// A constant volatility whose reference date is the given number of business days after the evaluation date.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="displacement">The displacement of a shifted lognormal volatility.</param>
    /// <returns>The structure, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="cal"/> or
    /// <paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> or <paramref name="type"/> isn't a member of its type.</exception>
    public static ConstantOptionletVolatility Create(
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        double volatility,
        DayCounter dc,
        EvaluationContext context,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(cal);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new ConstantOptionletVolatility(referenceDate, cal, bdc, volatility, dc, type, displacement, settlementDays);
    }

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>
    /// A flat smile section on the option date, with the structure's day counter and reference date; as QuantLib's, it
    /// takes the section's defaults for the rest: no at-the-money level, shifted lognormal and no shift, whatever the
    /// structure's type and displacement.
    /// </summary>
    /// <param name="optionDate">The option date.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(Date optionDate) =>
        new FlatSmileSection(optionDate, volatility, DayCounter, ReferenceDate);

    /// <summary>
    /// A flat smile section on the option time, with the structure's day counter; as QuantLib's, it takes the section's
    /// defaults for the rest, whatever the structure's type and displacement.
    /// </summary>
    /// <param name="optionTime">The option time.</param>
    /// <returns>The smile section.</returns>
    protected override SmileSection SmileSectionImpl(double optionTime) =>
        new FlatSmileSection(optionTime, volatility, DayCounter);

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double strike) => volatility;
}
