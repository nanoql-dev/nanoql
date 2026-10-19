// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/capfloor/capfloortermvolatilitystructure.hpp ql/termstructures/volatility/capfloor/capfloortermvolatilitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (bdc, dc) (not ported: no QuantLib class calls it)

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base class of cap/floor term-volatility structures: the volatility of a cap or floor of a given length and
/// strike.
/// </summary>
/// <remarks>
/// An extension point, as QuantLib's abstract class is: a derived structure implements <see cref="VolatilityImpl"/>,
/// which the <c>Volatility</c> members call after their range and strike checks, and the inspectors its base leaves
/// abstract. Its reference date is fixed; a derived structure whose reference date QuantLib moves with the evaluation
/// date passes its settlement days, as its <c>Create</c> factory computes it from the context.
/// </remarks>
public abstract class CapFloorTermVolatilityStructure : VolatilityTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="CapFloorTermVolatilityStructure"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// with which option dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type, or
    /// <paramref name="settlementDays"/> is negative.</exception>
    protected CapFloorTermVolatilityStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The volatility for a given cap/floor length and strike rate.</summary>
    /// <param name="length">The length, from the reference date: its option date is computed on the calendar.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">
    /// The structure has no calendar, or the date or the strike is outside the structure's domain.
    /// </exception>
    public double Volatility(Period length, double strike, bool extrapolate = false)
    {
        Date d = OptionDateFromTenor(length);
        return Volatility(d, strike, extrapolate);
    }

    /// <summary>The volatility for a given end date and strike rate.</summary>
    /// <param name="end">The end date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date or the strike is outside the structure's domain.</exception>
    public double Volatility(Date end, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(end, nameof(end));
        CheckRange(end, extrapolate);
        double t = TimeFromReference(end);
        return Volatility(t, strike, extrapolate);
    }

    /// <summary>The volatility for a given end time and strike rate.</summary>
    /// <param name="t">The end time, from the reference date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The time or the strike is outside the structure's domain.</exception>
    public double Volatility(double t, double strike, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        CheckStrike(strike, extrapolate);
        return VolatilityImpl(t, strike);
    }

    /// <summary>The volatility calculation, after the range and strike checks.</summary>
    /// <param name="length">The time, from the reference date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double VolatilityImpl(double length, double strike);
}
