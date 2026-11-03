// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/inflation/cpivolatilitystructure.hpp ql/termstructures/volatility/inflation/cpivolatilitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base class of zero-inflation (CPI, RPI, HICP) volatility structures, which observe an index with a lag.
/// </summary>
/// <remarks>
/// <para>
/// CPI volatility is always with respect to a base date. An observation lag of <see langword="null"/>, or QuantLib's
/// sentinel <c>Period(-1, Days)</c>, means the structure's own <see cref="ObservationLag"/>.
/// </para>
/// <para>
/// An extension point. QuantLib's only constructor computes the reference date from settlement days and the evaluation
/// date; here the derived class's <c>Create</c> computes it and passes it in, so every built structure has a fixed
/// reference date.
/// </para>
/// </remarks>
public abstract class CPIVolatilitySurface : VolatilityTermStructure
{
    private static readonly Period NoLag = new(-1, TimeUnit.Days);

    private readonly Period observationLag;
    private readonly Frequency frequency;
    private readonly bool indexIsInterpolated;

    // QuantLib's protected baseLevel_: written by a derived class while it is built, never after.
    private double? baseLevel;

    /// <summary>Initializes a new instance of the <see cref="CPIVolatilitySurface"/> class.</summary>
    /// <param name="referenceDate">The reference date, the given business days after the evaluation date.</param>
    /// <param name="settlementDays">The business days the reference date was computed with.</param>
    /// <param name="calendar">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="observationLag">The lag the structure observes the index with.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="indexIsInterpolated">Whether the index is observed with linear interpolation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="calendar"/> or <paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> or <paramref name="frequency"/> isn't a member of its type.</exception>
    protected CPIVolatilitySurface(
        Date referenceDate,
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period observationLag,
        Frequency frequency,
        bool indexIsInterpolated)
        : base(referenceDate, calendar ?? throw new ArgumentNullException(nameof(calendar)), bdc, dc, settlementDays)
    {
        EnumArguments.Check(frequency, nameof(frequency));
        this.observationLag = observationLag;
        this.frequency = frequency;
        this.indexIsInterpolated = indexIsInterpolated;
    }

    /// <summary>
    /// Gets the lag the structure observes the index with, which usually differs from the index's availability lag.
    /// </summary>
    public virtual Period ObservationLag => observationLag;

    /// <summary>Gets the index's frequency.</summary>
    public virtual Frequency Frequency => frequency;

    /// <summary>Gets a value indicating whether the index is observed with linear interpolation.</summary>
    public virtual bool IndexIsInterpolated => indexIsInterpolated;

    /// <summary>
    /// The base date: the reference date less the observation lag, at the start of its inflation period unless the
    /// index is interpolated. It is in the past because of the lag.
    /// </summary>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index isn't interpolated and its frequency isn't one
    /// <see cref="InflationTermStructureFunctions.InflationPeriod"/> handles.</exception>
    public virtual Date BaseDate()
    {
        // Depends on interpolation, or not, of observed index
        // and observation lag with which it was built.
        // We want this to work even if the index does not
        // have a term structure.
        if (IndexIsInterpolated)
        {
            return ReferenceDate - ObservationLag;
        }

        return InflationTermStructureFunctions.InflationPeriod(ReferenceDate - ObservationLag, Frequency).Start;
    }

    /// <summary>The time from the base date to a date observed with a lag.</summary>
    /// <param name="date">The date.</param>
    /// <param name="obsLag">The observation lag, or <see langword="null"/> for the structure's.</param>
    /// <returns>The time, by the structure's day counter.</returns>
    public virtual double TimeFromBase(Date date, Period? obsLag = null)
    {
        Date.ThrowIfDefault(date, nameof(date));
        Period useLag = UseLag(obsLag);
        Date useDate = IndexIsInterpolated
            ? date - useLag
            : InflationTermStructureFunctions.InflationPeriod(date - useLag, Frequency).Start;

        // This assumes that the inflation term structure starts
        // as late as possible given the inflation index definition,
        // which is the usual case.
        return DayCounter.YearFraction(BaseDate(), useDate);
    }

    /// <summary>The volatility at the base date, which acts as the zero-time value for bootstrapping.</summary>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The structure has no base level.</exception>
    public virtual double BaseLevel() =>
        baseLevel ?? throw new NanoQLException("Base volatility, for baseDate(), not set.");

    /// <summary>The volatility for a maturity date and a strike, observed with a lag.</summary>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="obsLag">The observation lag, or <see langword="null"/> for the structure's.</param>
    /// <param name="extrapolate">Whether to extrapolate.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The observed date or the strike is out of range.</exception>
    public double Volatility(Date maturityDate, double strike, Period? obsLag = null, bool extrapolate = false)
    {
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        Period useLag = UseLag(obsLag);
        Date d = IndexIsInterpolated
            ? maturityDate - useLag
            : InflationTermStructureFunctions.InflationPeriod(maturityDate - useLag, Frequency).Start;
        CheckRange(d, strike, extrapolate);
        double t = TimeFromReference(d);
        return VolatilityImpl(t, strike);
    }

