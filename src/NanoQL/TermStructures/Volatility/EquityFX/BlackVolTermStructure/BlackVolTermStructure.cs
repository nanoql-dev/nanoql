// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvoltermstructure.hpp ql/termstructures/volatility/equityfx/blackvoltermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (bdc, dc) (not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead)

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The Black-volatility term structure: the base of the structures that give Black volatilities and variances by
/// maturity and strike. Volatilities are on an annual basis.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: the Black volatility and variance structures derive from it, through
/// <see cref="BlackVolatilityTermStructure"/> and <see cref="BlackVarianceTermStructure"/>. A structure is immutable,
/// its reference date fixed at construction.
/// </remarks>
public abstract class BlackVolTermStructure : VolatilityTermStructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlackVolTermStructure"/> class at a reference date; with
    /// settlement days, for a structure a factory dates by the context.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="bdc">The business-day convention of the option dates; QuantLib's default is
    /// <c>Following</c>.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was moved by, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    protected BlackVolTermStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The spot volatility at a maturity date and strike.</summary>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date or strike is out of range.</exception>
    public double BlackVol(Date maturity, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(maturity, nameof(maturity));
        CheckRange(maturity, extrapolate);
        CheckStrike(strike, extrapolate);
        double t = TimeFromReference(maturity);
        return BlackVolOn(maturity, t, strike);
    }

    /// <summary>The spot volatility at a maturity time and strike.</summary>
    /// <param name="maturity">The maturity time.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The time or strike is out of range.</exception>
    public double BlackVol(double maturity, double strike, bool extrapolate = false)
    {
        CheckRange(maturity, extrapolate);
        CheckStrike(strike, extrapolate);
        return BlackVolImpl(maturity, strike);
    }

    /// <summary>The spot variance at a maturity date and strike.</summary>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date or strike is out of range.</exception>
    public double BlackVariance(Date maturity, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(maturity, nameof(maturity));
        CheckRange(maturity, extrapolate);
        CheckStrike(strike, extrapolate);
        double t = TimeFromReference(maturity);
        return BlackVarianceOn(maturity, t, strike);
    }

    /// <summary>The spot variance at a maturity time and strike.</summary>
    /// <param name="maturity">The maturity time.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The time or strike is out of range.</exception>
    public double BlackVariance(double maturity, double strike, bool extrapolate = false)
    {
        CheckRange(maturity, extrapolate);
        CheckStrike(strike, extrapolate);
        return BlackVarianceImpl(maturity, strike);
    }

    /// <summary>The forward (at-the-money) volatility between two dates, at a strike.</summary>
    /// <param name="date1">The first date.</param>
    /// <param name="date2">The second date, not before the first.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward volatility.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the first is later than the
    /// second.</exception>
    /// <exception cref="NanoQLException">The second date or the strike is out of range, or the variance
    /// decreases.</exception>
    public double BlackForwardVol(Date date1, Date date2, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(date1, nameof(date1));
        Date.ThrowIfDefault(date2, nameof(date2));

        // (redundant) date-based range check
        if (!(date1 <= date2))
        {
            throw new ArgumentException($"{date1} later than {date2}", nameof(date1));
        }

        CheckRange(date2, extrapolate);

        // using the time implementation
        double time1 = TimeFromReference(date1);
        double time2 = TimeFromReference(date2);
        if (time2 == time1 || !(time1 <= time2))
        {
            return BlackForwardVol(time1, time2, strike, extrapolate);
        }

        // the time implementation's arithmetic, its variances read at the dates
        CheckRange(time2, extrapolate);
        CheckStrike(strike, extrapolate);
        double var1 = BlackVarianceOn(date1, time1, strike);
        double var2 = BlackVarianceOn(date2, time2, strike);
        if (!(var2 >= var1))
        {
            throw new NanoQLException("variances must be non-decreasing");
        }

        return Math.Sqrt((var2 - var1) / (time2 - time1));
    }

    /// <summary>The forward (at-the-money) volatility between two times, at a strike.</summary>
    /// <param name="time1">The first time.</param>
    /// <param name="time2">The second time, not before the first.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward volatility.</returns>
    /// <exception cref="ArgumentException">The first time is later than the second.</exception>
    /// <exception cref="NanoQLException">The second time or the strike is out of range, or the variance
    /// decreases.</exception>
    public double BlackForwardVol(double time1, double time2, double strike, bool extrapolate = false)
    {
        if (!(time1 <= time2))
        {
            throw new ArgumentException($"{Show(time1)} later than {Show(time2)}", nameof(time1));
        }

        CheckRange(time2, extrapolate);
        CheckStrike(strike, extrapolate);
        if (time2 == time1)
        {
            if (time1 == 0.0)
            {
                double epsilon = 1.0e-5;
                double var = BlackVarianceImpl(epsilon, strike);
                return Math.Sqrt(var / epsilon);
            }
            else
            {
                double epsilon = Min(1.0e-5, time1);
                double var1 = BlackVarianceImpl(time1 - epsilon, strike);
                double var2 = BlackVarianceImpl(time1 + epsilon, strike);
                if (!(var2 >= var1))
                {
                    throw new NanoQLException("variances must be non-decreasing");
                }

                return Math.Sqrt((var2 - var1) / (2 * epsilon));
            }
        }
        else
        {
            double var1 = BlackVarianceImpl(time1, strike);
            double var2 = BlackVarianceImpl(time2, strike);
            if (!(var2 >= var1))
            {
                throw new NanoQLException("variances must be non-decreasing");
            }

            return Math.Sqrt((var2 - var1) / (time2 - time1));
        }
    }

    /// <summary>The forward (at-the-money) variance between two dates, at a strike.</summary>
    /// <param name="date1">The first date.</param>
    /// <param name="date2">The second date, not before the first.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward variance.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the first is later than the
    /// second.</exception>
    /// <exception cref="NanoQLException">The second date or the strike is out of range, or the variance
    /// decreases.</exception>
    public double BlackForwardVariance(Date date1, Date date2, double strike, bool extrapolate = false)
    {
        Date.ThrowIfDefault(date1, nameof(date1));
        Date.ThrowIfDefault(date2, nameof(date2));

        // (redundant) date-based range check
        if (!(date1 <= date2))
        {
            throw new ArgumentException($"{date1} later than {date2}", nameof(date1));
        }

        CheckRange(date2, extrapolate);

        // using the time implementation
        double time1 = TimeFromReference(date1);
        double time2 = TimeFromReference(date2);
        if (!(time1 <= time2))
        {
            return BlackForwardVariance(time1, time2, strike, extrapolate);
        }

        // the time implementation's arithmetic, its variances read at the dates
        CheckRange(time2, extrapolate);
        CheckStrike(strike, extrapolate);
        double v1 = BlackVarianceOn(date1, time1, strike);
        double v2 = BlackVarianceOn(date2, time2, strike);
        if (!(v2 >= v1))
        {
            throw new NanoQLException("variances must be non-decreasing");
        }

        return v2 - v1;
    }

    /// <summary>The forward (at-the-money) variance between two times, at a strike.</summary>
    /// <param name="time1">The first time.</param>
    /// <param name="time2">The second time, not before the first.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward variance.</returns>
    /// <exception cref="ArgumentException">The first time is later than the second.</exception>
    /// <exception cref="NanoQLException">The second time or the strike is out of range, or the variance
    /// decreases.</exception>
    public double BlackForwardVariance(double time1, double time2, double strike, bool extrapolate = false)
    {
        if (!(time1 <= time2))
        {
            throw new ArgumentException($"{Show(time1)} later than {Show(time2)}", nameof(time1));
        }

        CheckRange(time2, extrapolate);
        CheckStrike(strike, extrapolate);
        double v1 = BlackVarianceImpl(time1, strike);
        double v2 = BlackVarianceImpl(time2, strike);
        if (!(v2 >= v1))
        {
            throw new NanoQLException("variances must be non-decreasing");
        }

        return v2 - v1;
    }

    // The variance at a date and its time, which BlackVariance(Date), BlackForwardVariance(Date, Date) and
    // BlackForwardVol(Date, Date) read, the range checked: the variance at the date's time, QuantLib's read. A structure
    // whose times can't carry its dates reads them by date (the implied structure on a day counter whose year fractions
    // don't add, the S-van plan's VR4 (xxii)); every other structure keeps this read and its bits.
    internal virtual double BlackVarianceOn(Date d, double t, double strike) => BlackVarianceImpl(t, strike);

    // The volatility at a date and its time, which BlackVol(Date) reads, the range checked: QuantLib's read at the
    // date's time, as BlackVarianceOn.
    internal virtual double BlackVolOn(Date d, double t, double strike) => BlackVolImpl(t, strike);

    // The variance accrued by a date, none before the reference date (TermStructure.AccrualTimeTo), where QuantLib's
    // read throws: BlackVariance on and after that date.
    internal double AccruedVariance(Date d, double strike) => d < ReferenceDate ? 0.0 : BlackVariance(d, strike);

    // The variance accrued between two dates: QuantLib's BlackForwardVariance from the reference date on, and before it
    // the variance accrued by the second date.
    internal double AccruedForwardVariance(Date d1, Date d2, double strike) =>
        d1 >= ReferenceDate ? BlackForwardVariance(d1, d2, strike) : AccruedVariance(d2, strike);

    // The variance accrued between the instants two times on another structure's clock denote, from the structure's own
    // reference date (TermStructure.AccrualTime): QuantLib's BlackForwardVariance where the two share their times;
    // otherwise between the instants' accrual times.
    internal double BlackForwardVarianceAt(TermStructure clock, double t1, double t2, double strike, bool extrapolate = false) =>
        SharesTimes(clock)
            ? BlackForwardVariance(t1, t2, strike, extrapolate)
            : BlackForwardVariance(AccrualTime(clock, t1), AccrualTime(clock, t2), strike, extrapolate);

    /// <summary>The at-the-money level at a time, or <see langword="null"/> where the structure gives none.</summary>
    /// <param name="t">The time.</param>
    /// <returns><see langword="null"/>, QuantLib's default.</returns>
    public virtual double? AtmLevel(double t) => null;

    /// <summary>The smile section at a maturity date: the structure's volatilities at that maturity.</summary>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>A smile section.</returns>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of range.</exception>
    public SmileSection SmileSection(Date maturity, bool extrapolate = false)
    {
        Date.ThrowIfDefault(maturity, nameof(maturity));
        CheckRange(maturity, extrapolate);
        return SmileSectionImpl(TimeFromReference(maturity));
    }

    /// <summary>The smile section at a maturity time: the structure's volatilities at that maturity.</summary>
    /// <param name="maturity">The maturity time.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>A smile section.</returns>
    /// <exception cref="NanoQLException">The time is out of range.</exception>
    public SmileSection SmileSection(double maturity, bool extrapolate = false)
    {
        CheckRange(maturity, extrapolate);
        return SmileSectionImpl(maturity);
    }

    /// <summary>The variance at a time and strike, the range already checked; it must extrapolate.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The variance.</returns>
    protected abstract double BlackVarianceImpl(double t, double strike);

    /// <summary>The volatility at a time and strike, the range already checked; it must extrapolate.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double BlackVolImpl(double t, double strike);

    /// <summary>
    /// The smile section at a time: by default an adapter over this structure, which reads its volatilities with
    /// extrapolation. A structure with a smile representation of its own overrides it.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>A smile section.</returns>
    /// <remarks>
    /// QuantLib's adapter holds a <c>shared_ptr</c> to the structure through <c>shared_from_this</c>, and refuses a
    /// structure no <c>shared_ptr</c> holds; a .NET object is always held by reference, so the refusal has no
    /// counterpart.
    /// </remarks>
    protected virtual SmileSection SmileSectionImpl(double t) => new BlackVolSmileSectionAdapter(this, t);

    private static double Min(double a, double b) => b < a ? b : a;

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>QuantLib's <c>BlackVolSmileSectionAdapter</c>: a smile section reading a Black structure at one
/// time.</summary>
file sealed class BlackVolSmileSectionAdapter : SmileSection
{
    private readonly BlackVolTermStructure vol;

    public BlackVolSmileSectionAdapter(BlackVolTermStructure vol, double t)
        : base(t, vol.DayCounter)
    {
        this.vol = vol;
    }

    public override double MinStrike() => vol.MinStrike();

    public override double MaxStrike() => vol.MaxStrike();

    public override double? AtmLevel() => vol.AtmLevel(ExerciseTime);

    protected override double VolatilityImpl(double strike) => vol.BlackVol(ExerciseTime, strike, true);
}
