// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructure.hpp ql/termstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructors taking a day counter only, for structures that manage their own reference date, which no pilot curve uses

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL;

/// <summary>Basic term-structure functionality: a reference date, a day counter and range checks.</summary>
/// <remarks>
/// Every term structure has a fixed reference date. QuantLib's structures whose reference date moves with the
/// evaluation date are built by factories that take the evaluation context and record the settlement days.
/// </remarks>
public abstract class TermStructure : Extrapolator
{
    private readonly DayCounter dayCounter;

    /// <summary>Initializes a new instance of the <see cref="TermStructure"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar used for reference and option date calculation, if any.</param>
    /// <param name="dc">The day counter used for date/time conversion.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    protected TermStructure(Date referenceDate, Calendar? calendar, DayCounter dc, int? settlementDays = null)
    {
        ArgumentNullException.ThrowIfNull(dc);
        if (settlementDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(settlementDays));
        }

        if (referenceDate.SerialNumber == 0)
        {
            throw new ArgumentException("null reference date", nameof(referenceDate));
        }

        ReferenceDate = referenceDate;
        Calendar = calendar;
        dayCounter = dc;
        SettlementDays = settlementDays;
    }

    /// <summary>Gets the date at which discount = 1.0 and/or variance = 0.0.</summary>
    public Date ReferenceDate { get; }

    /// <summary>Gets the day counter used for date/time conversion.</summary>
    public virtual DayCounter DayCounter => dayCounter;

    /// <summary>Gets the calendar used for reference and/or option date calculation, if any.</summary>
    public virtual Calendar? Calendar { get; }

    /// <summary>
    /// Gets the settlement days used for the reference date calculation; <see langword="null"/> for a structure built
    /// from its reference date, where QuantLib throws "settlement days not provided".
    /// </summary>
    public virtual int? SettlementDays { get; }

    /// <summary>Gets the latest date for which the curve can return values.</summary>
    public abstract Date MaxDate { get; }

    /// <summary>Gets the latest time for which the curve can return values.</summary>
    public virtual double MaxTime => TimeFromReference(MaxDate);

    /// <summary>Date/time conversion: the year fraction from the reference date to a date.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    public double TimeFromReference(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return DayCounter.YearFraction(ReferenceDate, date);
    }

    // Whether this structure measures time as another does: the same day counter from the same reference date.
    internal bool SharesTimes(TermStructure other) => DayCounter == other.DayCounter && ReferenceDate == other.ReferenceDate;

    // The time on this structure's own clock of the instant a time on another structure's clock denotes, for the
    // engines and processes whose times are one structure's and that read others at them (the S-eq plan's rulings of
    // 2026-10-20 and 2026-10-21, under R8): the identity where the two share their times; otherwise the clock's time
    // is taken to its date, the last whose year fraction is at most the time, and the fraction of the next day past
    // it, and read as this structure's year fraction, linear within the day. Between Actual/N day counters it is
    // exact, a reference date's offset a constant shift. An instant before the structure's reference date has a
    // negative own time, which AccrualTime takes to 0.
    internal double OwnTime(TermStructure clock, double t)
    {
        if (SharesTimes(clock))
        {
            return t;
        }

        if (!double.IsFinite(t))
        {
            return double.NaN;
        }

        DayCounter clockDc = clock.DayCounter;
        Date clockReference = clock.ReferenceDate;
        double year = clockDc.YearFraction(clockReference, clockReference + 365);
        int last = Date.MaxDate - clockReference;
        int n = Math.Min((int)Math.Floor(t / year * 365.0), last);
        while (clockDc.YearFraction(clockReference, clockReference + n) > t)
        {
            --n;
        }

        while (n < last && clockDc.YearFraction(clockReference, clockReference + (n + 1)) <= t)
        {
            ++n;
        }

        Date day = clockReference + n;
        double from = clockDc.YearFraction(clockReference, day);
        double own = DayCounter.YearFraction(ReferenceDate, day);
        if (n == last)
        {
            // the last representable day has no next: its fraction on the day before's interval
            double fromBefore = clockDc.YearFraction(clockReference, day - 1);
            double ownBefore = DayCounter.YearFraction(ReferenceDate, day - 1);
            return own + ((t - from) / (from - fromBefore) * (own - ownBefore));
        }

        double fraction = (t - from) / (clockDc.YearFraction(clockReference, day + 1) - from);
        return own + (fraction * (DayCounter.YearFraction(ReferenceDate, day + 1) - own));
    }

    // The own time up to which this structure has accrued at the instant a time on another structure's clock denotes:
    // each structure counts from its own reference date, as QuantLib's analytic engines do (the user's rulings of
    // 2026-10-21 on the S-eq plan, C4-O1 and C4R2-O2). An instant before the reference date is its time 0, so a
    // structure dated after the clock's accrues nothing before its date.
    internal double AccrualTime(TermStructure clock, double t)
    {
        double own = OwnTime(clock, t);
        return own < 0.0 ? 0.0 : own;
    }

    // This structure's time to a date, none before its reference date: TimeFromReference on and after that date (the
    // user's rulings of 2026-10-21 on the S-eq plan, C4R2-O2 (i): nothing accrues before a structure's reference date).
    internal double AccrualTimeTo(Date date) => Math.Max(TimeFromReference(date), 0.0);

    // The year fraction on another day counter from this structure's reference date to a date, none before that date:
    // this structure's time to the date measured in another's units.
    internal double AccrualTimeTo(Date date, DayCounter dayCounter) =>
        Math.Max(dayCounter.YearFraction(ReferenceDate, date), 0.0);

    // This structure's accrual time elapsed per unit of another structure's between two times on that one's clock,
    // the rate at which a rate or variance read at its own times accrues per unit of the clock's (AccrualTime); a
    // zero-length interval widened as QuantLib widens a forward rate's.
    internal double AccruedPerClockTime(TermStructure clock, double t1, double t2)
    {
        if (t2 == t1)
        {
            const double Dt = 0.0001;
            double shifted = t1 - (Dt / 2.0);
            t1 = shifted < 0.0 ? 0.0 : shifted;
            t2 = t1 + Dt;
        }

        return (AccrualTime(clock, t2) - AccrualTime(clock, t1)) / (t2 - t1);
    }

    /// <summary>Checks that a date is within the curve's range, or that extrapolation is allowed.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The date is out of range.</exception>
    protected void CheckRange(Date d, bool extrapolate)
    {
        if (!(d >= ReferenceDate))
        {
            throw new NanoQLException($"date ({d}) before reference date ({ReferenceDate})");
        }

        if (!(extrapolate || AllowsExtrapolation || d <= MaxDate))
        {
            throw new NanoQLException($"date ({d}) is past max curve date ({MaxDate})");
        }
    }

    /// <summary>Checks that a time is within the curve's range, or that extrapolation is allowed.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The time is out of range.</exception>
    protected void CheckRange(double t, bool extrapolate)
    {
        if (!(t >= 0.0))
        {
            throw new NanoQLException($"negative time ({Show(t)}) given");
        }

        if (!(extrapolate || AllowsExtrapolation || t <= MaxTime || ComparisonFunctions.CloseEnough(t, MaxTime)))
        {
            throw new NanoQLException($"time ({Show(t)}) is past max curve time ({Show(MaxTime)})");
        }
    }

    private static string Show(double t) => t.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
