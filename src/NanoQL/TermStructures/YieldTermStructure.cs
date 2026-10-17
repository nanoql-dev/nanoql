// SPDX-FileCopyrightText: Copyright (C) 2004, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yieldtermstructure.hpp ql/termstructures/yieldtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructors taking a day counter only, and the jumps (jumpDates, jumpTimes), which no pilot curve uses

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The interest-rate term structure: the base of the curves that give discount factors, zero rates and forward rates.
/// </summary>
public abstract class YieldTermStructure : TermStructure
{
    // time interval used in finite differences
    private const double Dt = 0.0001;

    /// <summary>Initializes a new instance of the <see cref="YieldTermStructure"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar used for reference and option date calculation, if any.</param>
    /// <param name="dc">The day counter used for date/time conversion.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    protected YieldTermStructure(Date referenceDate, Calendar? cal, DayCounter dc, int? settlementDays = null)
        : base(referenceDate, cal, dc, settlementDays)
    {
    }

    /// <summary>The discount factor from a date to the reference date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The discount factor.</returns>
    public double Discount(Date d, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return Discount(TimeFromReference(d), extrapolate);
    }

    /// <summary>
    /// The discount factor from a time to the reference date, the time measured by the curve's day counter.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The discount factor.</returns>
    public double Discount(double t, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        return DiscountImpl(t);
    }

    /// <summary>
    /// The implied zero-yield rate for a date, with the given day-counting rule; the time is calculated as a fraction of
    /// a year from the reference date.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="resultDayCounter">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The zero rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ZeroRate(
        Date d, DayCounter resultDayCounter, Compounding comp, Frequency freq = Frequency.Annual, bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(resultDayCounter);
        Date.ThrowIfDefault(d, nameof(d));
        double t = TimeFromReference(d);
        if (t == 0)
        {
            double compound0 = 1.0 / Discount(Dt, extrapolate);

            // t has been calculated with a possibly different daycounter but the difference should not matter for very
            // small times
            return InterestRate.ImpliedRate(compound0, resultDayCounter, comp, freq, Dt);
        }

        double compound = 1.0 / Discount(t, extrapolate);
        return InterestRate.ImpliedRate(compound, resultDayCounter, comp, freq, ReferenceDate, d);
    }

    /// <summary>The implied zero-yield rate for a time, measured by the curve's day counter.</summary>
    /// <param name="t">The time.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The zero rate, with the curve's day counter.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ZeroRate(double t, Compounding comp, Frequency freq = Frequency.Annual, bool extrapolate = false)
    {
        if (t == 0.0)
        {
            t = Dt;
        }

        double compound = 1.0 / Discount(t, extrapolate);
        return InterestRate.ImpliedRate(compound, DayCounter, comp, freq, t);
    }

    /// <summary>
    /// The forward rate between two dates, with the given day-counting rule; the instantaneous forward rate when the
    /// dates are equal.
    /// </summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="resultDayCounter">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The forward rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ForwardRate(
        Date d1,
        Date d2,
        DayCounter resultDayCounter,
        Compounding comp,
        Frequency freq = Frequency.Annual,
        bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(resultDayCounter);
        Date.ThrowIfDefault(d1, nameof(d1));
        Date.ThrowIfDefault(d2, nameof(d2));
        if (d1 == d2)
        {
            CheckRange(d1, extrapolate);
            double t1 = Math.Max(TimeFromReference(d1) - (Dt / 2.0), 0.0);
            double t2 = t1 + Dt;
            double compound0 = Discount(t1, true) / Discount(t2, true);

            // times have been calculated with a possibly different daycounter but the difference should not matter for
            // very small times
            return InterestRate.ImpliedRate(compound0, resultDayCounter, comp, freq, Dt);
        }

        if (!(d1 < d2))
        {
            throw new ArgumentException($"{d1} later than {d2}", nameof(d1));
        }

        double compound = Discount(d1, extrapolate) / Discount(d2, extrapolate);
        return InterestRate.ImpliedRate(compound, resultDayCounter, comp, freq, d1, d2);
    }

    /// <summary>The forward rate between a date and the date a period later. Dates are not adjusted for holidays.</summary>
    /// <param name="d">The start date.</param>
    /// <param name="p">The period.</param>
    /// <param name="resultDayCounter">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The forward rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ForwardRate(
        Date d,
        Period p,
        DayCounter resultDayCounter,
        Compounding comp,
        Frequency freq = Frequency.Annual,
        bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(resultDayCounter);
        Date.ThrowIfDefault(d, nameof(d));
        return ForwardRate(d, d + p, resultDayCounter, comp, freq, extrapolate);
    }

    /// <summary>
    /// The forward rate between two times, measured by the curve's day counter; the instantaneous forward rate when the
    /// times are equal.
    /// </summary>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum time.</param>
    /// <returns>The forward rate, with the curve's day counter.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate ForwardRate(
        double t1, double t2, Compounding comp, Frequency freq = Frequency.Annual, bool extrapolate = false)
    {
        double compound;
        if (t2 == t1)
        {
            CheckRange(t1, extrapolate);
            t1 = Math.Max(t1 - (Dt / 2.0), 0.0);
            t2 = t1 + Dt;
            compound = Discount(t1, true) / Discount(t2, true);
        }
        else
        {
            if (!(t2 > t1))
            {
                throw new ArgumentException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"t1 ({t1}) >= t2 ({t2})"), nameof(t1));
            }

            compound = Discount(t1, extrapolate) / Discount(t2, extrapolate);
        }

        return InterestRate.ImpliedRate(compound, DayCounter, comp, freq, t2 - t1);
    }

    /// <summary>
    /// The discount factor at a time, which derived curves calculate. Range checks have already been performed, so it
    /// must assume that extrapolation is required.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected abstract double DiscountImpl(double t);
}
