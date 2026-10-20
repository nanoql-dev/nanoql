// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/swaptionvolstructure.hpp ql/termstructures/volatility/swaption/swaptionvolstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (bdc, dc) (not ported: its only QuantLib user, the spreaded swaption volatility, passes its base's reference date to the reference-date constructor)

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>The base class of swaption volatility structures: volatilities by option date and swap tenor.</summary>
/// <remarks>
/// <para>
/// An option is given by its tenor from the reference date, its date or its time, and the swap by its tenor or its
/// length in years, so each query has QuantLib's six overloads. A tenor's option date is the reference date advanced on
/// the structure's calendar; a swap tenor's length is <see cref="SwapLength(Period)"/>.
/// </para>
/// <para>
/// The smile sections pass the <c>extrapolate</c> flag as QuantLib does: those by an option time and a swap tenor, and
/// by an option tenor or date and a swap length, check the query with the flag and then call the overload by option
/// time or date and swap length without it, which checks the query again without extrapolating.
/// </para>
/// </remarks>
public abstract class SwaptionVolatilityStructure : VolatilityTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="SwaptionVolatilityStructure"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty
    /// calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    protected SwaptionVolatilityStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dc,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dc, settlementDays)
    {
    }

    /// <summary>Gets the largest swap tenor for which the structure returns volatilities.</summary>
    public abstract Period MaxSwapTenor { get; }

    /// <summary>Gets the type of the volatilities the structure returns; by default shifted lognormal.</summary>
    public virtual VolatilityType VolatilityType => VolatilityType.ShiftedLognormal;

    /// <summary>The volatility for an option tenor, a swap tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    public double Volatility(Period optionTenor, Period swapTenor, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return Volatility(optionDate, swapTenor, strike, extrapolate);
    }

    /// <summary>The volatility for an option date, a swap tenor and a strike.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The swap tenor, the date or the strike is outside the structure's
    /// domain.</exception>
    public double Volatility(Date optionDate, Period swapTenor, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckSwapTenor(swapTenor, extrapolate);
        CheckRange(optionDate, extrapolate);
        CheckStrike(strike, extrapolate);
        return VolatilityImpl(optionDate, swapTenor, strike);
    }

    /// <summary>The volatility for an option time, a swap tenor and a strike.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The swap tenor, the time or the strike is outside the structure's
    /// domain.</exception>
    public double Volatility(double optionTime, Period swapTenor, double strike, bool extrapolate = false)
    {
        CheckSwapTenor(swapTenor, extrapolate);
        CheckRange(optionTime, extrapolate);
        CheckStrike(strike, extrapolate);
        double length = SwapLength(swapTenor);
        return VolatilityImpl(optionTime, length, strike);
    }

    /// <summary>The volatility for an option tenor, a swap length and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    public double Volatility(Period optionTenor, double swapLength, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return Volatility(optionDate, swapLength, strike, extrapolate);
    }

    /// <summary>The volatility for an option date, a swap length and a strike.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The swap length, the date or the strike is outside the structure's
    /// domain.</exception>
    public double Volatility(Date optionDate, double swapLength, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckSwapTenor(swapLength, extrapolate);
        CheckRange(optionDate, extrapolate);
        CheckStrike(strike, extrapolate);
        double optionTime = TimeFromReference(optionDate);
        return VolatilityImpl(optionTime, swapLength, strike);
    }

    /// <summary>The volatility for an option time, a swap length and a strike.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The swap length, the time or the strike is outside the structure's
    /// domain.</exception>
    public double Volatility(double optionTime, double swapLength, double strike, bool extrapolate = false)
    {
        CheckSwapTenor(swapLength, extrapolate);
        CheckRange(optionTime, extrapolate);
        CheckStrike(strike, extrapolate);
        return VolatilityImpl(optionTime, swapLength, strike);
    }

    /// <summary>The Black variance for an option tenor, a swap tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(Period optionTenor, Period swapTenor, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return BlackVariance(optionDate, swapTenor, strike, extrapolate);
    }

    /// <summary>The Black variance for an option date, a swap tenor and a strike: the volatility squared times the time.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(Date optionDate, Period swapTenor, double strike, bool extrapolate = false)
    {
        double v = Volatility(optionDate, swapTenor, strike, extrapolate);
        double optionTime = TimeFromReference(optionDate);
        return v * v * optionTime;
    }

    /// <summary>The Black variance for an option time, a swap tenor and a strike: the volatility squared times the time.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(double optionTime, Period swapTenor, double strike, bool extrapolate = false)
    {
        double v = Volatility(optionTime, swapTenor, strike, extrapolate);
        return v * v * optionTime;
    }

    /// <summary>The Black variance for an option tenor, a swap length and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(Period optionTenor, double swapLength, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return BlackVariance(optionDate, swapLength, strike, extrapolate);
    }

    /// <summary>The Black variance for an option date, a swap length and a strike: the volatility squared times the time.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(Date optionDate, double swapLength, double strike, bool extrapolate = false)
    {
        double v = Volatility(optionDate, swapLength, strike, extrapolate);
        double optionTime = TimeFromReference(optionDate);
        return v * v * optionTime;
    }

    /// <summary>The Black variance for an option time, a swap length and a strike: the volatility squared times the time.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    public double BlackVariance(double optionTime, double swapLength, double strike, bool extrapolate = false)
    {
        double v = Volatility(optionTime, swapLength, strike, extrapolate);
        return v * v * optionTime;
    }

    /// <summary>The shift for an option tenor and a swap tenor.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The shift.</returns>
    public double Shift(Period optionTenor, Period swapTenor, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return Shift(optionDate, swapTenor, extrapolate);
    }

    /// <summary>The shift for an option date and a swap tenor.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The shift.</returns>
    /// <exception cref="NanoQLException">The swap tenor or the date is outside the structure's domain.</exception>
    public double Shift(Date optionDate, Period swapTenor, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckSwapTenor(swapTenor, extrapolate);
        CheckRange(optionDate, extrapolate);
        return ShiftImpl(optionDate, swapTenor);
    }

    /// <summary>The shift for an option time and a swap tenor.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The shift.</returns>
    /// <exception cref="NanoQLException">The swap tenor or the time is outside the structure's domain.</exception>
    public double Shift(double optionTime, Period swapTenor, bool extrapolate = false)
    {
        CheckSwapTenor(swapTenor, extrapolate);
        CheckRange(optionTime, extrapolate);
        double length = SwapLength(swapTenor);
        return ShiftImpl(optionTime, length);
    }

    /// <summary>The shift for an option tenor and a swap length.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The shift.</returns>
    public double Shift(Period optionTenor, double swapLength, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return Shift(optionDate, swapLength, extrapolate);
    }

    /// <summary>The shift for an option date and a swap length.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The shift.</returns>
    /// <exception cref="NanoQLException">The swap length or the date is outside the structure's domain.</exception>
    public double Shift(Date optionDate, double swapLength, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckSwapTenor(swapLength, extrapolate);
        CheckRange(optionDate, extrapolate);
        double optionTime = TimeFromReference(optionDate);
        return ShiftImpl(optionTime, swapLength);
    }

    /// <summary>The shift for an option time and a swap length.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The shift.</returns>
    /// <exception cref="NanoQLException">The swap length or the time is outside the structure's domain.</exception>
    public double Shift(double optionTime, double swapLength, bool extrapolate = false)
    {
        CheckSwapTenor(swapLength, extrapolate);
        CheckRange(optionTime, extrapolate);
        return ShiftImpl(optionTime, swapLength);
    }

    /// <summary>The smile section for an option tenor and a swap tenor.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The smile section.</returns>
    public SmileSection SmileSection(Period optionTenor, Period swapTenor, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return SmileSection(optionDate, swapTenor, extrapolate);
    }

    /// <summary>The smile section for an option date and a swap tenor.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The swap tenor or the date is outside the structure's domain.</exception>
    public SmileSection SmileSection(Date optionDate, Period swapTenor, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckSwapTenor(swapTenor, extrapolate);
        CheckRange(optionDate, extrapolate);
        return SmileSectionImpl(optionDate, swapTenor);
    }

    /// <summary>
    /// The smile section for an option time and a swap tenor: the query is checked with the flag, and the section is
    /// the one for the time and the tenor's swap length, checked again without it.
    /// </summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range, in the first check only.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The swap tenor or the time is outside the structure's domain.</exception>
    public SmileSection SmileSection(double optionTime, Period swapTenor, bool extrapolate = false)
    {
        CheckSwapTenor(swapTenor, extrapolate);
        CheckRange(optionTime, extrapolate);

        // QuantLib calls the two-argument overload, whose extrapolate is false.
        return SmileSection(optionTime, SwapLength(swapTenor));
    }

    /// <summary>
    /// The smile section for an option tenor and a swap length: the query is checked with the flag, and the section is
    /// the one for the tenor's option date, checked again without it.
    /// </summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range, in the first check only.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The swap length or the date is outside the structure's domain.</exception>
    public SmileSection SmileSection(Period optionTenor, double swapLength, bool extrapolate = false)
    {
        CheckSwapTenor(swapLength, extrapolate);
        Date optionDate = OptionDateFromTenor(optionTenor);
        CheckRange(optionDate, extrapolate);

        // QuantLib calls the two-argument overload, whose extrapolate is false.
        return SmileSection(optionDate, swapLength);
    }

    /// <summary>
    /// The smile section for an option date and a swap length: the query is checked with the flag, and the section is
    /// the one for the date's time, checked again without it.
    /// </summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range, in the first check only.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The swap length or the date is outside the structure's domain.</exception>
    public SmileSection SmileSection(Date optionDate, double swapLength, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckSwapTenor(swapLength, extrapolate);
        CheckRange(optionDate, extrapolate);

        // QuantLib calls the two-argument overload, whose extrapolate is false.
        return SmileSection(TimeFromReference(optionDate), swapLength);
    }

    /// <summary>The smile section for an option time and a swap length.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The swap length or the time is outside the structure's domain.</exception>
    public SmileSection SmileSection(double optionTime, double swapLength, bool extrapolate = false)
    {
        CheckSwapTenor(swapLength, extrapolate);
        CheckRange(optionTime, extrapolate);
        return SmileSectionImpl(optionTime, swapLength);
    }

    /// <summary>The largest swap length for which the structure returns volatilities: the length of <see cref="MaxSwapTenor"/>.</summary>
    /// <returns>The length, in years.</returns>
    /// <exception cref="ArgumentException">The maximum swap tenor has no swap length (<see cref="SwapLength(Period)"/>).</exception>
    public double MaxSwapLength() => SwapLength(MaxSwapTenor);

    /// <summary>The swap length of a swap tenor: its length in years, a tenor in months counting twelfths.</summary>
    /// <param name="swapTenor">The swap tenor, in months or years.</param>
    /// <returns>The length, in years.</returns>
    /// <exception cref="ArgumentException">The tenor isn't positive, or isn't in months or years.</exception>
    public double SwapLength(Period swapTenor)
    {
        if (!(swapTenor.Length > 0))
        {
            throw new ArgumentException($"non-positive swap tenor ({swapTenor}) given", nameof(swapTenor));
        }

        switch (swapTenor.Units)
        {
            case TimeUnit.Months:
                return swapTenor.Length / 12.0;
            case TimeUnit.Years:
                return swapTenor.Length;
            default:
                throw new ArgumentException($"invalid Time Unit ({swapTenor.Units}) for swap length", nameof(swapTenor));
        }
    }

    /// <summary>The swap length between two dates: the days between them in months of 365.25/12 days, rounded to whole
    /// months, in years.</summary>
    /// <param name="start">The swap's start date.</param>
    /// <param name="end">The swap's end date.</param>
    /// <returns>The length, in years.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the end isn't after the start.</exception>
    public double SwapLength(Date start, Date end)
    {
        Date.ThrowIfDefault(start, nameof(start));
        Date.ThrowIfDefault(end, nameof(end));
        if (!(end > start))
        {
            throw new ArgumentException($"swap end date ({end}) must be greater than start ({start})", nameof(end));
        }

        double result = (end - start) / 365.25 * 12.0; // month unit
        result = new ClosestRounding(0).Evaluate(result);
        result /= 12.0; // year unit
        return result;
    }

    /// <summary>The smile section for an option date and a swap tenor, without range checks; by default the one for the
    /// date's time and the tenor's swap length.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <returns>The smile section.</returns>
    protected virtual SmileSection SmileSectionImpl(Date optionDate, Period swapTenor) =>
        SmileSectionImpl(TimeFromReference(optionDate), SwapLength(swapTenor));

    /// <summary>The smile section for an option time and a swap length, without range checks.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The smile section.</returns>
    protected abstract SmileSection SmileSectionImpl(double optionTime, double swapLength);

    /// <summary>The volatility for an option date, a swap tenor and a strike, without range checks; by default the one
    /// for the date's time and the tenor's swap length.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected virtual double VolatilityImpl(Date optionDate, Period swapTenor, double strike) =>
        VolatilityImpl(TimeFromReference(optionDate), SwapLength(swapTenor), strike);

    /// <summary>The volatility for an option time, a swap length and a strike, without range checks.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double VolatilityImpl(double optionTime, double swapLength, double strike);

    /// <summary>The shift for an option date and a swap tenor, without range checks; by default the one for the date's
    /// time and the tenor's swap length.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <returns>The shift.</returns>
    protected virtual double ShiftImpl(Date optionDate, Period swapTenor) =>
        ShiftImpl(TimeFromReference(optionDate), SwapLength(swapTenor));

    /// <summary>The shift for an option time and a swap length, without range checks; by default 0, for a shifted
    /// lognormal structure only.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The shift.</returns>
    /// <exception cref="NanoQLException">The structure's volatilities aren't shifted lognormal.</exception>
    protected virtual double ShiftImpl(double optionTime, double swapLength)
    {
        if (!(VolatilityType == VolatilityType.ShiftedLognormal))
        {
            throw new NanoQLException("shift parameter only makes sense for lognormal volatilities");
        }

        return 0.0;
    }

    /// <summary>Checks that a swap tenor is positive and in the structure's range, or that extrapolation is allowed.</summary>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The tenor isn't positive or is past the maximum swap tenor.</exception>
    protected void CheckSwapTenor(Period swapTenor, bool extrapolate)
    {
        if (!(swapTenor.Length > 0))
        {
            throw new NanoQLException($"non-positive swap tenor ({swapTenor}) given");
        }

        if (!(extrapolate || AllowsExtrapolation || swapTenor <= MaxSwapTenor))
        {
            throw new NanoQLException($"swap tenor ({swapTenor}) is past max tenor ({MaxSwapTenor})");
        }
    }

    /// <summary>Checks that a swap length is positive and in the structure's range, or that extrapolation is allowed.</summary>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The length isn't positive or is past the maximum swap length.</exception>
    protected void CheckSwapTenor(double swapLength, bool extrapolate)
    {
        if (!(swapLength > 0.0))
        {
            throw new NanoQLException($"non-positive swap length ({Show(swapLength)}) given");
        }

        if (!(extrapolate || AllowsExtrapolation || swapLength <= MaxSwapLength()))
        {
            throw new NanoQLException($"swap tenor ({Show(swapLength)}) is past max tenor ({Show(MaxSwapLength())})");
        }
    }

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
