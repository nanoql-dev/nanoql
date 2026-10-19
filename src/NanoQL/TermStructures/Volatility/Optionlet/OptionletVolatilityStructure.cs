// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2015 Michael von den Driesch
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp ql/termstructures/volatility/optionlet/optionletvolatilitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, which waits for its first user (S8)

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>The base class of caplet and floorlet volatility structures.</summary>
public abstract class OptionletVolatilityStructure : VolatilityTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="OptionletVolatilityStructure"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    protected OptionletVolatilityStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>Gets the type of the volatilities the structure returns; by default shifted lognormal.</summary>
    public virtual VolatilityType VolatilityType => VolatilityType.ShiftedLognormal;

    /// <summary>Gets the displacement of shifted lognormal volatilities; by default 0.</summary>
    public virtual double Displacement => 0.0;

    /// <summary>The volatility for an option tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    public double Volatility(Period optionTenor, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return Volatility(optionDate, strike, extrapolate);
    }

    /// <summary>The volatility for an option date and a strike.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The date or the strike is outside the structure's domain.</exception>
    public double Volatility(Date optionDate, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckRange(optionDate, extrapolate);
        CheckStrike(strike, extrapolate);
        return VolatilityImpl(optionDate, strike);
    }

    /// <summary>The volatility for an option time and a strike.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The time or the strike is outside the structure's domain.</exception>
    public double Volatility(double optionTime, double strike, bool extrapolate = false)
    {
        CheckRange(optionTime, extrapolate);
        CheckStrike(strike, extrapolate);
        return VolatilityImpl(optionTime, strike);
    }

    /// <summary>The Black variance for an option tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(Period optionTenor, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return BlackVariance(optionDate, strike, extrapolate);
    }

    /// <summary>The Black variance for an option date and a strike: the volatility squared times the time.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(Date optionDate, double strike, bool extrapolate = false)
    {
        double v = Volatility(optionDate, strike, extrapolate);
        double t = TimeFromReference(optionDate);
        return v * v * t;
    }

    /// <summary>The Black variance for an option time and a strike: the volatility squared times the time.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(double optionTime, double strike, bool extrapolate = false)
    {
        double v = Volatility(optionTime, strike, extrapolate);
        return v * v * optionTime;
    }

    /// <summary>The smile section for an option tenor.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The smile section.</returns>
    public SmileSection SmileSection(Period optionTenor, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return SmileSection(optionDate, extrapolate);
    }

    /// <summary>The smile section for an option date.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The date is outside the structure's domain.</exception>
    public SmileSection SmileSection(Date optionDate, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckRange(optionDate, extrapolate);
        return SmileSectionImpl(optionDate);
    }

    /// <summary>The smile section for an option time.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The time is outside the structure's domain.</exception>
    public SmileSection SmileSection(double optionTime, bool extrapolate = false)
    {
        CheckRange(optionTime, extrapolate);
        return SmileSectionImpl(optionTime);
    }

    /// <summary>The smile section for an option date, without range checks; by default the one for the date's time.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <returns>The smile section.</returns>
    protected virtual SmileSection SmileSectionImpl(Date optionDate) =>
        SmileSectionImpl(TimeFromReference(optionDate));

    /// <summary>The smile section for an option time, without range checks.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <returns>The smile section.</returns>
    protected abstract SmileSection SmileSectionImpl(double optionTime);

    /// <summary>The volatility for an option date, without range checks; by default through the date's time.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected virtual double VolatilityImpl(Date optionDate, double strike) =>
        VolatilityImpl(TimeFromReference(optionDate), strike);

    /// <summary>The volatility for an option time, without range checks.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double VolatilityImpl(double optionTime, double strike);
}