    /// <summary>The volatility for an option tenor and a strike, observed with a lag.</summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="obsLag">The observation lag, or <see langword="null"/> for the structure's.</param>
    /// <param name="extrapolate">Whether to extrapolate.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The observed date or the strike is out of range.</exception>
    public double Volatility(Period optionTenor, double strike, Period? obsLag = null, bool extrapolate = false)
    {
        Date maturityDate = OptionDateFromTenor(optionTenor);
        return Volatility(maturityDate, strike, obsLag, extrapolate);
    }

    /// <summary>The volatility for a time and a strike, with no lag or interpolation applied to the time.</summary>
    /// <param name="time">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    public double Volatility(double time, double strike) => VolatilityImpl(time, strike);

    /// <summary>
    /// The total integrated variance for an exercise date and a strike: the squared volatility times the time from the
    /// base date. "Total" because the structure doesn't know whether it is Black, Bachelier or displaced-diffusion
    /// variance.
    /// </summary>
    /// <param name="exerciseDate">The exercise date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="obsLag">The observation lag, or <see langword="null"/> for the structure's.</param>
    /// <param name="extrapolate">Whether to extrapolate.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The observed date or the strike is out of range.</exception>
    public virtual double TotalVariance(Date exerciseDate, double strike, Period? obsLag = null, bool extrapolate = false)
    {
        double vol = Volatility(exerciseDate, strike, obsLag, extrapolate);
        double t = TimeFromBase(exerciseDate, obsLag);
        return vol * vol * t;
    }

    /// <summary>The total integrated variance for an option tenor and a strike.</summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="obsLag">The observation lag, or <see langword="null"/> for the structure's.</param>
    /// <param name="extrapolate">Whether to extrapolate.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The observed date or the strike is out of range.</exception>
    public virtual double TotalVariance(Period optionTenor, double strike, Period? obsLag = null, bool extrapolate = false)
    {
        Date maturityDate = OptionDateFromTenor(optionTenor);
        return TotalVariance(maturityDate, strike, obsLag, extrapolate);
    }

    /// <summary>Sets the base level, QuantLib's protected <c>baseLevel_</c>, while a derived structure is built.</summary>
    /// <param name="v">The volatility at the base date.</param>
    protected void SetBaseLevel(double v) => baseLevel = v;

    /// <summary>Checks that a date and a strike are within the structure's range, or that extrapolation is allowed.</summary>
    /// <param name="d">The date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The date is before the base date, or past the maximum date, or the strike is
    /// outside the domain, without extrapolation.</exception>
    protected virtual void CheckRange(Date d, double strike, bool extrapolate)
    {
        if (!(d >= BaseDate()))
        {
            throw new NanoQLException($"date ({d}) is before base date");
        }

        if (!(extrapolate || AllowsExtrapolation || d <= MaxDate))
        {
            throw new NanoQLException($"date ({d}) is past max curve date ({MaxDate})");
        }

        if (!(extrapolate || AllowsExtrapolation || (strike >= MinStrike() && strike <= MaxStrike())))
        {
            throw new NanoQLException(
                $"strike ({Show(strike)}) is outside the curve domain [{Show(MinStrike())},{Show(MaxStrike())}]] at "
                + $"date = {d}");
        }
    }

    /// <summary>Checks that a time and a strike are within the structure's range, or that extrapolation is allowed.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The time is before the base date's, or past the maximum time, or the strike
    /// is outside the domain, without extrapolation.</exception>
    protected virtual void CheckRange(double t, double strike, bool extrapolate)
    {
        if (!(t >= TimeFromReference(BaseDate())))
        {
            throw new NanoQLException($"time ({Show(t)}) is before base date");
        }

        if (!(extrapolate || AllowsExtrapolation || t <= MaxTime))
        {
            throw new NanoQLException($"time ({Show(t)}) is past max curve time ({Show(MaxTime)})");
        }

        if (!(extrapolate || AllowsExtrapolation || (strike >= MinStrike() && strike <= MaxStrike())))
        {
            throw new NanoQLException(
                $"strike ({Show(strike)}) is outside the curve domain [{Show(MinStrike())},{Show(MaxStrike())}] at "
                + $"time = {Show(t)}");
        }
    }

    /// <summary>The volatility at a time and a strike, which a derived structure computes.</summary>
    /// <param name="length">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double VolatilityImpl(double length, double strike);

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // QuantLib's useLag: the structure's lag for the sentinel Period(-1, Days), or for none given.
    private Period UseLag(Period? obsLag) => obsLag is { } lag && !(lag == NoLag) ? lag : ObservationLag;
}
