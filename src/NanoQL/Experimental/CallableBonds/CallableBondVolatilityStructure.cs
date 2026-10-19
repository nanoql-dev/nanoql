// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebondvolstructure.hpp ql/experimental/callablebonds/callablebondvolstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (dc, bdc), which no QuantLib 1.44 class uses and which waits for its first user

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The callable-bond volatility structure base class: forward yield volatilities by option time and bond length, or by
/// option date or tenor and bond tenor, and their smile sections.
/// </summary>
/// <remarks>
/// <para>
/// An option is given by its time, its date or its tenor from the reference date, the bond by its length in years or
/// its tenor. A tenor's option date is the reference date advanced on the structure's calendar by its business-day
/// convention; QuantLib's empty calendar, the default, can't advance by days and refuses that with "no calendar
/// implementation provided".
/// </para>
/// <para>
/// A structure whose reference date moves with the evaluation date is built by a derived class's factory taking the
/// context, which gives this base the reference date and the settlement days. An extension point, as QuantLib's
/// abstract class is: the constant volatility derives from it.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class CallableBondVolatilityStructure : TermStructure
{
    private readonly BusinessDayConvention bdc;

    /// <summary>Initializes a new instance of the <see cref="CallableBondVolatilityStructure"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">
    /// The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar.
    /// </param>
    /// <param name="dc">The day counter.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bdc"/> isn't a member of its enum, or <paramref name="settlementDays"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    protected CallableBondVolatilityStructure(
        Date referenceDate,
        Calendar? calendar,
        DayCounter dc,
        BusinessDayConvention bdc = BusinessDayConvention.Following,
        int? settlementDays = null)
        : base(referenceDate, calendar, dc, settlementDays)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        this.bdc = bdc;
    }

    /// <summary>Gets the largest bond tenor for which the structure returns volatilities.</summary>
    public abstract Period MaxBondTenor { get; }

    /// <summary>Gets the business-day convention used for the option dates computed from tenors.</summary>
    public virtual BusinessDayConvention BusinessDayConvention => bdc;

    /// <summary>The volatility for an option time, a bond length and a strike.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="bondLength">The bond length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The time, the length or the strike is outside the structure's domain.</exception>
    public double Volatility(double optionTime, double bondLength, double strike, bool extrapolate = false)
    {
        CheckRange(optionTime, bondLength, strike, extrapolate);
        return VolatilityImpl(optionTime, bondLength, strike);
    }

    /// <summary>The Black variance for an option time, a bond length and a strike: the volatility squared times the time.</summary>
    /// <param name="optionTime">The option time, from the reference date.</param>
    /// <param name="bondLength">The bond length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The time, the length or the strike is outside the structure's domain.</exception>
    public double BlackVariance(double optionTime, double bondLength, double strike, bool extrapolate = false)
    {
        CheckRange(optionTime, bondLength, strike, extrapolate);
        double vol = VolatilityImpl(optionTime, bondLength, strike);
        return vol * vol * optionTime;
    }

    /// <summary>The volatility for an option date, a bond tenor and a strike.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="optionDate"/> is <c>default(Date)</c>, or the bond tenor ends on or before the option date.
    /// </exception>
    /// <exception cref="NanoQLException">The date, the tenor or the strike is outside the structure's domain.</exception>
    public double Volatility(Date optionDate, Period bondTenor, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        CheckRange(optionDate, bondTenor, strike, extrapolate);
        return VolatilityImpl(optionDate, bondTenor, strike);
    }

    /// <summary>
    /// The Black variance for an option date, a bond tenor and a strike: the volatility squared times the option time.
    /// </summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="optionDate"/> is <c>default(Date)</c>, or the bond tenor ends on or before the option date.
    /// </exception>
    /// <exception cref="NanoQLException">The date, the tenor or the strike is outside the structure's domain.</exception>
    public double BlackVariance(Date optionDate, Period bondTenor, double strike, bool extrapolate = false)
    {
        double vol = Volatility(optionDate, bondTenor, strike, extrapolate);
        (double optionTime, double _) = ConvertDates(optionDate, bondTenor);
        return vol * vol * optionTime;
    }

    /// <summary>The smile section for an option date and a bond tenor.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="optionDate"/> is <c>default(Date)</c>, or the bond tenor ends on or before the option date.
    /// </exception>
    public virtual SmileSection SmileSection(Date optionDate, Period bondTenor)
    {
        (double optionTime, double bondLength) = ConvertDates(optionDate, bondTenor);
        return SmileSectionImpl(optionTime, bondLength);
    }

    /// <summary>The volatility for an option tenor, a bond tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException">The bond tenor ends on or before the option date.</exception>
    /// <exception cref="NanoQLException">
    /// The option date can't be computed on the calendar, or the date, the tenor or the strike is outside the
    /// structure's domain.
    /// </exception>
    public double Volatility(Period optionTenor, Period bondTenor, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return Volatility(optionDate, bondTenor, strike, extrapolate);
    }

    /// <summary>The Black variance for an option tenor, a bond tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to extrapolate outside the structure's range.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentException">The bond tenor ends on or before the option date.</exception>
    /// <exception cref="NanoQLException">
    /// The option date can't be computed on the calendar, or the date, the tenor or the strike is outside the
    /// structure's domain.
    /// </exception>
    public double BlackVariance(Period optionTenor, Period bondTenor, double strike, bool extrapolate = false)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        double vol = Volatility(optionDate, bondTenor, strike, extrapolate);
        (double optionTime, double _) = ConvertDates(optionDate, bondTenor);
        return vol * vol * optionTime;
    }

    /// <summary>The smile section for an option tenor and a bond tenor.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="ArgumentException">The bond tenor ends on or before the option date.</exception>
    /// <exception cref="NanoQLException">The option date can't be computed on the calendar.</exception>
    public SmileSection SmileSection(Period optionTenor, Period bondTenor)
    {
        Date optionDate = OptionDateFromTenor(optionTenor);
        return SmileSection(optionDate, bondTenor);
    }

    /// <summary>The largest bond length for which the structure returns volatilities: by default, the maximum bond tenor's.</summary>
    /// <returns>The length, in years.</returns>
    public virtual double MaxBondLength() => TimeFromReference(ReferenceDate + MaxBondTenor);

    /// <summary>The minimum strike for which the structure returns volatilities.</summary>
    /// <returns>The strike.</returns>
    public abstract double MinStrike();

    /// <summary>The maximum strike for which the structure returns volatilities.</summary>
    /// <returns>The strike.</returns>
    public abstract double MaxStrike();

    /// <summary>The conversion of an option date and a bond tenor into the option time and the bond length.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <returns>The option time, from the reference date, and the bond length, in years, on the day counter.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="optionDate"/> is <c>default(Date)</c>, or the bond tenor ends on or before the option date.
    /// </exception>
    public virtual (double OptionTime, double BondLength) ConvertDates(Date optionDate, Period bondTenor)
    {
        Date.ThrowIfDefault(optionDate, nameof(optionDate));
        Date end = optionDate + bondTenor;
        if (!(end > optionDate))
        {
            throw new ArgumentException($"negative bond tenor ({bondTenor}) given", nameof(bondTenor));
        }

        double optionTime = TimeFromReference(optionDate);
        double timeLength = DayCounter.YearFraction(optionDate, end);
        return (optionTime, timeLength);
    }

    /// <summary>
    /// The option date of a tenor: the reference date advanced by it on the calendar, by the business-day convention.
    /// </summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <returns>The option date.</returns>
    /// <exception cref="NanoQLException">The date can't be computed on QuantLib's empty calendar.</exception>
    public Date OptionDateFromTenor(Period optionTenor) =>
        (Calendar ?? EmptyCalendar.Instance).Advance(ReferenceDate, optionTenor, BusinessDayConvention);

    /// <summary>The smile section for an option time and a bond length.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="bondLength">The bond length, in years.</param>
    /// <returns>The smile section.</returns>
    protected abstract SmileSection SmileSectionImpl(double optionTime, double bondLength);

    /// <summary>The volatility for an option time, a bond length and a strike, past the range checks.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="bondLength">The bond length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double VolatilityImpl(double optionTime, double bondLength, double strike);

    /// <summary>
    /// The volatility for an option date, a bond tenor and a strike, past the range checks: by default, by time and
    /// length.
    /// </summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected virtual double VolatilityImpl(Date optionDate, Period bondTenor, double strike)
    {
        (double optionTime, double bondLength) = ConvertDates(optionDate, bondTenor);
        return VolatilityImpl(optionTime, bondLength, strike);
    }

    /// <summary>Checks an option time, a bond length and a strike against the structure's domain.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="bondLength">The bond length, in years.</param>
    /// <param name="k">The strike.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">A value is outside the domain.</exception>
    protected void CheckRange(double optionTime, double bondLength, double k, bool extrapolate)
    {
        CheckRange(optionTime, extrapolate);
        if (!(bondLength >= 0.0))
        {
            throw new NanoQLException($"negative bondLength ({Show(bondLength)}) given");
        }

        if (!(extrapolate || AllowsExtrapolation || bondLength <= MaxBondLength()))
        {
            throw new NanoQLException(
                $"bondLength ({Show(bondLength)}) is past max curve bondLength ({Show(MaxBondLength())})");
        }

        CheckStrike(k, extrapolate);
    }

    /// <summary>Checks an option date, a bond tenor and a strike against the structure's domain.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="bondTenor">The bond tenor.</param>
    /// <param name="k">The strike.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">A value is outside the domain.</exception>
    protected void CheckRange(Date optionDate, Period bondTenor, double k, bool extrapolate)
    {
        CheckRange(TimeFromReference(optionDate), extrapolate);
        if (!(bondTenor.Length > 0))
        {
            throw new NanoQLException($"negative bond tenor ({bondTenor}) given");
        }

        if (!(extrapolate || AllowsExtrapolation || bondTenor <= MaxBondTenor))
        {
            throw new NanoQLException($"bond tenor ({bondTenor}) is past max tenor ({MaxBondTenor})");
        }

        CheckStrike(k, extrapolate);
    }

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void CheckStrike(double k, bool extrapolate)
    {
        if (!(extrapolate || AllowsExtrapolation || (k >= MinStrike() && k <= MaxStrike())))
        {
            throw new NanoQLException(
                $"strike ({Show(k)}) is outside the curve domain [{Show(MinStrike())},{Show(MaxStrike())}]");
        }
    }
}
